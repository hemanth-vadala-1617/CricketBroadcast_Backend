namespace CricketStream.Contracts.Matches;
public class CreateMatchRequest {
    public Guid TournamentId { get; set; }
    public Guid HomeTeamId { get; set; }
    public Guid AwayTeamId { get; set; }
    public Guid VenueId { get; set; }
    public Guid MatchRulesId { get; set; }
    public Guid? OverlayThemeId { get; set; }
    public string? Title { get; set; }
    public string MatchFormat { get; set; } = "T20";
    public DateTimeOffset? ScheduledStart { get; set; }
}
