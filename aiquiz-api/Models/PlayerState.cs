namespace aiquiz_api.Models;

public class PlayerState
{
    public string? ConnectionId { get; set; }
    public string? Name { get; set; }
    public int CurrentQuestionIndex { get; set; }
    public bool Disconnected { get; set; } = false; // Indicates if the player is disconnected
    public bool JustJoined { get; set; } = false; // Indicates if the player just joined
}