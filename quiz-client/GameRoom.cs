public class GameRoom
{
    public required string RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public int? MaxPlayers { get; set; } = 20; // Default max players
    public string? RoomPassword { get; set; }
}

