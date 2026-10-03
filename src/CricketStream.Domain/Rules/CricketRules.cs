using CricketStream.Domain.Entities;
using CricketStream.Domain.Enums;

namespace CricketStream.Domain.Rules;

/// <summary>What the scorer typed for one delivery. Positions/bowler come from the innings, not the scorer.</summary>
public sealed record DeliveryInput(
    int RunsBat,
    int ExtraRuns,                       // runs run on top of the automatic wide/no-ball penalty, or the byes/leg-byes
    ExtrasType Primary,                  // None, Wide, NoBall, Bye, LegBye
    ExtrasType Secondary,                // Bye/LegBye with a NoBall, else None
    bool IsBoundary,
    bool IsWicket,
    DismissalType? Dismissal,
    bool IsFreeHit);

public sealed record DeliveryOutcome(
    bool IsLegal,
    int RunsBat,
    int ExtrasTotal,
    int BowlerRuns,
    int RunsCompleted,
    bool BatterFacesBall,
    bool IsFour,
    bool IsSix,
    bool IsBoundary);

/// <summary>Pure cricket rules. No EF, no HTTP, no clock.</summary>
public static class CricketRules
{
    public static bool CreditsBowler(DismissalType t) =>
        t is DismissalType.Bowled or DismissalType.Caught or DismissalType.LBW
          or DismissalType.Stumped or DismissalType.HitWicket;

    /// <summary>Dismissals where the batters do not cross for the strike calculation.</summary>
    public static bool IgnoresCrossing(DismissalType t) => CreditsBowler(t);

    public static bool AllowedOnFreeHit(DismissalType t) =>
        t is DismissalType.RunOut or DismissalType.HandledBall or DismissalType.ObstructingField;

    public static bool AllowedOnWide(DismissalType t) =>
        t is DismissalType.Stumped or DismissalType.RunOut or DismissalType.HitWicket
          or DismissalType.HandledBall or DismissalType.ObstructingField;

    public static bool AllowedOnNoBall(DismissalType t) =>
        t is DismissalType.RunOut or DismissalType.HandledBall or DismissalType.ObstructingField;

    /// <summary>Dismissals that can happen on a delivery (retired/timed-out are handled elsewhere).</summary>
    public static bool IsDeliveryDismissal(DismissalType t) =>
        t is not (DismissalType.RetiredHurt or DismissalType.TimedOut);

    public static string FormatOvers(int legalBalls, int ballsPerOver)
    {
        if (ballsPerOver <= 0) ballsPerOver = 6;
        return $"{legalBalls / ballsPerOver}.{legalBalls % ballsPerOver}";
    }

    public static decimal RunRate(int runs, int legalBalls, int ballsPerOver) =>
        legalBalls <= 0 ? 0m : Math.Round(runs * (decimal)ballsPerOver / legalBalls, 2);

    public static decimal StrikeRate(int runs, int balls) =>
        balls <= 0 ? 0m : Math.Round(runs * 100m / balls, 2);

    public static decimal Economy(int runs, int legalBalls, int ballsPerOver) =>
        RunRate(runs, legalBalls, ballsPerOver);

    /// <summary>Returns an error message, or null when the delivery is acceptable.</summary>
    public static string? Validate(DeliveryInput d)
    {
        if (d.RunsBat < 0 || d.ExtraRuns < 0) return "Runs cannot be negative.";
        if (d.RunsBat > 7 || d.ExtraRuns > 7) return "Runs for one delivery cannot exceed 7.";
        if (d.Primary is ExtrasType.Penalty) return "Penalty runs are added separately, not as a delivery.";
        if (d.Secondary is not (ExtrasType.None or ExtrasType.Bye or ExtrasType.LegBye))
            return "Secondary extras can only be Bye or LegBye.";
        if (d.Secondary != ExtrasType.None && d.Primary != ExtrasType.NoBall)
            return "Byes/leg-byes can only be combined with a no-ball.";
        if (d.Primary is ExtrasType.Wide or ExtrasType.Bye or ExtrasType.LegBye && d.RunsBat > 0)
            return "No runs can come off the bat on a wide, bye or leg-bye.";
        if (d.Primary is ExtrasType.None && d.ExtraRuns > 0)
            return "Extra runs need an extras type.";
        if (d.Primary is ExtrasType.NoBall && d.Secondary == ExtrasType.None && d.ExtraRuns > 0)
            return "Extra runs on a no-ball need Bye or LegBye.";
        if (d.IsBoundary)
        {
            var boundaryRuns = d.RunsBat > 0 ? d.RunsBat : d.ExtraRuns;
            if (boundaryRuns < 4) return "A boundary needs at least 4 runs.";
        }

        if (d.IsWicket)
        {
            if (d.Dismissal is null) return "A dismissal type is required for a wicket.";
            var t = d.Dismissal.Value;
            if (!IsDeliveryDismissal(t)) return $"{t} is recorded with the retire action, not on a ball.";
            if (d.IsFreeHit && !AllowedOnFreeHit(t)) return $"{t} is not allowed on a free hit.";
            if (d.Primary == ExtrasType.Wide && !AllowedOnWide(t)) return $"{t} is not allowed on a wide.";
            if (d.Primary == ExtrasType.NoBall && !AllowedOnNoBall(t)) return $"{t} is not allowed on a no-ball.";
            if (d.Primary is ExtrasType.Bye or ExtrasType.LegBye
                && t is DismissalType.Bowled or DismissalType.Caught or DismissalType.LBW)
                return $"{t} is not possible on a bye or leg-bye.";
        }
        return null;
    }

    /// <summary>Works out runs, legality and strike-relevant runs for one validated delivery.</summary>
    public static DeliveryOutcome Evaluate(DeliveryInput d, MatchRules rules)
    {
        var bat = d.RunsBat;
        var isSix = bat == 6;
        var boundary = d.IsBoundary || isSix;
        var isFour = boundary && bat == 4;

        switch (d.Primary)
        {
            case ExtrasType.Wide:
            {
                var extras = rules.WideRuns + d.ExtraRuns;
                return new DeliveryOutcome(false, 0, extras, extras, boundary ? 0 : d.ExtraRuns, false, false, false, boundary);
            }
            case ExtrasType.NoBall:
            {
                var byes = d.Secondary != ExtrasType.None ? d.ExtraRuns : 0;
                var extras = rules.NoBallRuns + byes;
                var completed = boundary ? 0 : bat + byes;
                return new DeliveryOutcome(false, bat, extras, bat + rules.NoBallRuns, completed, true, isFour, isSix, boundary);
            }
            case ExtrasType.Bye:
            case ExtrasType.LegBye:
                return new DeliveryOutcome(true, 0, d.ExtraRuns, 0, boundary ? 0 : d.ExtraRuns, true, false, false, boundary);
            default:
                return new DeliveryOutcome(true, bat, 0, bat, boundary ? 0 : bat, true, isFour, isSix, boundary);
        }
    }

    /// <summary>Strike swaps on an odd number of completed runs.</summary>
    public static bool SwapsStrike(int runsCompleted) => runsCompleted % 2 == 1;

    public static bool OversComplete(int legalBalls, MatchRules rules) =>
        rules.OversPerInnings > 0 && legalBalls >= rules.OversPerInnings * rules.BallsPerOver;

    public static bool IsPowerplay(int completedOvers, MatchRules rules) =>
        rules.PowerplayOvers > 0 && completedOvers < rules.PowerplayOvers;

    /// <summary>"Overs left today" for Test matches, in balls.</summary>
    public static int BallsLeftToday(int ballsBowledToday, int oversLostToday, MatchRules rules)
    {
        if (rules.OversPerDay <= 0) return 0;
        var left = (rules.OversPerDay - oversLostToday) * rules.BallsPerOver - ballsBowledToday;
        return Math.Max(0, left);
    }

    public static bool BowlerHasQuotaLeft(int bowlerLegalBalls, MatchRules rules) =>
        rules.MaxOversPerBowler <= 0 || bowlerLegalBalls < rules.MaxOversPerBowler * rules.BallsPerOver;

    /// <summary>Target for the innings being played, or null when none applies yet.
    /// Limited overs: first-innings runs + 1 in the second innings.
    /// Test: only when the batting side is on its second innings and the opposition has finished two.</summary>
    public static int? ComputeTarget(
        MatchRules rules, int inningsNumber, int battingTeamPriorRuns, int battingTeamInningsPlayedBefore,
        int oppositionRuns, int oppositionInningsCompleted, int firstInningsRuns)
    {
        if (!rules.IsTest)
            return inningsNumber == 2 ? firstInningsRuns + 1 : null;
        if (battingTeamInningsPlayedBefore >= 1 && oppositionInningsCompleted >= 2)
            return oppositionRuns - battingTeamPriorRuns + 1;
        return null;
    }

    public static bool FollowOnAvailable(MatchRules rules, int firstInningsRuns, int secondInningsRuns) =>
        rules.IsTest && rules.FollowOnEnabled && firstInningsRuns - secondInningsRuns >= rules.FollowOnMargin;
}
