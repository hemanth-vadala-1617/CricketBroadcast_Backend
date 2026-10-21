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

    /// <summary>The stoppage in progress (strategic timeout, drinks break, lunch...), or null while play is on.</summary>
    public BreakDto? Break { get; set; }

    /// <summary>Milestones reached ON THE LAST BALL (a fifty, a hundred, a maiden over, a five-wicket haul). Each has an id that is new
    /// for every occurrence, so the overlay shows each once. Empty most of the time.</summary>
    public List<MilestoneDto> Milestones { get; set; } = new();

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
    public PowerSurgeDto? PowerSurge { get; set; }   // null when the rules have no Power Surge
    public bool NewBallDue { get; set; }
    public bool IsSuperOver { get; set; }
    public bool IsDls { get; set; }                  // overs and/or target were revised (rain, DLS)
    public int OversLimit { get; set; }              // overs this innings is limited to (0 = unlimited)
    public int DeadBalls { get; set; }               // dead balls called in this innings; a change flashes DEAD BALL on the overlay
    public BatterDto? Striker { get; set; }
    public BatterDto? NonStriker { get; set; }
    public BowlerDto? Bowler { get; set; }
}

public class PartnershipDto { public int Runs { get; set; } public int Balls { get; set; } }

public class PowerSurgeDto {
    public bool Active { get; set; }           // the surge overs are being bowled now
    public bool Used { get; set; }             // it has been taken (active or finished)
    public bool CanStart { get; set; }         // between overs, in the allowed window, not taken yet
    public bool CanCancel { get; set; }        // just started (or still waiting for the next over), nothing bowled in it yet
    public bool Pending { get; set; }          // started in the middle of an over: it begins with the next over
    public int EarliestOver { get; set; }      // the window it may be started in
    public int LatestOver { get; set; }
    public int NextStartOver { get; set; }     // the over a surge started right now would begin at
    public int StartOver { get; set; }         // the over it began at (0 = not taken)
    public int Overs { get; set; }             // length of the block
    public int OversLeft { get; set; }         // overs of it still to bowl while active
}

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

public class BreakDto {
    public string Kind { get; set; } = string.Empty;     // StrategicTimeout, DrinksBreak, InjuryBreak, RainDelay, BadLight, WetOutfield, Lunch, Tea, Stumps, Other
    public string Label { get; set; } = string.Empty;    // "Strategic timeout"
    public int PlannedSeconds { get; set; }              // 0 = open-ended
    public int ElapsedSeconds { get; set; }              // how long it has lasted when this state was built (clients add the time since they received it)
}

public class MilestoneDto {
    public string Id { get; set; } = string.Empty;       // ballId + kind + value: unique per occurrence
    public string Kind { get; set; } = string.Empty;     // Runs (batter reached Value), Maiden, FourWickets, FiveWickets
    public int Value { get; set; }                       // Runs: 50, 100, 150...; FiveWickets: wickets taken; Maiden: maidens so far
    public Guid PlayerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public string Detail { get; set; } = string.Empty;   // "104 (61) 9x4 4x6" / "1-0-6-0"  / "5-24 (3.4)"
}

public class LastBallDto {
    public Guid BallId { get; set; }
    public string Label { get; set; } = string.Empty; // "0", "4", "W", "WD", "NB+2"
    public string Kind { get; set; } = string.Empty;  // Dot, Runs, Four, Six, Wicket, Wide, NoBall, Bye, LegBye
    public int Runs { get; set; }
    public int? SixDistance { get; set; }            // metres the six travelled, when the scorer has entered it
    public int SixStreak { get; set; }
    public string? DuckType { get; set; }            // when the batter dismissed on this ball has no runs: Duck, Golden (out to the first ball he faced) or Diamond (out without facing a ball)
    public string? DuckName { get; set; }            // that batter's name
    public int FourStreak { get; set; }              // fours in a row by this batter ending with this ball (0 unless it was a four)
    public int WicketStreak { get; set; }            // wickets in a row by this bowler ending with this ball (3 = hat-trick; 0 unless it was a bowler's wicket)               // sixes in a row by this batter ending with this ball (0 unless it was a six)
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
    public int FollowOnLead { get; set; }                  // first-innings runs minus second-innings runs (what the follow-on rule looks at)
    public bool CanStartSuperOver { get; set; }            // a limited-overs match that ended tied
    public bool CanReviseTarget { get; set; }              // a limited-overs innings is under way: rain-reduced overs / DLS target can be entered
    public bool CanComplete { get; set; }
    public List<Guid> RetiredHurtIds { get; set; } = new();   // batters who retired hurt in this innings and may come back
    public List<Guid> AbsentHurtIds { get; set; } = new();    // batting-side players who cannot bat in this innings
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
    public List<string> AbsentHurt { get; set; } = new();
    public bool IsSuperOver { get; set; }
    public bool IsFollowOn { get; set; }
    public bool IsDls { get; set; }
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