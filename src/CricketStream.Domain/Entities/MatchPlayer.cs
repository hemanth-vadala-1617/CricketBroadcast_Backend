using CricketStream.Domain.Common;
namespace CricketStream.Domain.Entities;
public class MatchPlayer : BaseEntity {
    public Guid MatchId { get; set; }
    public Guid TeamId { get; set; }
    public Guid PlayerId { get; set; }
    public bool IsPlayingXI { get; set; } = true;
    public bool IsSubstitute { get; set; }
    public bool IsCaptain { get; set; }
    public bool IsWicketKeeper { get; set; }
    public int BattingOrder { get; set; }

    public Match Match { get; set; } = null!;
    public Team Team { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
