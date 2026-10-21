using CricketStream.Domain.Common;
namespace CricketStream.Domain.Entities;
/// <summary>A team belonging to a tournament / competition (IPL, BBL, World Cup...). A team can be in several; it is how the Teams page groups them into folders.</summary>
public class TournamentTeam : BaseEntity {
    public Guid TournamentId { get; set; }
    public Guid TeamId { get; set; }
    public Tournament Tournament { get; set; } = null!;
    public Team Team { get; set; } = null!;
}
