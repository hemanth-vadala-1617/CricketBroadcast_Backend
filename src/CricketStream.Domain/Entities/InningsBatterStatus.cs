using CricketStream.Domain.Common;
using CricketStream.Domain.Enums;
namespace CricketStream.Domain.Entities;

/// <summary>A batter who retired hurt, or who is absent hurt, in one innings (a player can be absent in one innings and bat in the next).
/// AfterBallSequence is the last live ball when it happened, so undoing that ball also undoes the retirement.
/// A retired-hurt batter is not out and may walk back in; an absent-hurt batter cannot bat and does not count towards "all out".</summary>
public class InningsBatterStatus : BaseEntity {
    public Guid InningsId { get; set; }
    public Guid PlayerId { get; set; }
    public BatterStatusKind Kind { get; set; }
    public int AfterBallSequence { get; set; }
    public string? Reason { get; set; }

    public Innings Innings { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
