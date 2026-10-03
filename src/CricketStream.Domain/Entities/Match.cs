using CricketStream.Domain.Common;
using CricketStream.Domain.Enums;
namespace CricketStream.Domain.Entities;
public class Match : BaseEntity {
    public Guid TournamentId { get; set; }
    public Guid HomeTeamId { get; set; }
    public Guid AwayTeamId { get; set; }
    public Guid VenueId { get; set; }
    public Guid MatchRulesId { get; set; }
    public Guid? OverlayThemeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public MatchFormat MatchFormat { get; set; }
    public MatchStatus Status { get; set; } = MatchStatus.Scheduled;
    public DateTimeOffset? ScheduledStart { get; set; }
    public DateTimeOffset? ActualStart { get; set; }
    public DateTimeOffset? ActualEnd { get; set; }
    public Guid? TossWinnerTeamId { get; set; }
    public TossDecision? TossDecision { get; set; }
    public Guid? WinnerTeamId { get; set; }
    public MatchResultType ResultType { get; set; } = MatchResultType.None;
    public string? ResultText { get; set; }
    public Guid? CurrentInningsId { get; set; }

    // Test-match clock (Day 3 : Session 1, overs left today)
    public int CurrentDay { get; set; } = 1;
    public int CurrentSession { get; set; } = 1;
    public int OversLostToday { get; set; }

    // Manual win probability (percent, should add to 100)
    public int WinProbHome { get; set; }
    public int WinProbDraw { get; set; }
    public int WinProbAway { get; set; }

    /// <summary>Bumped on every state change; also the optimistic-concurrency token.
    /// Clients ignore messages with an older version.</summary>
    public long Version { get; set; }

    public Tournament Tournament { get; set; } = null!;
    public Team HomeTeam { get; set; } = null!;
    public Team AwayTeam { get; set; } = null!;
    public Venue Venue { get; set; } = null!;
    public MatchRules MatchRules { get; set; } = null!;
    public OverlayTheme? OverlayTheme { get; set; }
    public Team? TossWinnerTeam { get; set; }
    public Team? WinnerTeam { get; set; }

    public ICollection<MatchPlayer> MatchPlayers { get; set; } = new List<MatchPlayer>();
    public ICollection<MatchStaff> MatchStaff { get; set; } = new List<MatchStaff>();
    public ICollection<Innings> Innings { get; set; } = new List<Innings>();
}
