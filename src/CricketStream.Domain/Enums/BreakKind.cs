namespace CricketStream.Domain.Enums;
/// <summary>Why play is stopped. Rain delay keeps its own match status; every other kind is "Paused" with a kind.</summary>
public enum BreakKind { StrategicTimeout, DrinksBreak, InjuryBreak, RainDelay, BadLight, WetOutfield, Lunch, Tea, Stumps, Other }

public static class BreakKinds {
    public static string Label(BreakKind k) => k switch {
        BreakKind.StrategicTimeout => "Strategic timeout", BreakKind.DrinksBreak => "Drinks break", BreakKind.InjuryBreak => "Injury break",
        BreakKind.RainDelay => "Rain delay", BreakKind.BadLight => "Bad light", BreakKind.WetOutfield => "Wet outfield",
        BreakKind.Lunch => "Lunch", BreakKind.Tea => "Tea", BreakKind.Stumps => "Stumps", _ => "Play paused",
    };
    /// <summary>Lunch, tea and stumps are Test-match breaks; the strategic timeout belongs to the shorter formats.</summary>
    public static bool TestOnly(BreakKind k) => k is BreakKind.Lunch or BreakKind.Tea or BreakKind.Stumps;
    public static bool LimitedOversOnly(BreakKind k) => k == BreakKind.StrategicTimeout;
}
