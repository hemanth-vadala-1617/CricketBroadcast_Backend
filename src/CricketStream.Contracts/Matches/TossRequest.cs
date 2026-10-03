namespace CricketStream.Contracts.Matches;
public class TossRequest {
    public Guid TossWinnerTeamId { get; set; }
    public string TossDecision { get; set; } = string.Empty; // Bat | Bowl
}
