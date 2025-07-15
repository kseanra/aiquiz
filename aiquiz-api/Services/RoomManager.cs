using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using aiquiz_api.Models;

public class RoomManager : IRoomManager
{
    private static readonly ConcurrentDictionary<string, GameRoom> Rooms = new();
    private static readonly ConcurrentDictionary<string, object> keyLocks = new();
    private static readonly object gloabLock = new();
    private const int MaxPlayers = 20; // Maximum number of players per room
    private readonly ILogger<RoomManager> _logger;
    public RoomManager(ILogger<RoomManager> logger)
    {
        _logger = logger;
    }
    public GameRoom GetGameRoomById(string roomId)
    {
        return Rooms[roomId];
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
                room = new GameRoom { RoomId = $"room-{Guid.NewGuid()}" };
                Rooms.AddOrUpdate(room.RoomId, room, (key, oldValue) => room);
                _logger.LogInformation("Create a new game room: {id}", room.RoomId);
            }
            player.Status = PlayerStatus.ReadyForGame;
            var lockObj = keyLocks.GetOrAdd(room.RoomId, _ => new object());
            lock (lockObj)
            {
                room.Players.AddOrUpdate(connectionId, player, (key, oldValue) => player);
                room.Status = room.Players.Count() == MaxPlayers ? RoomStatus.Ready : room.Status;
                _logger.LogDebug("Player {name} add to game room {id}", player.Name, room.RoomId);
                return room;
            }
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

                    //Delete game room
                    if (!room.Players.Any())
                    {
                        _logger.LogInformation("Try to remove game room {id}", room.RoomId);
                        if (!Rooms.TryRemove(room.RoomId, out _))
                        {
                            _logger.LogError("Remove game room {id} failed", room.RoomId);
                        }
                        else
                        {
                            _logger.LogInformation("Game room {id} removed", room.RoomId);
                        }
                    }
                }
                else
                {
                    _logger.LogError("Remove player {name} from game room {id} failed", player?.Name, room.RoomId);
                }
            }
        }
    }

    public async Task<GameRoom?> GetRoomByConnectionAsync(string connectionId)
    {
        return await Task.Run(() =>
            Rooms.Values.FirstOrDefault(r => r.Players.ContainsKey(connectionId)) ?? Rooms.Values.FirstOrDefault(r => r.Players.Count < MaxPlayers)
        );
    }

    public async Task<GameRoom?> SetPlayerQuestionAsync(string connectionId, int questionIndex)
    {
        return await SetPlayerStatesAsync(connectionId, null, questionIndex, null);
    }

    public Task<GameRoom?> SetPlayerStatesAsync(string connectionId, PlayerStatus status)
    {
        return SetPlayerStatesAsync(connectionId, null, null, status);
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
        var lockObj = keyLocks.GetOrAdd(roomId, _ => new object());
        lock (lockObj)
        {
            return Rooms[roomId].Status == RoomStatus.Close;
        }
    }

    public GameRoom? SetGameRoomStatus(string roomId, RoomStatus roomStatus)
    {
        var lockObj = keyLocks.GetOrAdd(roomId, _ => new object());
        lock (lockObj)
        {
            if (roomStatus == RoomStatus.Close && Rooms[roomId].Status != roomStatus)
            {
                _logger.LogInformation("Room {id} game over", roomId);
                Rooms[roomId].Status = roomStatus;
                _logger.LogInformation("Room {id} closed!", roomId);
            }
            else
            {
                Rooms[roomId].Status = roomStatus;
            }
            return Rooms[roomId];
        }
    }

    public async Task<(bool, GameRoom?, Quiz?)> MarkAnswer(string connectionId, string answer)
    {
        var room = await GetRoomByConnectionAsync(connectionId);
        if (room == null) return (false, null, null);

        var player = room.Players[connectionId];
        if (player == null) return (false, null, null);

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
                await SetPlayerQuestionAsync(connectionId, player.CurrentQuestionIndex);
                nextQuiz = room.Questions[player.CurrentQuestionIndex];
            }
            else
            {
                object lockObj = keyLocks.GetOrAdd(room.RoomId, _ => new object());
                lock (lockObj)
                {
                    room = GetGameRoomById(room.RoomId);
                    if (room != null && player.Name != null && string.IsNullOrEmpty(room.GameWinner))
                    {
                            _logger.LogInformation("Set game room's {id} with player {count} winner is : {name} ", room.RoomId, room.Players.Count(), player.Name);
                            _logger.LogDebug(JsonSerializer.Serialize(room));
                            room.GameWinner = player.Name;
                    }
                }
            }
        }

        return (isCorrect, room, nextQuiz);
    }

    private async Task<GameRoom?> SetPlayerStatesAsync(string connectionId, string? playerName, int? questionIndex, PlayerStatus? status)
    {
        var room = await GetRoomByConnectionAsync(connectionId);
        if (room == null) { return null; }

        if (room.Players.TryGetValue(connectionId, out var player))
        {
            player.Status = status ?? player.Status;
            player.Name = playerName ?? player.Name;
            player.CurrentQuestionIndex = questionIndex ?? player.CurrentQuestionIndex;
            room.Players[connectionId] = player;
        }

        return room;
    }
    
    public GameRoom? FindAvailableGameRoomAsync()
    {
        _logger.LogDebug("Find available game rooom : {rooms}", JsonSerializer.Serialize(Rooms));
        return Rooms.Values.FirstOrDefault(r => r.Status != RoomStatus.GameStarted && string.IsNullOrEmpty(r.GameWinner) && r.Players.Count() < MaxPlayers);
    }
}
