using CricketStream.Domain.Common;
using CricketStream.Domain.Enums;
namespace CricketStream.Domain.Entities;
/// <summary>Aggregate columns (runs, wickets, extras, LegalBalls) are a cache of the ball log,
/// rewritten from the non-voided balls after every command. The positions/FreeHitPending are
/// the live crease state used to accept the next ball.</summary>
public class Innings : BaseEntity {
    public Guid MatchId { get; set; }
    public Guid BattingTeamId { get; set; }
    public Guid BowlingTeamId { get; set; }
    public int InningsNumber { get; set; }
    public InningsStatus Status { get; set; } = InningsStatus.InProgress;
    public bool IsFollowOn { get; set; }
    public bool IsDeclared { get; set; }
    public int? Target { get; set; }

    public int TotalRuns { get; set; }
    public int TotalWickets { get; set; }
    public int TotalExtras { get; set; }
    public int LegalBalls { get; set; }
    public int PenaltyRuns { get; set; }

    public Guid? CurrentStrikerId { get; set; }
    public Guid? CurrentNonStrikerId { get; set; }
    public Guid? CurrentBowlerId { get; set; }
    public Guid? LastBowlerId { get; set; }
    public bool FreeHitPending { get; set; }

    public Match Match { get; set; } = null!;
    public Team BattingTeam { get; set; } = null!;
    public Team BowlingTeam { get; set; } = null!;
    public ICollection<Over> Overs { get; set; } = new List<Over>();
}
