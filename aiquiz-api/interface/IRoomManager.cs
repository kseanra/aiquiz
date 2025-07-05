public interface IRoomManager
{
    Task<GameRoom> JoinRoomAsync(string connectionId);
    Task LeaveRoomAsync(string connectionId);
    Task<GameRoom?> GetRoomByConnectionAsync(string connectionId);
    Task<bool> SetPlayerReadyAsync(string connectionId, bool ready);
}
