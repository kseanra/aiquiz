public class RoomManager : IRoomManager
{
    private static readonly Dictionary<string, GameRoom> Rooms = new();
    private const int MaxPlayers = 4;

    public async Task<GameRoom> JoinRoomAsync(string connectionId)
    {
        var room = Rooms.Values.FirstOrDefault(r => !r.IsGameStarted && r.PlayerConnections.Count < MaxPlayers);
        if (room == null)
        {
            room = new GameRoom { RoomId = $"room-{Guid.NewGuid()}" };
            Rooms[room.RoomId] = room;
        }

        room.PlayerConnections.Add(connectionId);
        room.PlayerReady[connectionId] = false;

        return room;
    }

    public async Task LeaveRoomAsync(string connectionId)
    {
        var room = Rooms.Values.FirstOrDefault(r => r.PlayerConnections.Contains(connectionId));
        if (room != null)
        {
            room.PlayerConnections.Remove(connectionId);
            room.PlayerReady.Remove(connectionId);
            if (!room.PlayerConnections.Any())
                Rooms.Remove(room.RoomId);
        }
    }

    public async Task<GameRoom?> GetRoomByConnectionAsync(string connectionId)
    {
        return Rooms.Values.FirstOrDefault(r => r.PlayerConnections.Contains(connectionId));
    }

    public async Task<bool> SetPlayerReadyAsync(string connectionId, bool ready)
    {
        var room = await GetRoomByConnectionAsync(connectionId);
        if (room == null) return false;

        room.PlayerReady[connectionId] = ready;
        return room.PlayerReady.Values.All(x => x); // return true if all ready
    }
}
