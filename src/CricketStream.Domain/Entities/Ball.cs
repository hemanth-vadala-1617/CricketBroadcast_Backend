using CricketStream.Domain.Common;
using CricketStream.Domain.Enums;
namespace CricketStream.Domain.Entities;
/// <summary>One delivery. Immutable once written, except IsVoided (undo). StrikerId/NonStrikerId/BowlerId
/// are the positions BEFORE the ball, which is what makes undo exact.</summary>
public class Ball : BaseEntity {
    public Guid OverId { get; set; }
    public Guid InningsId { get; set; }
    public int Sequence { get; set; }            // unique per innings, voided balls keep their slot
    public Guid StrikerId { get; set; }
    public Guid NonStrikerId { get; set; }
    public Guid BowlerId { get; set; }

    public int RunsBat { get; set; }
    public int RunsExtra { get; set; }           // total extras on this ball (incl. wide/no-ball penalty)
    public int BowlerRuns { get; set; }          // runs charged to the bowler
    public int RunsCompleted { get; set; }       // runs physically run (drives strike rotation)
    public ExtrasType ExtrasType { get; set; }           // None/Wide/NoBall/Bye/LegBye
    public ExtrasType SecondaryExtrasType { get; set; }  // Bye/LegBye on a no-ball, else None
    public bool IsBoundary { get; set; }
    public bool IsFour { get; set; }
    public bool IsSix { get; set; }
    public bool IsLegal { get; set; }
    public bool IsFreeHit { get; set; }
    public bool IsWicket { get; set; }
    public int Day { get; set; } = 1;
    public int Session { get; set; } = 1;

    public bool IsVoided { get; set; }
    public string? VoidedReason { get; set; }
    public string? VoidedBy { get; set; }
    public string ScoredBy { get; set; } = string.Empty;
    public Guid ClientRequestId { get; set; }

    public Over Over { get; set; } = null!;
    public Innings Innings { get; set; } = null!;
    public Player Striker { get; set; } = null!;
    public Player NonStriker { get; set; } = null!;
    public Player Bowler { get; set; } = null!;
    public Dismissal? Dismissal { get; set; }
}
