using CricketStream.Domain.Common;
namespace CricketStream.Domain.Entities;
public class OverlayTheme : BaseEntity {
    public string Name { get; set; } = string.Empty;
    public string PrimaryColor { get; set; } = "#0f6b4f";
    public string SecondaryColor { get; set; } = "#7a1fa2";
    public string AccentColor { get; set; } = "#ffd400";
    public string? BackgroundUrl { get; set; }
    public string? LogoUrl { get; set; }
    public string? WatermarkUrl { get; set; }
    public string? WatermarkText { get; set; }
    // false: both score bars use PrimaryColor (the theme decides). true: each bar wears its own team colour, like the TV graphic.
    public bool UseTeamColors { get; set; }
}
