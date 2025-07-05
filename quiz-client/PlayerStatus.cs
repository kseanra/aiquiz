public class PlayerState
{
    public string? ConnectionId { get; set; }
    public string? Name { get; set; }
    public int CurrentQuestionIndex { get; set; }
    public PlayerStatus Status { get; set; } = PlayerStatus.Active;
}

public enum PlayerStatus
{
    JustJoined,
    Active,
    Disconnected,
    ReadyForGame,
    GameOver,
    WaitingForGame,
    GameWinner
}
