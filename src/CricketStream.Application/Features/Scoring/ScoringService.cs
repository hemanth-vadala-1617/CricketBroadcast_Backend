using System.Text.Json;
using CricketStream.Application.Common;
using CricketStream.Application.Features.Matches;
using CricketStream.Application.Interfaces;
using CricketStream.Contracts.Matches;
using CricketStream.Contracts.Scoring;
using CricketStream.Domain.Entities;
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
        if (!CricketRules.BowlerHasQuotaLeft(w.BowlerLegalBalls(pid), w.Rules))
            return Result<MatchStateDto>.Fail(Fail.Validation($"That bowler has used the maximum {w.Rules.MaxOversPerBowler} overs."));

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
        var ps = MatchStateBuilder.PowerSurgeState(inn, rules, inn.LegalBalls, inn.Status == CricketStream.Domain.Enums.InningsStatus.InProgress);
        if (request.Start) {
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
        if (inn.CurrentStrikerId == request.PlayerId) inn.CurrentStrikerId = request.NewBatterId;
        else inn.CurrentNonStrikerId = request.NewBatterId;
        return await _committer.CommitAsync(w.Match);
    }

    public async Task<Result<MatchStateDto>> PenaltyAsync(Guid matchId, PenaltyRequest request, string userId) {
        if (request.Runs == 0 || Math.Abs(request.Runs) > 25)
            return Result<MatchStateDto>.Fail(Fail.Validation("Penalty runs must be between -25 and 25 (not 0). Use a negative number to correct a mistake."));
        var load = await InningsWork.LoadAsync(_db, matchId);
        if (!load.Succeeded) return Result<MatchStateDto>.Fail(load.Error!);
        var w = load.Value!; var inn = w.Innings;
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
}

