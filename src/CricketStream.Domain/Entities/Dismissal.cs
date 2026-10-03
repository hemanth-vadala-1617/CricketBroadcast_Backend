using CricketStream.Domain.Common;
using CricketStream.Domain.Enums;
namespace CricketStream.Domain.Entities;
public class Dismissal : BaseEntity {
    public Guid BallId { get; set; }
    public Guid DismissedPlayerId { get; set; }
    public Guid? FielderId { get; set; }
    public Guid? NewBatterId { get; set; }
    public DismissalType DismissalType { get; set; }

    public Ball Ball { get; set; } = null!;
    public Player DismissedPlayer { get; set; } = null!;
    public Player? Fielder { get; set; }
}
