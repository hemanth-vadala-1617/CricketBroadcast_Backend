using System.Text.Json;
using CricketStream.Application.Common;
using CricketStream.Application.Features.Matches;
using CricketStream.Application.Interfaces;
using CricketStream.Contracts.Matches;
using CricketStream.Contracts.Scoring;
using CricketStream.Domain.Entities;
using CricketStream.Domain.Enums;
using CricketStream.Domain.Rules;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CricketStream.Application.Features.Scoring;

public class ScoringService : IScoringService {
    private readonly ICricketDbContext _db;
    private readonly MatchCommitter _committer;
    private readonly IMatchStateService _state;
    private readonly IValidator<ScoreBallRequest> _validator;

    public ScoringService(ICricketDbContext db, MatchCommitter committer, IMatchStateService state, IValidator<ScoreBallRequest> validator) {
        _db = db; _committer = committer; _state = state; _validator = validator;
    }

    public async Task<Result<MatchStateDto>> ScoreBallAsync(Guid matchId, ScoreBallRequest request, string userId) {
        var bad = await _validator.FirstErrorAsync(request);
        if (bad is not null) return Result<MatchStateDto>.Fail(Fail.Validation(bad));

        // Idempotency: a retried/double-clicked request returns the current state instead of a second ball.
        if (await _db.Balls.AnyAsync(b => b.ClientRequestId == request.ClientRequestId)) {
            var current = await _state.GetAsync(matchId);
            return current is null ? Result<MatchStateDto>.Fail(Fail.NotFound("Match not found.")) : Result<MatchStateDto>.Ok(current);
        }

        var load = await InningsWork.LoadAsync(_db, matchId);
        if (!load.Succeeded) return Result<MatchStateDto>.Fail(load.Error!);
        var w = load.Value!;
        var err = BallEngine.Apply(w, request, userId, _db);
        if (err is not null) return Result<MatchStateDto>.Fail(err);
        return await _committer.CommitAsync(w.Match);
    }

    public async Task<Result<MatchStateDto>> NewBatterAsync(Guid matchId, NewBatterRequest request) {
        var load = await InningsWork.LoadAsync(_db, matchId);
        if (!load.Succeeded) return Result<MatchStateDto>.Fail(load.Error!);
        var w = load.Value!; var inn = w.Innings;
        if (inn.CurrentStrikerId is not null && inn.CurrentNonStrikerId is not null)
            return Result<MatchStateDto>.Fail(Fail.Unprocessable("No incoming batter is needed right now."));
        var err = BallEngine.ValidateIncoming(w, request.PlayerId, null, null);
        if (err is not null) return Result<MatchStateDto>.Fail(err);
        if (inn.CurrentStrikerId is null) inn.CurrentStrikerId = request.PlayerId; else inn.CurrentNonStrikerId = request.PlayerId;
        return await _committer.CommitAsync(w.Match);
    }

    public async Task<Result<MatchStateDto>> NewBowlerAsync(Guid matchId, NewBowlerRequest request) {
        var load = await InningsWork.LoadAsync(_db, matchId);
        if (!load.Succeeded) return Result<MatchStateDto>.Fail(load.Error!);
        var w = load.Value!; var inn = w.Innings;
        var pid = request.PlayerId;

        if (!w.IsBowlerEligibleSquad(pid)) return Result<MatchStateDto>.Fail(Fail.Validation("The bowler must be in the fielding XI."));
        if (pid == inn.LastBowlerId) return Result<MatchStateDto>.Fail(Fail.Validation("A bowler cannot bowl two overs in a row."));
        if (pid == inn.CurrentBowlerId) return Result<MatchStateDto>.Fail(Fail.Validation("That bowler is already bowling."));
        if (!CricketRules.BowlerHasQuotaLeft(w.BowlerLegalBalls(pid), w.Rules, inn))
            return Result<MatchStateDto>.Fail(Fail.Validation($"That bowler has used the maximum {CricketRules.MaxOversForBowler(w.Rules, inn)} overs."));

        var openOver = w.Overs.FirstOrDefault(o => !o.IsCompleted);
        var midOver = openOver is not null && w.LiveBallsInOver(openOver) > 0;
        if (inn.CurrentBowlerId is not null && midOver && !request.ReplaceMidOver)
            return Result<MatchStateDto>.Fail(Fail.Unprocessable("An over is in progress. Confirm that you are replacing the bowler mid-over."));
        inn.CurrentBowlerId = pid;
        return await _committer.CommitAsync(w.Match);
    }

    public async Task<Result<MatchStateDto>> SwapStrikeAsync(Guid matchId) {
        var load = await InningsWork.LoadAsync(_db, matchId);
        if (!load.Succeeded) return Result<MatchStateDto>.Fail(load.Error!);
        var inn = load.Value!.Innings;
        // Choosing the next batter or bowler during a break is allowed (that is when the commentators need the time), but the strike is not changed while play is stopped.
        if (load.Value!.MustBeLive() is { } stopped) return Result<MatchStateDto>.Fail(stopped);
        if (inn.CurrentStrikerId is null || inn.CurrentNonStrikerId is null)
            return Result<MatchStateDto>.Fail(Fail.Unprocessable("Both batters must be at the crease."));
        (inn.CurrentStrikerId, inn.CurrentNonStrikerId) = (inn.CurrentNonStrikerId, inn.CurrentStrikerId);
        return await _committer.CommitAsync(load.Value!.Match);
    }

    /// <summary>Power Surge: start it (from the next over if an over is under way), or cancel it before a ball has been bowled in it.</summary>
    public async Task<Result<MatchStateDto>> PowerSurgeAsync(Guid matchId, PowerSurgeRequest request) {
        var load = await InningsWork.LoadAsync(_db, matchId);
        if (!load.Succeeded) return Result<MatchStateDto>.Fail(load.Error!);
        var w = load.Value!; var inn = w.Innings; var rules = w.Match.MatchRules;
        if (rules.IsTest) return Result<MatchStateDto>.Fail(Fail.Unprocessable("A Test match has no Power Surge."));
        if (inn.IsSuperOver) return Result<MatchStateDto>.Fail(Fail.Unprocessable("A Super Over has no Power Surge."));
        var ps = MatchStateBuilder.PowerSurgeState(inn, rules, inn.LegalBalls, inn.Status == CricketStream.Domain.Enums.InningsStatus.InProgress);
        if (request.Start) {
            if (w.MustBeLive() is { } stopped) return Result<MatchStateDto>.Fail(stopped);
            if (inn.PowerSurgeStartOver is not null) return Result<MatchStateDto>.Fail(Fail.Unprocessable("The Power Surge has already been taken in this innings."));
            if (!ps.CanStart) return Result<MatchStateDto>.Fail(Fail.Unprocessable(rules.PowerSurgeEnabled
                ? $"The Power Surge can only start from over {ps.EarliestOver} to over {ps.LatestOver}."
                : "There are not enough overs left in the innings for a Power Surge."));
            inn.PowerSurgeStartOver = ps.NextStartOver;
        } else {
            if (!ps.CanCancel) return Result<MatchStateDto>.Fail(Fail.Unprocessable("The Power Surge can only be cancelled before a ball has been bowled in it."));
            inn.PowerSurgeStartOver = null;
        }
        return await _committer.CommitAsync(w.Match);
    }

    public const int MaxSixMeters = 200;

    /// <summary>Records how far the last six travelled (for the "six meter" graphic). Only the most recent ball, and only if it was a six.</summary>
    public async Task<Result<MatchStateDto>> SetSixDistanceAsync(Guid matchId, SixDistanceRequest request) {
        if (request.Meters is { } m && (m < 1 || m > MaxSixMeters))
            return Result<MatchStateDto>.Fail(Fail.Validation($"The distance must be between 1 and {MaxSixMeters} metres."));
        var load = await InningsWork.LoadAsync(_db, matchId);
        if (!load.Succeeded) return Result<MatchStateDto>.Fail(load.Error!);
        var last = load.Value!.Live.OrderBy(b => b.Sequence).LastOrDefault();
        if (last is null || !last.IsSix) return Result<MatchStateDto>.Fail(Fail.Unprocessable("The last ball was not a six."));
        last.SixDistance = request.Meters;
        return await _committer.CommitAsync(load.Value!.Match);
    }

    public async Task<Result<MatchStateDto>> RetireAsync(Guid matchId, RetireRequest request, string userId) {
        var load = await InningsWork.LoadAsync(_db, matchId);
        if (!load.Succeeded) return Result<MatchStateDto>.Fail(load.Error!);
        var w = load.Value!; var inn = w.Innings;
        if (request.PlayerId != inn.CurrentStrikerId && request.PlayerId != inn.CurrentNonStrikerId)
            return Result<MatchStateDto>.Fail(Fail.Validation("Only a batter at the crease can retire."));
        if (request.NewBatterId is { } nb) {
            var err = BallEngine.ValidateIncoming(w, nb, null, request.PlayerId);
            if (err is not null) return Result<MatchStateDto>.Fail(err);
        }
        _db.AuditLogs.Add(new AuditLog {
            UserId = userId, MatchId = matchId, ActionType = "Retire", EntityName = "Innings", EntityId = inn.Id,
            PreviousStateJson = JsonSerializer.Serialize(new { inn.CurrentStrikerId, inn.CurrentNonStrikerId, retired = request.PlayerId }),
            Reason = request.Reason ?? "Retired hurt",
        });
        // Remember who retired and when: the scorecard says "retired hurt" (not "not out"), he may come back later, and an undo of the ball before it takes it back.
        _db.InningsBatterStatuses.Add(new InningsBatterStatus {
            InningsId = inn.Id, PlayerId = request.PlayerId, Kind = BatterStatusKind.RetiredHurt,
            AfterBallSequence = w.Live.Select(b => b.Sequence).DefaultIfEmpty(0).Max(), Reason = request.Reason,
        });
        if (inn.CurrentStrikerId == request.PlayerId) inn.CurrentStrikerId = request.NewBatterId;
        else inn.CurrentNonStrikerId = request.NewBatterId;
        return await _committer.CommitAsync(w.Match);
    }

    /// <summary>Marks a batting-side player absent hurt (cannot bat in this innings and is not counted towards "all out"), or clears it.</summary>
    public async Task<Result<MatchStateDto>> AbsentHurtAsync(Guid matchId, AbsentHurtRequest request, string userId) {
        var load = await InningsWork.LoadAsync(_db, matchId);
        if (!load.Succeeded) return Result<MatchStateDto>.Fail(load.Error!);
        var w = load.Value!; var inn = w.Innings;
        if (!w.BattingXi().Any(p => p.PlayerId == request.PlayerId)) return Result<MatchStateDto>.Fail(Fail.Validation("The player must be in the batting team's playing XI."));
        var existing = w.Statuses.Where(s => s.Kind == BatterStatusKind.AbsentHurt && s.PlayerId == request.PlayerId).ToList();
        if (request.Absent) {
            if (existing.Count > 0) return Result<MatchStateDto>.Fail(Fail.Unprocessable("That player is already marked absent hurt."));
            if (request.PlayerId == inn.CurrentStrikerId || request.PlayerId == inn.CurrentNonStrikerId)
                return Result<MatchStateDto>.Fail(Fail.Unprocessable("That player is at the crease. Retire him instead."));
            if (w.Live.Any(b => b.StrikerId == request.PlayerId || b.NonStrikerId == request.PlayerId) || w.Dismissed().Contains(request.PlayerId))
                return Result<MatchStateDto>.Fail(Fail.Unprocessable("That player has already batted in this innings."));
            _db.InningsBatterStatuses.Add(new InningsBatterStatus {
                InningsId = inn.Id, PlayerId = request.PlayerId, Kind = BatterStatusKind.AbsentHurt,
                AfterBallSequence = w.Live.Select(b => b.Sequence).DefaultIfEmpty(0).Max(),
            });
        } else {
            if (existing.Count == 0) return Result<MatchStateDto>.Fail(Fail.Unprocessable("That player is not marked absent hurt."));
            foreach (var s in existing) _db.InningsBatterStatuses.Remove(s);
        }
        _db.AuditLogs.Add(new AuditLog {
            UserId = userId, MatchId = matchId, ActionType = request.Absent ? "AbsentHurt" : "AbsentHurtCleared", EntityName = "Innings", EntityId = inn.Id,
            PreviousStateJson = JsonSerializer.Serialize(new { request.PlayerId }), Reason = request.Absent ? "Absent hurt" : "Back available",
        });
        return await _committer.CommitAsync(w.Match);
    }

    /// <summary>The umpire called dead ball. Nothing is scored; the count only makes the overlay flash DEAD BALL.</summary>
    public async Task<Result<MatchStateDto>> DeadBallAsync(Guid matchId, string userId) {
        var load = await InningsWork.LoadAsync(_db, matchId);
        if (!load.Succeeded) return Result<MatchStateDto>.Fail(load.Error!);
        var w = load.Value!;
        if (w.MustBeLive() is { } stopped) return Result<MatchStateDto>.Fail(stopped);
        w.Innings.DeadBalls++;
        _db.AuditLogs.Add(new AuditLog {
            UserId = userId, MatchId = matchId, ActionType = "DeadBall", EntityName = "Innings", EntityId = w.Innings.Id,
            PreviousStateJson = JsonSerializer.Serialize(new { deadBalls = w.Innings.DeadBalls - 1 }), Reason = "Dead ball",
        });
        return await _committer.CommitAsync(w.Match);
    }

    /// <summary>Rain-reduced overs and/or a revised (DLS) target for the innings in progress. The scorer enters the numbers; DLS itself is not calculated here.</summary>
    public async Task<Result<MatchStateDto>> ReviseInningsAsync(Guid matchId, ReviseInningsRequest request, string userId) {
        if (request.Overs is null && request.Target is null) return Result<MatchStateDto>.Fail(Fail.Validation("Enter the revised overs, the revised target, or both."));
        var load = await InningsWork.LoadAsync(_db, matchId);
        if (!load.Succeeded) return Result<MatchStateDto>.Fail(load.Error!);
        var w = load.Value!; var inn = w.Innings; var rules = w.Rules;
        if (rules.IsTest) return Result<MatchStateDto>.Fail(Fail.Unprocessable("Overs and targets are only revised in limited-overs matches."));
        if (inn.IsSuperOver) return Result<MatchStateDto>.Fail(Fail.Unprocessable("A Super Over cannot be revised."));
        var before = new { overs = CricketRules.OversLimit(rules, inn), target = inn.Target };
        if (request.Overs is { } o) {
            if (o < 1 || o > rules.OversPerInnings) return Result<MatchStateDto>.Fail(Fail.Validation($"The revised overs must be between 1 and {rules.OversPerInnings}."));
            if (o * rules.BallsPerOver < inn.LegalBalls) return Result<MatchStateDto>.Fail(Fail.Validation("The revised overs cannot be fewer than the overs already bowled."));
            inn.OversLimit = o;
        }
        if (request.Target is { } t) {
            if (inn.Target is null) return Result<MatchStateDto>.Fail(Fail.Unprocessable("This innings has no target to revise. Enter the revised overs only."));
            if (t < 1 || t > 2000) return Result<MatchStateDto>.Fail(Fail.Validation("Enter a sensible target."));
            inn.Target = t;
        }
        inn.IsDls = true;
        _db.AuditLogs.Add(new AuditLog {
            UserId = userId, MatchId = matchId, ActionType = "Revise", EntityName = "Innings", EntityId = inn.Id,
            PreviousStateJson = JsonSerializer.Serialize(before), AfterStateJson = JsonSerializer.Serialize(new { overs = CricketRules.OversLimit(rules, inn), target = inn.Target }),
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? "Revised (rain / DLS)" : request.Reason,
        });
        // Revising can finish the innings at once: the new overs are all bowled, or the new target is already reached.
        var oversDone = inn.LegalBalls > 0 && inn.LegalBalls % rules.BallsPerOver == 0 && CricketRules.OversComplete(inn.LegalBalls, rules, inn);
        if (oversDone || (inn.Target is { } tg && inn.TotalRuns >= tg)) MatchCompletion.EndInnings(w);
        return await _committer.CommitAsync(w.Match);
    }

    public async Task<Result<MatchStateDto>> PenaltyAsync(Guid matchId, PenaltyRequest request, string userId) {
        if (request.Runs == 0 || Math.Abs(request.Runs) > 25)
            return Result<MatchStateDto>.Fail(Fail.Validation("Penalty runs must be between -25 and 25 (not 0). Use a negative number to correct a mistake."));
        var load = await InningsWork.LoadAsync(_db, matchId);
        if (!load.Succeeded) return Result<MatchStateDto>.Fail(load.Error!);
        var w = load.Value!; var inn = w.Innings;
        if (request.ToFieldingSide) return await FieldingPenaltyAsync(w, request, userId);
        var before = inn.PenaltyRuns;
        inn.PenaltyRuns = Math.Max(0, inn.PenaltyRuns + request.Runs);
        w.RecomputeTotals();
        _db.AuditLogs.Add(new AuditLog {
            UserId = userId, MatchId = matchId, ActionType = "Penalty", EntityName = "Innings", EntityId = inn.Id,
            PreviousStateJson = JsonSerializer.Serialize(new { penaltyRuns = before }),
            AfterStateJson = JsonSerializer.Serialize(new { penaltyRuns = inn.PenaltyRuns }),
            Reason = request.Reason ?? "Penalty runs",
        });
        if (inn.Target is { } t && inn.TotalRuns >= t) MatchCompletion.EndInnings(w);
        return await _committer.CommitAsync(w.Match);
    }

    /// <summary>Penalty runs awarded to the side that is bowling (Law 41.17): they go to its latest innings, or wait for its next one if it has not batted yet.
    /// A side chasing that total then needs more, so the target rises with it.</summary>
    private async Task<Result<MatchStateDto>> FieldingPenaltyAsync(InningsWork w, PenaltyRequest request, string userId) {
        var m = w.Match; var inn = w.Innings; var team = inn.BowlingTeamId;
        var theirs = m.Innings.Where(i => i.BattingTeamId == team && i.Id != inn.Id).OrderBy(i => i.InningsNumber).LastOrDefault();
        int before, after;
        if (theirs is not null) {
            before = theirs.PenaltyRuns;
            after = Math.Max(0, before + request.Runs);
            var delta = after - before;
            theirs.PenaltyRuns = after; theirs.TotalRuns += delta; theirs.TotalExtras += delta; theirs.UpdatedAt = DateTimeOffset.UtcNow;
            if (inn.Target is { } tg && delta != 0) inn.Target = Math.Max(1, tg + delta);
        } else {
            before = team == m.HomeTeamId ? m.PendingPenaltyHome : m.PendingPenaltyAway;
            after = Math.Max(0, before + request.Runs);
            if (team == m.HomeTeamId) m.PendingPenaltyHome = after; else m.PendingPenaltyAway = after;
        }
        _db.AuditLogs.Add(new AuditLog {
            UserId = userId, MatchId = m.Id, ActionType = "Penalty", EntityName = "Innings", EntityId = (theirs ?? inn).Id,
            PreviousStateJson = JsonSerializer.Serialize(new { penaltyRuns = before }),
            AfterStateJson = JsonSerializer.Serialize(new { penaltyRuns = after, toFieldingSide = true }),
            Reason = request.Reason ?? "Penalty runs to the fielding side",
        });
        if (inn.Target is { } t2 && inn.TotalRuns >= t2) MatchCompletion.EndInnings(w);
        return await _committer.CommitAsync(m);
    }
}

