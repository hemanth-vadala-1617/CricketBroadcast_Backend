using CricketStream.Domain.Common;
using CricketStream.Domain.Enums;
namespace CricketStream.Domain.Entities;
/// <summary>A player is not owned by one team forever (IPL trades, World Cup squads).
/// CurrentTeamId is only the default team; the real per-match team is MatchPlayer.TeamId.</summary>
public class Player : BaseEntity {
    public Guid? CurrentTeamId { get; set; }
    public Team? CurrentTeam { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public int JerseyNumber { get; set; }
    public string BattingStyle { get; set; } = string.Empty;
    public string BowlingStyle { get; set; } = string.Empty;
    public PlayerRole PlayerRole { get; set; }
    public bool IsActive { get; set; } = true;
}
