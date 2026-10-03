namespace CricketStream.Contracts.Players;
public class PlayerDto {
    public Guid Id { get; set; }
    public Guid? CurrentTeamId { get; set; }
    public string? CurrentTeamName { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public int JerseyNumber { get; set; }
    public string BattingStyle { get; set; } = string.Empty;
    public string BowlingStyle { get; set; } = string.Empty;
    public string PlayerRole { get; set; } = "Batter";
    public bool IsActive { get; set; } = true;
}
