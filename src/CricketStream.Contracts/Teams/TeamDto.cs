namespace CricketStream.Contracts.Teams;
public class TeamDto {
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public bool IsActive { get; set; }
    public int PlayerCount { get; set; }
    public List<Guid> TournamentIds { get; set; } = new();   // the competitions (IPL, BBL, World Cup...) this team belongs to
}
