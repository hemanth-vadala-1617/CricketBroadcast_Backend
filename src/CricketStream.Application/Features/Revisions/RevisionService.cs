using System.Text.Json;
using CricketStream.Application.Common;
using CricketStream.Application.Features.Matches;
using CricketStream.Application.Features.Scoring;
using CricketStream.Application.Interfaces;
using CricketStream.Contracts.Matches;
using CricketStream.Contracts.Revisions;
using CricketStream.Domain.Entities;
using CricketStream.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CricketStream.Application.Features.Revisions;

/// <summary>Undo walks back one ball at a time (unlimited depth). To correct an older ball, undo back to it
/// and re-score; the scorer's crease positions then stay consistent by construction.</summary>
public class RevisionService : IRevisionService {
    private readonly ICricketDbContext _db;
    private readonly MatchCommitter _committer;
    private readonly IValidator<UndoBallRequest> _undoValidator;
    private readonly IValidator<EditBallRequest> _editValidator;

    public RevisionService(ICricketDbContext db, MatchCommitter committer, IValidator<UndoBallRequest> undoValidator, IValidator<EditBallRequest> editValidator) {
        _db = db; _committer = committer; _undoValidator = undoValidator; _editValidator = editValidator;
    }

    public async Task<Result<MatchStateDto>> UndoLastBallAsync(Guid matchId, UndoBallRequest request, string userId) {
        var bad = await _undoValidator.FirstErrorAsync(request);
        if (bad is not null) return Result<MatchStateDto>.Fail(Fail.Validation(bad));

        var load = await InningsWork.LoadAsync(_db, matchId, requireInProgress: false);
        if (!load.Succeeded) return Result<MatchStateDto>.Fail(load.Error!);
        var w = load.Value!;
        var err = BallEngine.VoidLast(w, userId, request.Reason, out var previous, _db);
        if (err is not null) return Result<MatchStateDto>.Fail(err);

        _db.AuditLogs.Add(new AuditLog {
            UserId = userId, MatchId = matchId, ActionType = "Undo", EntityName = "Ball", EntityId = w.Balls.Where(b => b.IsVoided).OrderBy(b => b.Sequence).Last().Id,
            PreviousStateJson = previous ?? "{}", Reason = request.Reason,
        });
        return await _committer.CommitAsync(w.Match);
    }

    public async Task<Result<MatchStateDto>> EditLastBallAsync(Guid matchId, EditBallRequest request, string userId) {
        var bad = await _editValidator.FirstErrorAsync(request);
        if (bad is not null) return Result<MatchStateDto>.Fail(Fail.Validation(bad));
        if (await _db.Balls.AnyAsync(b => b.ClientRequestId == request.Ball.ClientRequestId))
            return Result<MatchStateDto>.Fail(Fail.Conflict("This edit was already applied (duplicate clientRequestId)."));

        var load = await InningsWork.LoadAsync(_db, matchId, requireInProgress: false);
        if (!load.Succeeded) return Result<MatchStateDto>.Fail(load.Error!);
        var w = load.Value!;
        var voidErr = BallEngine.VoidLast(w, userId, $"Edited: {request.Reason}", out var previous, _db);
        if (voidErr is not null) return Result<MatchStateDto>.Fail(voidErr);
        var voidedId = w.Balls.Where(b => b.IsVoided).OrderBy(b => b.Sequence).Last().Id;

        var applyErr = BallEngine.Apply(w, request.Ball, userId, _db);
        if (applyErr is not null) return Result<MatchStateDto>.Fail(applyErr); // nothing was saved, so nothing is half-edited

        var replacement = w.Balls.Last();
        _db.AuditLogs.Add(new AuditLog {
            UserId = userId, MatchId = matchId, ActionType = "Edit", EntityName = "Ball", EntityId = voidedId,
            PreviousStateJson = previous ?? "{}", AfterStateJson = JsonSerializer.Serialize(BallEngine.Snapshot(replacement)),
            Reason = request.Reason,
        });
        return await _committer.CommitAsync(w.Match);
    }

    public async Task<Result<MatchStateDto>> CancelInningsStartAsync(Guid matchId, string userId) {
        var load = await InningsWork.LoadAsync(_db, matchId, requireInProgress: false);
        if (!load.Succeeded) return Result<MatchStateDto>.Fail(load.Error!);
        var w = load.Value!;
        if (w.Balls.Count > 0)
            return Result<MatchStateDto>.Fail(Fail.Unprocessable("Balls have been scored in this innings. Undo them instead."));

        var m = w.Match; var inn = w.Innings;
        _db.AuditLogs.Add(new AuditLog {
            UserId = userId, MatchId = matchId, ActionType = "CancelInnings", EntityName = "Innings", EntityId = inn.Id,
            PreviousStateJson = JsonSerializer.Serialize(new { inn.InningsNumber, inn.BattingTeamId, inn.CurrentStrikerId, inn.CurrentNonStrikerId, inn.CurrentBowlerId }),
            Reason = "Innings start cancelled before the first ball",
        });
        var previous = m.Innings.Where(i => i.Id != inn.Id).OrderBy(i => i.InningsNumber).LastOrDefault();
        m.CurrentInningsId = previous?.Id;
        m.Status = previous is null ? MatchStatus.Live : MatchStatus.InningsBreak;
        // Penalty runs that were waiting for this side go back to waiting.
        if (inn.PenaltyRuns > 0) { if (inn.BattingTeamId == m.HomeTeamId) m.PendingPenaltyHome += inn.PenaltyRuns; else m.PendingPenaltyAway += inn.PenaltyRuns; }
        // Backing out of the first Super Over innings leaves the match tied again.
        if (inn.IsSuperOver && previous is { IsSuperOver: false }) {
            m.Status = MatchStatus.Completed; m.ResultType = MatchResultType.Tie; m.ResultText = "Match tied"; m.WinnerTeamId = null; m.ActualEnd = DateTimeOffset.UtcNow;
        }
        m.Innings.Remove(inn);
        _db.Innings.Remove(inn);
        return await _committer.CommitAsync(m);
    }
}
