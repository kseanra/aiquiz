using System.Collections.Concurrent;
using aiquiz_api.Models;
public class GameRoom
{
    public required string RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public Guid OwnerId { get; set; } = Guid.Empty;
    public bool IsGameStarted { get; set; } = false;
    public RoomStatus Status { get; set; } = RoomStatus.Active;
    public ConcurrentDictionary<string, PlayerState> Players { get; set; } = new();
    public List<Quiz> Questions { get; set; } = new();
    public bool ReadyForGame { get; set; } = false;
    public string GameWinner { get; set; } = string.Empty;
    public bool IsPrivate { get; set; } = false;
    public int? MaxPlayers { get; set; } = 20; // Default max players

    // Randomly select a player from the room
    public PlayerState? GetRandomPlayer()
    {
        if (Players.Count == 0) return null;
        var rnd = new Random();
        var playerList = Players.Values.ToList();
        int idx = rnd.Next(playerList.Count);
        return playerList[idx];
    }
    public string? Topic { get; set; }
}

public enum RoomStatus
{
    Active,
    Ready,
    GameStarted,
    Close
}
