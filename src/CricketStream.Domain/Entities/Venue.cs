using CricketStream.Domain.Common;
namespace CricketStream.Domain.Entities;
public class Venue : BaseEntity {
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string Timezone { get; set; } = "UTC";
    public string? LogoUrl { get; set; }
    public string? GroundImageUrl { get; set; } // stadium background behind the overlay
}
