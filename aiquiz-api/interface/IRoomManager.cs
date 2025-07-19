using System.Collections.Concurrent;
using aiquiz_api.Models;

public interface IRoomManager
{
    GameRoom JoinRoomAsync(string connectionId, PlayerState player);
    GameRoom? JoinRoomByPasswordAsync(string connectionId, PlayerState player, string password);
    Task LeaveRoomAsync(string connectionId);
    GameRoom GetGameRoomById(string roomId);
    Task<GameRoom?> GetRoomByConnectionAsync(string connectionId);
    Task<GameRoom?> SetPlayerQuestionAsync(string roomId, string connectionId, int questionIndex);
    Task<GameRoom?> SetPlayerStatesAsync(string roomId, string connectionId, PlayerStatus status);
    Task<GameRoom?> SetQuizAsync(string connectionId, List<Quiz> questions);
    Task<(bool, GameRoom?, Quiz?)> MarkAnswer(string connectionId, string answer);
    GameRoom? SetGameRoomStatus(string roomId, RoomStatus roomStatus);
    bool GameRoomClosed(string roomId);
    GameRoom? CreateRoom(string roomName, PlayerState owner, int? maxPlayer, bool isPrivate = false);
}