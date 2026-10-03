using CricketStream.Domain.Common;
using CricketStream.Domain.Enums;
namespace CricketStream.Domain.Entities;
public class Tournament : BaseEntity {
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string Season { get; set; } = string.Empty;
    public MatchFormat Format { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public string? LogoUrl { get; set; }
    public string Status { get; set; } = "Upcoming";
    public string? Description { get; set; }
    public Guid? DefaultMatchRulesId { get; set; }
    public Guid? OverlayThemeId { get; set; }
}
