using CricketStream.Application.Common;
using CricketStream.Application.Interfaces;
using CricketStream.Contracts.Matches;
using CricketStream.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CricketStream.Application.Features.Matches;

public interface IMatchStateService {
    Task<MatchStateDto?> GetAsync(Guid matchId);
}

public class MatchStateService : IMatchStateService {
    private readonly ICricketDbContext _db;
    public MatchStateService(ICricketDbContext db) { _db = db; }

    public async Task<MatchStateDto?> GetAsync(Guid matchId) {
        var match = await _db.Matches.AsNoTracking().AsSplitQuery()
            .Include(m => m.HomeTeam).Include(m => m.AwayTeam).Include(m => m.Venue)
            .Include(m => m.Tournament).Include(m => m.MatchRules).Include(m => m.OverlayTheme)
            .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Player)
            .Include(m => m.Innings).ThenInclude(i => i.Overs).ThenInclude(o => o.Balls).ThenInclude(b => b.Dismissal)
            .FirstOrDefaultAsync(m => m.Id == matchId);
        if (match is null) return null;
        if (match.OverlayTheme is null && match.Tournament.OverlayThemeId is { } tournamentTheme)
            match.OverlayTheme = await _db.OverlayThemes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tournamentTheme);
        var graphics = await _db.OverlayGraphicStates.AsNoTracking().Where(g => g.MatchId == matchId).ToListAsync();
        var lastPenalty = await _db.AuditLogs.AsNoTracking()
            .Where(a => a.MatchId == matchId && a.ActionType == "Penalty").OrderByDescending(a => a.CreatedAt).FirstOrDefaultAsync();
        return MatchStateBuilder.Build(match, graphics, lastPenalty);
    }
}

/// <summary>Every command ends here: bump Version (also the concurrency token), save, rebuild state, broadcast.</summary>
public class MatchCommitter {
    private readonly ICricketDbContext _db;
    private readonly IMatchStateService _state;
    private readonly IMatchBroadcaster _broadcaster;
    public MatchCommitter(ICricketDbContext db, IMatchStateService state, IMatchBroadcaster broadcaster) {
        _db = db; _state = state; _broadcaster = broadcaster;
    }

    public async Task<Result<MatchStateDto>> CommitAsync(Match match) {
        match.Version++;
        match.UpdatedAt = DateTimeOffset.UtcNow;
        try {
            await _db.SaveChangesAsync();
        } catch (DbUpdateConcurrencyException) {
            return Result<MatchStateDto>.Fail(Fail.Conflict("The match changed while you were scoring (another scorer or tab). Refresh and try again."));
        }
        var state = await _state.GetAsync(match.Id);
        if (state is null) return Result<MatchStateDto>.Fail(Fail.NotFound("Match not found."));
        await _broadcaster.BroadcastStateAsync(match.Id, state);
        return Result<MatchStateDto>.Ok(state);
    }
}
