using CricketStream.Domain.Common;
namespace CricketStream.Domain.Entities;
public class Team : BaseEntity {
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<Player> Players { get; set; } = new List<Player>();
}
