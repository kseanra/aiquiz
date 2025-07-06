using System.Collections.Concurrent;
using aiquiz_api.Models;

public class GameRoom
{
    public required string RoomId { get; set; }
    public bool IsGameStarted { get; set; } = false;
    public ConcurrentDictionary<string, PlayerState> Players { get; set; } = new();
    public List<Quiz> Questions { get; set; } = new();
    public bool ReadyForGame { get; set; } = false;
    public string GameWinner { get; set; } = string.Empty;  
}
