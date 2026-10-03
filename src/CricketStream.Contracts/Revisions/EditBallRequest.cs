using CricketStream.Contracts.Scoring;
namespace CricketStream.Contracts.Revisions;
/// <summary>Replaces the last ball: void it, then score the corrected one, in one transaction.</summary>
public class EditBallRequest {
    public string Reason { get; set; } = string.Empty;
    public ScoreBallRequest Ball { get; set; } = new();
}
