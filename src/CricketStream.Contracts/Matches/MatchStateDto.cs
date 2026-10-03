using System.Text.Json;
namespace CricketStream.Contracts.Matches;

/// <summary>The single payload pushed to every client (overlay, scorer, producer, public).
/// Clients render it verbatim and never compute cricket maths.</summary>
public class MatchStateDto {
    public Guid MatchId { get; set; }
    public long Version { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public bool IsTest { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ResultText { get; set; }
    public string? ResultType { get; set; }
    public string TournamentName { get; set; } = string.Empty;
    public string VenueName { get; set; } = string.Empty;
    public string? BackgroundUrl { get; set; }
    public ThemeDto Theme { get; set; } = new();
    public TeamLiteDto HomeTeam { get; set; } = new();
    public TeamLiteDto AwayTeam { get; set; } = new();
    public string? TossText { get; set; }
    public int BallsPerOver { get; set; } = 6;
    public int OversPerInnings { get; set; }

    public InningsStateDto? Innings { get; set; }
    public List<InningsSummaryDto> PreviousInnings { get; set; } = new();
    public List<InningsScorecardDto> Scorecard { get; set; } = new();

    /// <summary>Playing XI of both teams, for the pre-match player-intro graphic.</summary>
    public LineupsDto Lineups { get; set; } = new();

    public LastBallDto? LastBall { get; set; }

    /// <summary>The most recent penalty award (runs added without a ball). The overlay shows a burst for each new one.</summary>
    public LastPenaltyDto? LastPenalty { get; set; }
    public List<OverTimelineDto> Timeline { get; set; } = new();

    public TestClockDto? TestClock { get; set; }
    public WinProbabilityDto WinProbability { get; set; } = new();
    public Dictionary<string, GraphicVisibilityDto> Graphics { get; set; } = new();

    public MatchActionsDto Actions { get; set; } = new();
}

public class ThemeDto {
    public string PrimaryColor { get; set; } = "#0f6b4f";
    public string SecondaryColor { get; set; } = "#7a1fa2";
    public string AccentColor { get; set; } = "#ffd400";
    public string? LogoUrl { get; set; }
    public string? WatermarkUrl { get; set; }
    public string? WatermarkText { get; set; }
    public bool UseTeamColors { get; set; }
}

public class TeamLiteDto {
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
}

public class InningsStateDto {
    public Guid InningsId { get; set; }
    public int InningsNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsFollowOn { get; set; }
    public TeamLiteDto BattingTeam { get; set; } = new();
    public TeamLiteDto BowlingTeam { get; set; } = new();
    public int Runs { get; set; }
    public int Wickets { get; set; }
    public string Overs { get; set; } = "0.0";
    public int LegalBalls { get; set; }
    public decimal CurrentRunRate { get; set; }
    public int Extras { get; set; }
    public int? Target { get; set; }
    public int? RunsRequired { get; set; }
    public int? BallsRemaining { get; set; }
    public decimal? RequiredRunRate { get; set; }
    public decimal ProjectedScore { get; set; }
    public string? LeadText { get; set; }      // "BAN lead by 157 runs" / "need 24 runs from 18 balls"
    public int? LeadRuns { get; set; }          // positive = batting side leads (Test)
    public PartnershipDto Partnership { get; set; } = new();
    public bool FreeHit { get; set; }
    public bool Powerplay { get; set; }
    public bool NewBallDue { get; set; }
    public BatterDto? Striker { get; set; }
    public BatterDto? NonStriker { get; set; }
    public BowlerDto? Bowler { get; set; }
}

public class PartnershipDto { public int Runs { get; set; } public int Balls { get; set; } }

public class BatterDto {
    public Guid PlayerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public int Runs { get; set; }
    public int Balls { get; set; }
    public int Fours { get; set; }
    public int Sixes { get; set; }
    public decimal StrikeRate { get; set; }
    public bool OnStrike { get; set; }
    // Used by the "new batter" introduction card.
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int JerseyNumber { get; set; }
    public string BattingStyle { get; set; } = string.Empty;
    public BatterCareerDto? Career { get; set; }
}
public class BatterCareerDto {
    public string Label { get; set; } = string.Empty;
    public int? Matches { get; set; }
    public int? Runs { get; set; }
    public decimal? Average { get; set; }
    public decimal? StrikeRate { get; set; }
    public int? Fifties { get; set; }
    public int? Hundreds { get; set; }
}

public class BowlerDto {
    public Guid PlayerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public string Overs { get; set; } = "0.0";
    public int Maidens { get; set; }
    public int Runs { get; set; }
    public int Wickets { get; set; }
    public decimal Economy { get; set; }
}

public class InningsSummaryDto {
    public int InningsNumber { get; set; }
    public string TeamShortName { get; set; } = string.Empty;
    public int Runs { get; set; }
    public int Wickets { get; set; }
    public string Overs { get; set; } = "0.0";
    public bool IsDeclared { get; set; }
    public string Text { get; set; } = string.Empty;  // "1st Inng.: 198-10"
}

public class LastBallDto {
    public Guid BallId { get; set; }
    public string Label { get; set; } = string.Empty; // "0", "4", "W", "WD", "NB+2"
    public string Kind { get; set; } = string.Empty;  // Dot, Runs, Four, Six, Wicket, Wide, NoBall, Bye, LegBye
    public int Runs { get; set; }
}

public class OverTimelineDto {
    public int OverNumber { get; set; }
    public string BowlerName { get; set; } = string.Empty;
    public int Runs { get; set; }
    public int Wickets { get; set; }
    public int LegalBalls { get; set; }
    public bool IsCompleted { get; set; }
    public List<BallChipDto> Balls { get; set; } = new();
}

public class BallChipDto {
    public string Label { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
}

public class TestClockDto {
    public int Day { get; set; }
    public int Session { get; set; }
    public string OversLeftToday { get; set; } = "0.0";
    public int OversLostToday { get; set; }
}

public class WinProbabilityDto {
    public int Home { get; set; }
    public int Draw { get; set; }
    public int Away { get; set; }
}

public class GraphicVisibilityDto {
    public bool IsVisible { get; set; }
    public JsonElement? Payload { get; set; }
}

/// <summary>What the scorer UI is allowed/required to do next.</summary>
public class MatchActionsDto {
    public bool CanRecordToss { get; set; }
    public bool CanStartMatch { get; set; }
    public bool CanStartInnings { get; set; }
    public bool AwaitingNewBatter { get; set; }
    public bool AwaitingNewBowler { get; set; }
    public bool CanScoreBall { get; set; }
    public bool CanUndo { get; set; }
    public bool InningsOver { get; set; }
    public bool CanDeclare { get; set; }
    public bool FollowOnAvailable { get; set; }
    public bool CanComplete { get; set; }
    public Guid? NextBattingTeamId { get; set; }
    public List<PlayerLiteDto> AvailableBatters { get; set; } = new();
    public List<PlayerLiteDto> EligibleBowlers { get; set; } = new();
    public List<PlayerLiteDto> BattingXI { get; set; } = new();
    public List<PlayerLiteDto> FieldingXI { get; set; } = new();
}

public class PlayerLiteDto {
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public bool IsWicketKeeper { get; set; }
}

public class InningsScorecardDto {
    public int InningsNumber { get; set; }
    public TeamLiteDto BattingTeam { get; set; } = new();
    public int Runs { get; set; }
    public int Wickets { get; set; }
    public string Overs { get; set; } = "0.0";
    public int Extras { get; set; }
    public decimal RunRate { get; set; }
    public string ExtrasText { get; set; } = string.Empty; // "(b 4, lb 2, w 5, nb 1)"
    public bool IsCompleted { get; set; }
    public List<BatterLineDto> Batting { get; set; } = new();
    public List<BowlerLineDto> Bowling { get; set; } = new();
    public List<FallOfWicketDto> FallOfWickets { get; set; } = new();
    public List<string> YetToBat { get; set; } = new();
}

public class BatterLineDto {
    public Guid PlayerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsCaptain { get; set; }
    public bool IsWicketKeeper { get; set; }
    public int Runs { get; set; }
    public int Balls { get; set; }
    public int Fours { get; set; }
    public int Sixes { get; set; }
    public decimal StrikeRate { get; set; }
    public string Status { get; set; } = string.Empty;   // batting, not out, out, retired hurt
    public string DismissalText { get; set; } = string.Empty;
}

public class BowlerLineDto {
    public Guid PlayerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Overs { get; set; } = "0.0";
    public int Maidens { get; set; }
    public int Runs { get; set; }
    public int Wickets { get; set; }
    public int Wides { get; set; }
    public int NoBalls { get; set; }
    public decimal Economy { get; set; }
}

public class FallOfWicketDto {
    public int WicketNumber { get; set; }
    public int Score { get; set; }
    public string Overs { get; set; } = string.Empty;
    public string BatterName { get; set; } = string.Empty;
}

public class LineupsDto {
    public LineupDto Home { get; set; } = new();
    public LineupDto Away { get; set; } = new();
}

public class LineupDto {
    public TeamLiteDto Team { get; set; } = new();
    public List<LineupPlayerDto> Players { get; set; } = new();
}

public class LineupPlayerDto {
    public Guid PlayerId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public bool IsCaptain { get; set; }
    public bool IsWicketKeeper { get; set; }
    public int BattingOrder { get; set; }
}
public class LastPenaltyDto {
    public Guid Id { get; set; }     // changes with every penalty, so clients can tell a NEW one from one they have already shown
    public int Runs { get; set; }
}