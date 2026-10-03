using CricketStream.Domain.Common;
namespace CricketStream.Domain.Entities;
public class Over : BaseEntity {
    public Guid InningsId { get; set; }
    public Guid BowlerId { get; set; }   // bowler who started the over
    public int OverNumber { get; set; }  // 1-based
    public bool IsCompleted { get; set; }

    public Innings Innings { get; set; } = null!;
    public Player Bowler { get; set; } = null!;
    public ICollection<Ball> Balls { get; set; } = new List<Ball>();
}
