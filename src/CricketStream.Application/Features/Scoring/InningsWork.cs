using CricketStream.Application.Common;
using CricketStream.Application.Interfaces;
using CricketStream.Domain.Entities;
using CricketStream.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CricketStream.Application.Features.Scoring;

/// <summary>The tracked match + current innings + its full ball log, loaded once per command.
/// All derived numbers are recomputed from the non-voided balls in memory.</summary>
public sealed class InningsWork
{
    public Match Match { get; private init; } = null!;
    public Innings Innings { get; private init; } = null!;
    public List<Ball> Balls { get; private init; } = new();
    public List<Over> Overs { get; private init; } = new();

    public MatchRules Rules => Match.MatchRules;
    public IEnumerable<Ball> Live => Balls.Where(b => !b.IsVoided);

    public HashSet<Guid> Dismissed() =>
        Live.Where(b => b.IsWicket && b.Dismissal is not null).Select(b => b.Dismissal!.DismissedPlayerId).ToHashSet();

    public List<MatchPlayer> BattingXi() =>
        Match.MatchPlayers.Where(p => p.TeamId == Innings.BattingTeamId && p.IsPlayingXI).ToList();

    public bool IsFielder(Guid playerId) =>
        Match.MatchPlayers.Any(p => p.TeamId == Innings.BowlingTeamId && p.PlayerId == playerId);

    public bool IsBowlerEligibleSquad(Guid playerId) =>
        Match.MatchPlayers.Any(p => p.TeamId == Innings.BowlingTeamId && p.PlayerId == playerId && p.IsPlayingXI);

    public int BowlerLegalBalls(Guid bowlerId) => Live.Count(b => b.BowlerId == bowlerId && b.IsLegal);

    public int LiveBallsInOver(Over o) => Live.Count(b => b.OverId == o.Id);

    public void RecomputeTotals()
    {
        var live = Live.ToList();
        Innings.TotalRuns = live.Sum(b => b.RunsBat + b.RunsExtra) + Innings.PenaltyRuns;
        Innings.TotalExtras = live.Sum(b => b.RunsExtra) + Innings.PenaltyRuns;
        Innings.TotalWickets = live.Count(b => b.IsWicket);
        Innings.LegalBalls = live.Count(b => b.IsLegal);
        Innings.UpdatedAt = DateTimeOffset.UtcNow;
    }

    public static async Task<Result<InningsWork>> LoadAsync(ICricketDbContext db, Guid matchId, bool requireInProgress = true)
    {
        var match = await db.Matches
            .Include(m => m.MatchRules)
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Include(m => m.MatchPlayers)
            .Include(m => m.Innings)
            .FirstOrDefaultAsync(m => m.Id == matchId);
        if (match is null) return Result<InningsWork>.Fail(Fail.NotFound("Match not found."));

        var innings = match.Innings.FirstOrDefault(i => i.Id == match.CurrentInningsId);
        if (innings is null) return Result<InningsWork>.Fail(Fail.Unprocessable("No innings has been started."));
        if (requireInProgress && innings.Status != InningsStatus.InProgress)
            return Result<InningsWork>.Fail(Fail.Unprocessable("The innings is over."));

        var balls = await db.Balls.Include(b => b.Dismissal)
            .Where(b => b.InningsId == innings.Id).OrderBy(b => b.Sequence).ToListAsync();
        var overs = await db.Overs.Where(o => o.InningsId == innings.Id).OrderBy(o => o.OverNumber).ToListAsync();

        return Result<InningsWork>.Ok(new InningsWork { Match = match, Innings = innings, Balls = balls, Overs = overs });
    }
}
