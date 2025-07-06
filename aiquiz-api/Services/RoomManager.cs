using System.Collections.Concurrent;
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

    public async Task<GameRoom> JoinRoomAsync(string connectionId)
    {
        var room = await GetRoomByConnectionAsync(connectionId);
        if (room == null)
        {
            _logger.LogDebug("Create a new game room");
            room = new GameRoom { RoomId = $"room-{Guid.NewGuid()}" };
            Rooms[room.RoomId] = room;
        }

        room.Players[connectionId] = new PlayerState { ConnectionId = connectionId, Status = PlayerStatus.JustJoined };

        return room;
    }

    public async Task LeaveRoomAsync(string connectionId)
    {
        var room = await GetRoomByConnectionAsync(connectionId);
        if (room != null)
        {
            room.Players.TryRemove(connectionId, out _);
            _logger.LogDebug("Remove player {player}", connectionId);
            if (!room.Players.Any())
                _logger.LogDebug("Remove game room {id}", room.RoomId);
                Rooms.TryRemove(room.RoomId, out _); // Remove room if no players left
        }
    }

    public async Task<GameRoom?> GetRoomByConnectionAsync(string connectionId)
    {
        return Rooms.Values.FirstOrDefault(r => r.Players.ContainsKey(connectionId)) ?? Rooms.Values.FirstOrDefault( r => r.Players.Count < MaxPlayers);
    }

    public async Task<GameRoom?> SetPlayerReadyAsync(string connectionId)
    {
        return await SetPlayerStatesAsync(connectionId, null, null, PlayerStatus.ReadyForGame);
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

        if (status == PlayerStatus.ReadyForGame && room.Players.Count() == MaxPlayers)
        {
            room.ReadyForGame = room.Players.Values.All(p => p.Status == PlayerStatus.ReadyForGame);
        }

        return room;
    }

    public async Task<(bool, GameRoom?, Quiz?)> MarkAnswer(string connectionId, string answer)
    {
        var room = await GetRoomByConnectionAsync(connectionId);
        if (room == null) return (false, null, null);

        var player = room.Players[connectionId];
        if (player == null) return (false, null, null);

        bool isCorrect = false;
        Quiz nextQuiz = null;
        // Check if the answer is correct
        if (player.CurrentQuestionIndex < room.Questions.Count)
        {
            var currentQuestion = room.Questions[player.CurrentQuestionIndex];
            _logger.LogInformation("Player is on Question {indx}", player.CurrentQuestionIndex);
            _logger.LogInformation("Mark Question: {question}", currentQuestion.Question);
            _logger.LogInformation("Correct Answer is {answer}", currentQuestion.Answer);
            _logger.LogInformation("User Submit is {answer}", answer);
            isCorrect = string.Equals(answer?.Trim(), currentQuestion.Answer?.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        if (isCorrect)
        {
            player.CurrentQuestionIndex++;
            if (player.CurrentQuestionIndex < room.Questions.Count - 1)
            {
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
                    if(room != null && player.Name != null)
                        room.GameWinner = player.Name;
                }
            }
        }

        return (isCorrect, room, nextQuiz);
    }
}
