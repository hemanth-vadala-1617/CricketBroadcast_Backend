using CricketStream.Domain.Common;
namespace CricketStream.Domain.Entities;
/// <summary>Support staff named for one team in one match (coach, physio, analyst...). Free text: staff are not master data.</summary>
public class MatchStaff : BaseEntity {
    public Guid MatchId { get; set; }
    public Guid TeamId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public Match Match { get; set; } = null!;
    public Team Team { get; set; } = null!;
}
