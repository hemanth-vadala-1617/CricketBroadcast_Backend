using CricketStream.Domain.Common;
namespace CricketStream.Domain.Entities;
/// <summary>Persisted on-air state of one overlay graphic so a refreshed OBS page restores it.</summary>
public class OverlayGraphicState : BaseEntity {
    public Guid MatchId { get; set; }
    public string GraphicKey { get; set; } = string.Empty;
    public bool IsVisible { get; set; }
    public string? PayloadJson { get; set; }
    public Match Match { get; set; } = null!;
}
