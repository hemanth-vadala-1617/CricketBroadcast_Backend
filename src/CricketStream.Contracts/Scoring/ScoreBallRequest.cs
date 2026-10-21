namespace CricketStream.Contracts.Scoring;

public class WicketRequest {
    public string DismissalType { get; set; } = string.Empty;
    public Guid? DismissedPlayerId { get; set; }   // null = the striker (run-outs may name the non-striker)
    public Guid? FielderId { get; set; }
    public Guid? NewBatterId { get; set; }         // may be sent later via /new-batter
}

/// <summary>Who is on strike / bowling comes from the innings, never from the scorer.</summary>
public class ScoreBallRequest {
    public Guid ClientRequestId { get; set; }       // idempotency key
    public int RunsBat { get; set; }
    public int ExtraRuns { get; set; }              // runs run on a wide, or the byes/leg-byes
    public string ExtrasType { get; set; } = "None"; // None, Wide, NoBall, Bye, LegBye
    public string SecondaryExtrasType { get; set; } = "None"; // Bye | LegBye with NoBall
    public bool IsBoundary { get; set; }
    public WicketRequest? Wicket { get; set; }
}
public class PowerSurgeRequest { public bool Start { get; set; } = true; }   // false cancels a surge that has not had a ball bowled yet
public class SixDistanceRequest { public int? Meters { get; set; } }   // null clears it
public class NewBatterRequest { public Guid PlayerId { get; set; } }
public class NewBowlerRequest { public Guid PlayerId { get; set; } public bool ReplaceMidOver { get; set; } }
public class RetireRequest { public Guid PlayerId { get; set; } public Guid? NewBatterId { get; set; } public string? Reason { get; set; } }
public class PenaltyRequest { public int Runs { get; set; } public string? Reason { get; set; } }
