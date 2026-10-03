namespace CricketStream.Contracts.Master;

public class VenueDto {
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string Timezone { get; set; } = "UTC";
    public string? LogoUrl { get; set; }
    public string? GroundImageUrl { get; set; }
}
public class TournamentDto {
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string Season { get; set; } = string.Empty;
    public string Format { get; set; } = "T20";
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public string? LogoUrl { get; set; }
    public string Status { get; set; } = "Upcoming";
    public string? Description { get; set; }
    public Guid? DefaultMatchRulesId { get; set; }
    public Guid? OverlayThemeId { get; set; }
}
public class MatchRulesDto {
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsTest { get; set; }
    public int BallsPerOver { get; set; } = 6;
    public int OversPerInnings { get; set; } = 20;
    public int MaxOversPerBowler { get; set; } = 4;
    public int PowerplayOvers { get; set; } = 6;
    public int WideRuns { get; set; } = 1;
    public int NoBallRuns { get; set; } = 1;
    public bool FreeHitEnabled { get; set; } = true;
    public bool FollowOnEnabled { get; set; }
    public int InningsPerSide { get; set; } = 1;
    public int Days { get; set; } = 1;
    public int OversPerDay { get; set; }
    public int SessionsPerDay { get; set; } = 1;
    public int NewBallAfterOvers { get; set; }
    public int FollowOnMargin { get; set; } = 200;
}
public class OverlayThemeDto {
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PrimaryColor { get; set; } = "#0f6b4f";
    public string SecondaryColor { get; set; } = "#7a1fa2";
    public string AccentColor { get; set; } = "#ffd400";
    public string? BackgroundUrl { get; set; }
    public string? LogoUrl { get; set; }
    public string? WatermarkUrl { get; set; }
    public string? WatermarkText { get; set; }
    public bool UseTeamColors { get; set; }
}
