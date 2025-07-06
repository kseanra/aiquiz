using System.Collections.Concurrent;
using aiquiz_api.Models;

public interface IRoomManager
{
    Task<GameRoom> JoinRoomAsync(string connectionId);
    Task LeaveRoomAsync(string connectionId);
    Task<GameRoom?> GetRoomByConnectionAsync(string connectionId);
    Task<GameRoom?> SetPlayerReadyAsync(string connectionId);
    Task<GameRoom?> SetPlayerQuestionAsync(string connectionId, int questionIndex);
    Task<GameRoom?> SetPlayerStatesAsync(string connectionId, PlayerStatus status);
    Task<GameRoom?> SetPlayerNameAsync(string connectionId, string playerName);
    Task<GameRoom?> SetQuizAsync(string connectionId, List<Quiz> questions);
    Task<(bool, GameRoom?, Quiz?)> MarkAnswer(string connectionId, string answer);
}