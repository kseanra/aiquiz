using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using aiquiz_api.Models;
using Microsoft.AspNetCore.Mvc;
using Prometheus;

public class RoomManager : IRoomManager
{
    private static readonly ConcurrentDictionary<string, GameRoom> Rooms = new();
    private static readonly ConcurrentDictionary<string, object> keyLocks = new();
    private static readonly object gloabLock = new();
    private const int MaxPlayers = 2; // Maximum number of players per room
    private readonly ILogger<RoomManager> _logger;
    public RoomManager(ILogger<RoomManager> logger)
    {
        _logger = logger;
    }
    
    public GameRoom GetGameRoomById(string roomId)
    {
        return Rooms.TryGetValue(roomId, out var room) ? room : null!;
    }

    /// <summary>
    /// Assigned player to game room
    /// </summary>
    /// <param name="connectionId"></param>
    /// <returns></returns>
    public GameRoom JoinRoomAsync(string connectionId, PlayerState player)
    {
        lock (gloabLock)
        {
            var room = FindAvailableGameRoomAsync();
            if (room == null)
            {
                room = new GameRoom { RoomId = $"room-{Guid.NewGuid()}", MaxPlayers = MaxPlayers, RoomName = "Default Room", Topic = null, Status = RoomStatus.Active };
                Rooms.AddOrUpdate(room.RoomId, room, (key, oldValue) => room);
                _logger.LogInformation("Create a new game room: {id}", room.RoomId);
            }
            player.Status = PlayerStatus.ReadyForGame;
            var lockObj = keyLocks.GetOrAdd(room.RoomId, _ => new object());
            RoomCounter.Set(Rooms.Count);
            lock (lockObj)
            {
                room.Players.AddOrUpdate(connectionId, player, (key, oldValue) => player);
                room.Status = room.Players.Count() == room.MaxPlayers ? RoomStatus.Ready : room.Status;
                _logger.LogDebug("Player {name} add to game room {id}", player.Name, room.RoomId);
                var playerCount = Rooms.Sum(p => p.Value.Players.Count());
                PlayerCounter.Set(playerCount);
                return room;
            }

        }
    }

    public GameRoom? JoinRoomByPasswordAsync(string connectionId, PlayerState player, string password)
    {
        lock (gloabLock)
        {
            var room = FindAvailableGameRoomByPasswordAsync(password);
            if (room == null)
            {
                _logger.LogError("Can't find game room by password: {password}", password);
                return null;
            }
            player.Status = PlayerStatus.ReadyForGame;
            var lockObj = keyLocks.GetOrAdd(room.RoomId, _ => new object());
            lock (lockObj)
            {
                room.Players.AddOrUpdate(connectionId, player, (key, oldValue) => player);
                room.Status = room.Players.Count() == room.MaxPlayers ? RoomStatus.Ready : room.Status;
                _logger.LogDebug("Player {name} add to game room {id}", player.Name, room.RoomId);
                var playerCount = Rooms.Sum(p => p.Value.Players.Count());
                PlayerCounter.Set(playerCount);
                return room;
            }

        }
    }

    public GameRoom? CreateRoom(string roomName, PlayerState owner, int? maxPlayer, bool isPrivate = false)
    {
        lock (gloabLock)
        {
            if (string.IsNullOrWhiteSpace(roomName) || owner == null)
                return null;

            var roomId = $"room-{Guid.NewGuid()}";
            var room = new GameRoom
            {
                RoomId = roomId,
                RoomName = roomName,
                IsPrivate = isPrivate,
                MaxPlayers = maxPlayer,
                OwnerId = owner.ConnectionId,
                Topic = null,
                Status = RoomStatus.Active,
                RoomPassword = GenerateSecureCode()
            };
            if (!string.IsNullOrEmpty(owner.ConnectionId))
            {
                room.Players.TryAdd(owner.ConnectionId, owner);
            }
            else
            {
                _logger.LogError("Owner's ConnectionId is null or empty. Cannot add to room.");
                return null;
            }
            Rooms.TryAdd(roomId, room);
            RoomCounter.Set(Rooms.Count);
            _logger.LogInformation("Created private room {roomId} with owner {owner}", roomId, owner.Name);
            return room;
        }
    }

    public async Task LeaveRoomAsync(string connectionId)
    {
        var room = await GetRoomByConnectionAsync(connectionId);

        if (room != null)
        {
            var lockObj = keyLocks.GetOrAdd(room.RoomId, _ => new object());
            lock (lockObj)
            {
                if (room.Players.TryRemove(connectionId, out var player))
                {
                    _logger.LogDebug("Removing player {player} from game room {id}", player?.Name, room.RoomId);
                    PlayerCounter.Set(Rooms.Sum(p => p.Value.Players.Count()));
                    //Delete game room
                    if (!room.Players.Any() && room.Status == RoomStatus.Close)
                    {
                        _logger.LogDebug("Try to remove game room {id}", room.RoomId);
                        if (!Rooms.TryRemove(room.RoomId, out _))
                        {
                            _logger.LogError("Remove game room {id} failed", room.RoomId);
                        }
                        else
                        {
                            RoomCounter.Set(Rooms.Count);
                            _logger.LogInformation("Game room {id} removed", room.RoomId);
                        }
                    }
                }
                else
                {
                    _logger.LogError("Failed to Remove room {id} : {status}", room.RoomId, room.Status);
                }
            }
        }
    }

    public async Task<GameRoom?> GetRoomByConnectionAsync(string connectionId)
    {
        return await Task.Run(() =>
            Rooms.Values.FirstOrDefault(r => r.Players.ContainsKey(connectionId)) ?? Rooms.Values.FirstOrDefault(r => r.Players.Count < r.MaxPlayers)
        );
    }

    public async Task<GameRoom?> SetPlayerQuestionAsync(string roomId, string connectionId, int questionIndex)
    {
        return await SetPlayerStatesAsync(roomId, connectionId, questionIndex, null);
    }

    public Task<GameRoom?> SetPlayerStatesAsync(string roomId, string connectionId, PlayerStatus status)
    {
        return SetPlayerStatesAsync(roomId, connectionId, null, status);
    }

    public async Task<GameRoom?> SetQuizAsync(string connectionId, List<Quiz> questions)
    {
        var room = await GetRoomByConnectionAsync(connectionId);
        if (room == null) { return null; }

        room.Questions = questions;
        return room;
    }

    public bool GameRoomClosed(string roomId)
    {
        if (!Rooms.ContainsKey(roomId))
        {
            return true;
        }

        var lockObj = keyLocks.GetOrAdd(roomId, _ => new object());
        lock (lockObj)
        {
            Rooms.TryGetValue(roomId, out var room);
            if (room == null)
            {
                return true; // Room not found, consider it closed
            }   
            return Rooms[roomId].Status == RoomStatus.Close;
        }
    }

    public GameRoom? SetGameRoomStatus(string roomId, RoomStatus roomStatus)
    {
        var lockObj = keyLocks.GetOrAdd(roomId, _ => new object());
        lock (lockObj)
        {
            if (!Rooms.ContainsKey(roomId))
            {
                _logger.LogError("SetGameRoomStatus {roomStatus} room {id} was not found", roomStatus, roomId);
                return null;
            }
            var room = Rooms[roomId];
            room.Status = roomStatus;
            if (roomStatus == RoomStatus.Close)
            {
                if (room.Status == RoomStatus.GameStarted)
                {
                    _logger.LogDebug("Room {id} game over", roomId);
                    Rooms.AddOrUpdate(roomId, room, (key, val) => room);
                    _logger.LogDebug("Room {id} closed!", roomId);
                }
            }
            else
            {
                Rooms.AddOrUpdate(roomId, room, (key, val) => room);
            }
            return room;
        }
    }

    public async Task<(bool, GameRoom?, Quiz?)> MarkAnswer(string connectionId, string answer)
    {
        var room = await GetRoomByConnectionAsync(connectionId);
        if (room == null) return (false, null, null);

        if (!room.Players.TryGetValue(connectionId, out var player)) return (false, null, null);

        bool isCorrect = false;
        Quiz? nextQuiz = null;
        // Check if the answer is correct
        if (player.CurrentQuestionIndex < room.Questions.Count)
        {
            var currentQuestion = room.Questions[player.CurrentQuestionIndex];
            _logger.LogDebug("Player {name} submited Q{index} answer: {answer}", player.Name, player.CurrentQuestionIndex, answer);
            _logger.LogDebug("Mark Question: {question}", currentQuestion.Question);
            _logger.LogDebug("Correct Answer is {answer}", currentQuestion.Answer);

            isCorrect = string.Equals(answer?.Trim(), currentQuestion.Answer?.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        if (isCorrect)
        {
            if (player.CurrentQuestionIndex < room.Questions.Count - 1)
            {
                player.CurrentQuestionIndex++;
                _logger.LogDebug("Set player {name} in Question {index}", player.Name, player.CurrentQuestionIndex);
                await SetPlayerQuestionAsync(room.RoomId, connectionId, player.CurrentQuestionIndex);
                nextQuiz = room.Questions[player.CurrentQuestionIndex];
            }
            else if(keyLocks.ContainsKey(room.RoomId))
            {
                object lockObj = keyLocks.GetOrAdd(room.RoomId, _ => new object());
                lock (lockObj)
                {
                    room = GetGameRoomById(room.RoomId);
                    if (room != null && player.Name != null && string.IsNullOrEmpty(room.GameWinner) && !string.IsNullOrEmpty(player.ConnectionId))
                    {
                        _logger.LogInformation("Set game room's {id} with player {count} winner is : {name} ", room.RoomId, room.Players.Count(), player.Name);
                        _logger.LogDebug(JsonSerializer.Serialize(room));
                        room.Players[player.ConnectionId].Status = PlayerStatus.GameWinner;
                        room.GameWinner = player.Name;
                    }
                }
            }
        }

        return (isCorrect, room, nextQuiz);
    }

    private async Task<GameRoom?> SetPlayerStatesAsync(string roomId, string connectionId, int? questionIndex, PlayerStatus? status)
    {
        var room = Rooms.ContainsKey(roomId) ? Rooms[roomId] : await GetRoomByConnectionAsync(connectionId);
        if (room == null) { return null; }

        if (room.Players.TryGetValue(connectionId, out var player))
        {
            player.Status = status ?? player.Status;
            player.CurrentQuestionIndex = questionIndex ?? player.CurrentQuestionIndex;
            room.Players.AddOrUpdate(connectionId, player, (key, val) => player);
        }

        return room;
    }

    public GameRoom? FindAvailableGameRoomAsync()
    {
        _logger.LogDebug("Find available game rooom : {rooms}", JsonSerializer.Serialize(Rooms));
        return Rooms.Values.FirstOrDefault(r => r.Status != RoomStatus.GameStarted && string.IsNullOrEmpty(r.GameWinner) && r.Players.Count() < r.MaxPlayers);
    }

    private GameRoom? FindAvailableGameRoomByPasswordAsync(string password)
    {
        _logger.LogDebug("Find available game rooom by password : {pass}", password);
        return Rooms.Values.FirstOrDefault(r => r.Status != RoomStatus.GameStarted && string.IsNullOrEmpty(r.GameWinner) && r.Players.Count() < r.MaxPlayers && r.RoomPassword == password);
    }

    private static readonly Gauge RoomCounter = Metrics.CreateGauge(
        "room_total",
        "number of rooms"
    );

    private static readonly Gauge PlayerCounter = Metrics.CreateGauge(
        "player_total",
        "number of players in the game"
    );
    
    private string GenerateSecureCode()
    {
        using (var rng = RandomNumberGenerator.Create())
        {
            byte[] bytes = new byte[4];
            rng.GetBytes(bytes);
            int value = BitConverter.ToInt32(bytes, 0) & 0x7FFFFFFF; // Make positive
            return (value % 900000 + 100000).ToString();
        }
    }
}
