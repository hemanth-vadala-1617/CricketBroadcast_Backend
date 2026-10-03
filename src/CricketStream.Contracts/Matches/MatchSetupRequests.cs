namespace CricketStream.Contracts.Matches;

public class SquadPlayerRequest {
    public Guid PlayerId { get; set; }
    public bool IsPlayingXI { get; set; } = true;
    public bool IsCaptain { get; set; }
    public bool IsWicketKeeper { get; set; }
    public int BattingOrder { get; set; }
}
public class SetSquadRequest {
    public Guid TeamId { get; set; }
    public List<SquadPlayerRequest> Players { get; set; } = new();
}
public class SquadDto {
    public Guid TeamId { get; set; }
    public List<SquadMemberDto> Players { get; set; } = new();
}
public class SquadMemberDto {
    public Guid PlayerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public bool IsPlayingXI { get; set; }
    public bool IsCaptain { get; set; }
    public bool IsWicketKeeper { get; set; }
    public int BattingOrder { get; set; }
}
public class StartInningsRequest {
    public Guid? BattingTeamId { get; set; }   // null = derive (toss for innings 1, else alternate)
    public Guid StrikerId { get; set; }
    public Guid NonStrikerId { get; set; }
    public Guid BowlerId { get; set; }
}
public class EndInningsRequest { public bool Declared { get; set; } public string? Reason { get; set; } }
public class CompleteMatchRequest {
    public string ResultType { get; set; } = "Win";      // Win, Tie, Draw, NoResult, Abandoned
    public Guid? WinnerTeamId { get; set; }
    public string? ResultText { get; set; }
}
public class WinProbabilityRequest {
    public int Home { get; set; }
    public int Draw { get; set; }
    public int Away { get; set; }
}
public class OversLostRequest { public int Overs { get; set; } }
