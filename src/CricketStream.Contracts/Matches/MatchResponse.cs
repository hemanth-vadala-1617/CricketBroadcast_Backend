namespace CricketStream.Contracts.Matches;
/// <summary>Row in the match list.</summary>
public class MatchResponse {
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid TournamentId { get; set; }
    public string TournamentName { get; set; } = string.Empty;
    public Guid HomeTeamId { get; set; }
    public Guid AwayTeamId { get; set; }
    public string HomeTeamName { get; set; } = string.Empty;
    public string AwayTeamName { get; set; } = string.Empty;
    public string HomeShortName { get; set; } = string.Empty;
    public string AwayShortName { get; set; } = string.Empty;
    public Guid VenueId { get; set; }
    public string VenueName { get; set; } = string.Empty;
    public Guid MatchRulesId { get; set; }
    public Guid? OverlayThemeId { get; set; }
    public string MatchFormat { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? ScheduledStart { get; set; }
    public Guid? TossWinnerTeamId { get; set; }
    public string? TossDecision { get; set; }
    public string? ResultText { get; set; }
    public int SquadSize { get; set; }
}
