using System.Collections.Concurrent;
using System.Text.Json;
using aiquiz_api.Models;

public class RoomManager : IRoomManager
{
    private static readonly ConcurrentDictionary<string, GameRoom> Rooms = new();
    private const int MaxPlayers = 2; // Maximum number of players per room
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
            _logger.LogDebug("Create a new game room");
            room = new GameRoom { RoomId = $"room-{Guid.NewGuid()}" };
            Rooms.AddOrUpdate(room.RoomId, room, (key, oldValue) => room);
        }

        player.Status = PlayerStatus.ReadyForGame;
        room.Players.AddOrUpdate(connectionId, player, (key, oldValue) => player );
        room.ReadyForGame = room.Players.Count() == MaxPlayers;
        //_logger.LogDebug(JsonSerializer.Serialize(Rooms));
        return room;
    }

    public async Task LeaveRoomAsync(string connectionId)
    {
        var room = await GetRoomByConnectionAsync(connectionId);
        if (room != null)
        {
            room.Players.TryRemove(connectionId, out _);
            _logger.LogDebug("Remove player {player}", connectionId);

            //Delete game room
            if (!room.Players.Any())
            {
                _logger.LogDebug("Remove game room {id}", room.RoomId);
                Rooms.TryRemove(room.RoomId, out _); // Remove room if no players left
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
            _logger.LogInformation("Player {name} is on Question {indx}", player.Name, player.CurrentQuestionIndex);
            _logger.LogInformation("Mark Question: {question}", currentQuestion.Question);
            _logger.LogInformation("Correct Answer is {answer}", currentQuestion.Answer);
            _logger.LogInformation("User Submit is {answer}", answer);
            isCorrect = string.Equals(answer?.Trim(), currentQuestion.Answer?.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        if (isCorrect)
        {
            if (player.CurrentQuestionIndex < room.Questions.Count - 1 )
            {
                player.CurrentQuestionIndex++;
                _logger.LogInformation("Set player Question index + 1");
                await SetPlayerQuestionAsync(connectionId, player.CurrentQuestionIndex);
                nextQuiz = room.Questions[player.CurrentQuestionIndex];
            }
            else
            {
                _logger.LogInformation("Set Player is winner");
                var winner = room.Players.Values.Any(p => p.Status == PlayerStatus.GameWinner);
                if (winner == false)
                {
                    room = await SetPlayerStatesAsync(connectionId, null, player.CurrentQuestionIndex, PlayerStatus.GameWinner);
                    if (room != null && player.Name != null)
                        room.GameWinner = player.Name;
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
        return await Task.Run(() => {
                _logger.LogDebug("Find available game rooom : {rooms}", JsonSerializer.Serialize(Rooms));
                return Rooms.Values.FirstOrDefault(r => !r.IsGameStarted && string.IsNullOrEmpty(r.GameWinner) && r.Players.Count() < MaxPlayers);
            }
        );
    }
}
