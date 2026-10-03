using System.Text.Json;
using CricketStream.Application.Common;
using CricketStream.Application.Interfaces;
using CricketStream.Contracts.Scoring;
using CricketStream.Domain.Entities;
using CricketStream.Domain.Enums;
using CricketStream.Domain.Rules;

namespace CricketStream.Application.Features.Scoring;

/// <summary>Applies one delivery (or undoes one) to the tracked entities. Never saves; the caller commits.</summary>
public static class BallEngine
{
    public static Error? Apply(InningsWork w, ScoreBallRequest r, string userId, ICricketDbContext db)
    {
        var m = w.Match; var inn = w.Innings; var rules = w.Rules;

        if (m.Status != MatchStatus.Live) return Fail.Unprocessable($"Match is {m.Status}; scoring is only possible while it is Live.");
        if (inn.Status != InningsStatus.InProgress) return Fail.Unprocessable("The innings is over.");
        if (inn.CurrentStrikerId is not { } striker || inn.CurrentNonStrikerId is not { } nonStriker)
            return Fail.Unprocessable("Select the incoming batter before scoring the next ball.");
        if (inn.CurrentBowlerId is not { } bowler)
            return Fail.Unprocessable("Select the next bowler before scoring the next ball.");

        if (!Enum.TryParse<ExtrasType>(r.ExtrasType, true, out var primary)) return Fail.Validation($"Unknown extras type '{r.ExtrasType}'.");
        if (!Enum.TryParse<ExtrasType>(r.SecondaryExtrasType, true, out var secondary)) return Fail.Validation($"Unknown secondary extras type '{r.SecondaryExtrasType}'.");

        DismissalType? dismissal = null;
        if (r.Wicket is not null)
        {
            if (!Enum.TryParse<DismissalType>(r.Wicket.DismissalType, true, out var dt)) return Fail.Validation($"Unknown dismissal type '{r.Wicket.DismissalType}'.");
            dismissal = dt;
        }

        var freeHit = inn.FreeHitPending && rules.FreeHitEnabled;
        var input = new DeliveryInput(r.RunsBat, r.ExtraRuns, primary, secondary, r.IsBoundary, r.Wicket is not null, dismissal, freeHit);
        var invalid = CricketRules.Validate(input);
        if (invalid is not null) return Fail.Validation(invalid);

        // ---- wicket details ----
        Guid? dismissedId = null; Guid? fielderId = null; Guid? newBatter = null;
        if (r.Wicket is not null)
        {
            var t = dismissal!.Value;
            dismissedId = r.Wicket.DismissedPlayerId ?? striker;
            if (dismissedId != striker && dismissedId != nonStriker) return Fail.Validation("The dismissed batter must be at the crease.");
            if (dismissedId == nonStriker && t is not (DismissalType.RunOut or DismissalType.ObstructingField))
                return Fail.Validation($"The non-striker can only be dismissed run out or obstructing the field, not {t}.");
            if (t is DismissalType.Caught or DismissalType.Stumped && r.Wicket.FielderId is null)
                return Fail.Validation($"{t} needs the fielder/wicket-keeper.");
            fielderId = r.Wicket.FielderId;
            if (fielderId is { } f && !w.IsFielder(f)) return Fail.Validation("The fielder must belong to the bowling team.");
            newBatter = r.Wicket.NewBatterId;
            if (newBatter is { } nb)
            {
                var err = ValidateIncoming(w, nb, extraAtCrease: null, dismissedNow: dismissedId.Value);
                if (err is not null) return err;
            }
        }

        var outcome = CricketRules.Evaluate(input, rules);

        // ---- over ----
        // The open over is the LOWEST-numbered incomplete one: after an undo reopens over N, an empty over N+1
        // may still exist and must not steal the ball.
        var over = w.Overs.FirstOrDefault(o => !o.IsCompleted);
        if (over is null)
        {
            over = new Over { InningsId = inn.Id, BowlerId = bowler, OverNumber = (w.Overs.Count == 0 ? 0 : w.Overs.Max(o => o.OverNumber)) + 1 };
            db.Overs.Add(over);
            w.Overs.Add(over);
        }
        else if (w.LiveBallsInOver(over) == 0)
        {
            over.BowlerId = bowler; // first ball of an over that was emptied by an undo
        }

        // ---- ball ----
        var ball = new Ball
        {
            OverId = over.Id, InningsId = inn.Id, Sequence = (w.Balls.Count == 0 ? 0 : w.Balls.Max(b => b.Sequence)) + 1,
            StrikerId = striker, NonStrikerId = nonStriker, BowlerId = bowler,
            RunsBat = outcome.RunsBat, RunsExtra = outcome.ExtrasTotal, BowlerRuns = outcome.BowlerRuns,
            RunsCompleted = outcome.RunsCompleted, ExtrasType = primary, SecondaryExtrasType = secondary,
            IsBoundary = outcome.IsBoundary, IsFour = outcome.IsFour, IsSix = outcome.IsSix, IsLegal = outcome.IsLegal,
            IsFreeHit = freeHit, IsWicket = r.Wicket is not null, Day = m.CurrentDay, Session = m.CurrentSession,
            ScoredBy = userId, ClientRequestId = r.ClientRequestId,
        };
        if (r.Wicket is not null)
            ball.Dismissal = new Dismissal
            {
                BallId = ball.Id, DismissedPlayerId = dismissedId!.Value, FielderId = fielderId,
                NewBatterId = newBatter, DismissalType = dismissal!.Value,
            };
        db.Balls.Add(ball);
        w.Balls.Add(ball);

        // ---- strike & crease ----
        Guid? s = striker, n = nonStriker;
        var runsForSwap = outcome.RunsCompleted;
        if (dismissal is { } dd && CricketRules.IgnoresCrossing(dd)) runsForSwap = 0;
        if (CricketRules.SwapsStrike(runsForSwap)) (s, n) = (n, s);
        if (dismissedId is { } gone)
        {
            if (s == gone) s = newBatter; else n = newBatter;
        }

        var endOfOver = false;
        if (outcome.IsLegal && w.Live.Count(b => b.OverId == over.Id && b.IsLegal) >= rules.BallsPerOver)
        {
            over.IsCompleted = true;
            endOfOver = true;
            (s, n) = (n, s);
            inn.LastBowlerId = bowler;
            inn.CurrentBowlerId = null;
        }

        inn.CurrentStrikerId = s;
        inn.CurrentNonStrikerId = n;
        if (primary == ExtrasType.NoBall && rules.FreeHitEnabled) inn.FreeHitPending = true;
        else if (outcome.IsLegal) inn.FreeHitPending = false;

        w.RecomputeTotals();

        // ---- innings end? ----
        var xi = w.BattingXi().Count;
        var allOut = r.Wicket is not null && inn.TotalWickets >= xi - 1;
        var oversDone = endOfOver && CricketRules.OversComplete(inn.LegalBalls, rules);
        var targetDone = inn.Target is { } target && inn.TotalRuns >= target;
        if (allOut || oversDone || targetDone) MatchCompletion.EndInnings(w);
        return null;
    }

    /// <summary>Checks that a player may walk in: in the batting XI, not out, not already at the crease.</summary>
    public static Error? ValidateIncoming(InningsWork w, Guid playerId, Guid? extraAtCrease, Guid? dismissedNow)
    {
        if (!w.BattingXi().Any(p => p.PlayerId == playerId)) return Fail.Validation("The incoming batter must be in the batting XI.");
        if (w.Dismissed().Contains(playerId) || playerId == dismissedNow) return Fail.Validation("That batter is already out.");
        var inn = w.Innings;
        if (playerId == inn.CurrentStrikerId || playerId == inn.CurrentNonStrikerId || playerId == extraAtCrease)
            return Fail.Validation("That batter is already at the crease.");
        return null;
    }

    /// <summary>Voids the last live ball and rewinds the crease to exactly how it was before that ball.</summary>
    public static Error? VoidLast(InningsWork w, string userId, string reason, out string? previousJson)
    {
        previousJson = null;
        var ball = w.Live.OrderBy(b => b.Sequence).LastOrDefault();
        if (ball is null) return Fail.Unprocessable("There is no ball to undo in this innings.");

        previousJson = JsonSerializer.Serialize(Snapshot(ball));
        ball.IsVoided = true;
        ball.VoidedReason = reason;
        ball.VoidedBy = userId;
        ball.UpdatedAt = DateTimeOffset.UtcNow;

        var inn = w.Innings;
        inn.CurrentStrikerId = ball.StrikerId;
        inn.CurrentNonStrikerId = ball.NonStrikerId;
        inn.CurrentBowlerId = ball.BowlerId;
        inn.FreeHitPending = ball.IsFreeHit;

        var over = w.Overs.First(o => o.Id == ball.OverId);
        over.IsCompleted = false;
        var prev = w.Overs.LastOrDefault(o => o.OverNumber < over.OverNumber);
        inn.LastBowlerId = prev is null ? null
            : w.Live.Where(b => b.OverId == prev.Id).OrderBy(b => b.Sequence).LastOrDefault()?.BowlerId ?? prev.BowlerId;

        var m = w.Match;
        if (inn.Status == InningsStatus.Completed) inn.Status = InningsStatus.InProgress;
        if (m.Status is MatchStatus.Completed or MatchStatus.InningsBreak)
        {
            m.Status = MatchStatus.Live;
            m.ResultType = MatchResultType.None; m.ResultText = null; m.WinnerTeamId = null; m.ActualEnd = null;
        }
        w.RecomputeTotals();
        return null;
    }

    public static object Snapshot(Ball b) => new
    {
        b.Sequence, b.RunsBat, b.RunsExtra, b.BowlerRuns, extrasType = b.ExtrasType.ToString(),
        secondary = b.SecondaryExtrasType.ToString(), b.IsBoundary, b.IsLegal, b.IsWicket,
        dismissal = b.Dismissal?.DismissalType.ToString(), b.StrikerId, b.NonStrikerId, b.BowlerId,
    };
}

