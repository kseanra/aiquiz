public class GameRoom
{
    public required string RoomId { get; set; }
    public List<string> PlayerConnections { get; set; } = new();
    public bool IsGameStarted { get; set; } = false;
    public Dictionary<string, bool> PlayerReady { get; set; } = new();
}
