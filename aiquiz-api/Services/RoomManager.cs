using System.Collections.Concurrent;
using System.Text.Json;
using aiquiz_api.Models;

public class RoomManager : IRoomManager
{
    private static readonly ConcurrentDictionary<string, GameRoom> Rooms = new();
    private static readonly ConcurrentDictionary<string, object> keyLocks = new();
    private static readonly object gloabLock = new();
    private const int MaxPlayers = 10; // Maximum number of players per room
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
    public async Task<GameRoom> JoinRoomAsync(string connectionId, PlayerState player)
    {
        var room = await FindAvailableGameRoomAsync();
        if (room == null)
        {
            lock (gloabLock)
            {
                room = new GameRoom { RoomId = $"room-{Guid.NewGuid()}" };
                Rooms.AddOrUpdate(room.RoomId, room, (key, oldValue) => room);
                _logger.LogInformation("Create a new game room: {id}", room.RoomId);
            }
        }

        player.Status = PlayerStatus.ReadyForGame;
        room.Players.AddOrUpdate(connectionId, player, (key, oldValue) => player );
        room.ReadyForGame = room.Players.Count() == MaxPlayers;
        //_logger.LogInformation("Player {name} add to game room {id}", player.Name, room.RoomId);
        return room;
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
                    _logger.LogDebug("Try to remove player {player} from game room {id}", player?.Name, room.RoomId);

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
                            _logger.LogInformation("Game room {id} was removed", room.RoomId);
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

    public async Task<GameRoom?> SetPlayerReadyAsync(string connectionId, string? name = null)
    {
        return await SetPlayerStatesAsync(connectionId, name, null, PlayerStatus.ReadyForGame);
    }

    public async Task<GameRoom?> SetPlayerQuestionAsync(string connectionId, int questionIndex)
    {
        return await SetPlayerStatesAsync(connectionId, null, questionIndex, null);
    }

    public Task<GameRoom?> SetPlayerStatesAsync(string connectionId, PlayerStatus status)
    {
        return SetPlayerStatesAsync(connectionId, null, null, status);
    }

    public Task<GameRoom?> SetPlayerNameAsync(string connectionId, string playerName)
    {
        return SetPlayerStatesAsync(connectionId, playerName, null, null);
    }


    public async Task<GameRoom?> SetQuizAsync(string connectionId, List<Quiz> questions)
    {
        var room = await GetRoomByConnectionAsync(connectionId);
        if (room == null) { return null; }

        room.Questions = questions;
        return room;
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
            if (player.CurrentQuestionIndex < room.Questions.Count - 1 )
            {
                player.CurrentQuestionIndex++;
                _logger.LogDebug("Set player {name} in Question {index}", player.Name, player.CurrentQuestionIndex);
                await SetPlayerQuestionAsync(connectionId, player.CurrentQuestionIndex);
                nextQuiz = room.Questions[player.CurrentQuestionIndex];
            }
            else
            {
                if (string.IsNullOrEmpty(room.GameWinner))
                {
                    object lockObj = keyLocks.GetOrAdd(room.RoomId, _ => new object());
                    lock (lockObj)
                    {
                        if (room != null && player.Name != null)
                        {
                            _logger.LogInformation("Set game room's {id} winner is : {name} ", room.RoomId, player.Name);
                            room.GameWinner = player.Name;
                        }
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
    
    public async Task<GameRoom?> FindAvailableGameRoomAsync()
    {
        return await Task.Run(() =>
        {
            lock (gloabLock)
            {
                _logger.LogDebug("Find available game rooom : {rooms}", JsonSerializer.Serialize(Rooms));
                return Rooms.Values.FirstOrDefault(r => !r.IsGameStarted && string.IsNullOrEmpty(r.GameWinner) && r.Players.Count() < MaxPlayers);
            }
        }
        );
    }
}
