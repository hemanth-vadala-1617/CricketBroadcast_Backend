using CricketStream.Application.Common;
using CricketStream.Application.Features.Scoring;
using CricketStream.Application.Interfaces;
using CricketStream.Contracts.Matches;
using CricketStream.Domain.Entities;
using CricketStream.Domain.Enums;
using CricketStream.Domain.Rules;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CricketStream.Application.Features.Matches;

public class MatchService : IMatchService {
    private readonly ICricketDbContext _db;
    private readonly MatchCommitter _committer;
    private readonly IMatchStateService _state;
    private readonly IValidator<CreateMatchRequest> _createValidator;
    private readonly IValidator<SetSquadRequest> _squadValidator;
    private readonly IValidator<StartInningsRequest> _inningsValidator;
    private readonly IValidator<WinProbabilityRequest> _probValidator;

    public MatchService(ICricketDbContext db, MatchCommitter committer, IMatchStateService state,
        IValidator<CreateMatchRequest> createValidator, IValidator<SetSquadRequest> squadValidator,
        IValidator<StartInningsRequest> inningsValidator, IValidator<WinProbabilityRequest> probValidator) {
        _db = db; _committer = committer; _state = state; _createValidator = createValidator;
        _squadValidator = squadValidator; _inningsValidator = inningsValidator; _probValidator = probValidator;
    }

    private static Result<MatchStateDto> Fail_(ErrorKind k, string m) => Result<MatchStateDto>.Fail(new Error(k, m));
    private static Result<MatchStateDto> NotFound() => Fail_(ErrorKind.NotFound, "Match not found.");

    private IQueryable<Match> Full() => _db.Matches
        .Include(m => m.HomeTeam).Include(m => m.AwayTeam).Include(m => m.Venue).Include(m => m.Tournament)
        .Include(m => m.MatchRules).Include(m => m.MatchPlayers).Include(m => m.Innings);

    private static MatchResponse Map(Match m) => new() {
        Id = m.Id, Title = m.Title, TournamentId = m.TournamentId, TournamentName = m.Tournament.Name,
        HomeTeamId = m.HomeTeamId, AwayTeamId = m.AwayTeamId, HomeTeamName = m.HomeTeam.Name, AwayTeamName = m.AwayTeam.Name,
        HomeShortName = m.HomeTeam.ShortName, AwayShortName = m.AwayTeam.ShortName,
        VenueId = m.VenueId, VenueName = m.Venue.Name, MatchRulesId = m.MatchRulesId, OverlayThemeId = m.OverlayThemeId,
        MatchFormat = m.MatchFormat.ToString(), Status = m.Status.ToString(), ScheduledStart = m.ScheduledStart,
        TossWinnerTeamId = m.TossWinnerTeamId, TossDecision = m.TossDecision?.ToString(), ResultText = m.ResultText,
        SquadSize = m.MatchPlayers.Count(p => p.IsPlayingXI),
    };

    // ---------------- queries ----------------
    public async Task<List<MatchResponse>> ListAsync(string? status, Guid? tournamentId, string? search) {
        var q = _db.Matches.AsNoTracking()
            .Include(m => m.HomeTeam).Include(m => m.AwayTeam).Include(m => m.Venue).Include(m => m.Tournament).Include(m => m.MatchPlayers)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) {
            if (status.Equals("live", StringComparison.OrdinalIgnoreCase)) {
                var live = new[] { MatchStatus.Live, MatchStatus.InningsBreak, MatchStatus.RainDelay, MatchStatus.Paused };
                q = q.Where(m => live.Contains(m.Status));
            } else if (Enum.TryParse<MatchStatus>(status, true, out var s)) q = q.Where(m => m.Status == s);
        }
        if (tournamentId is { } t) q = q.Where(m => m.TournamentId == t);
        if (!string.IsNullOrWhiteSpace(search)) {
            var like = search.Trim().ToLower();
            q = q.Where(m => m.Title.ToLower().Contains(like) || m.HomeTeam.Name.ToLower().Contains(like) || m.AwayTeam.Name.ToLower().Contains(like));
        }
        var rows = await q.OrderByDescending(m => m.ScheduledStart ?? m.CreatedAt).Take(200).ToListAsync();
        return rows.Select(Map).ToList();
    }

    public async Task<MatchResponse?> GetAsync(Guid id) {
        var m = await Full().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return m is null ? null : Map(m);
    }

    public Task<MatchStateDto?> GetStateAsync(Guid id) => _state.GetAsync(id);

    // ---------------- CRUD ----------------
    private async Task<Error?> CheckRefsAsync(CreateMatchRequest r) {
        if (!await _db.Tournaments.AnyAsync(x => x.Id == r.TournamentId)) return Fail.Validation("Tournament not found.");
        if (!await _db.Teams.AnyAsync(x => x.Id == r.HomeTeamId && x.IsActive)) return Fail.Validation("Home team not found or inactive.");
        if (!await _db.Teams.AnyAsync(x => x.Id == r.AwayTeamId && x.IsActive)) return Fail.Validation("Away team not found or inactive.");
        if (!await _db.Venues.AnyAsync(x => x.Id == r.VenueId)) return Fail.Validation("Venue not found.");
        if (!await _db.MatchRules.AnyAsync(x => x.Id == r.MatchRulesId)) return Fail.Validation("Match rules not found.");
        if (r.OverlayThemeId is { } th && !await _db.OverlayThemes.AnyAsync(x => x.Id == th)) return Fail.Validation("Overlay theme not found.");
        return null;
    }

    public async Task<Result<MatchResponse>> CreateAsync(CreateMatchRequest request) {
        var bad = await _createValidator.FirstErrorAsync(request);
        if (bad is not null) return Result<MatchResponse>.Fail(Fail.Validation(bad));
        var refs = await CheckRefsAsync(request);
        if (refs is not null) return Result<MatchResponse>.Fail(refs);

        var home = await _db.Teams.FindAsync(request.HomeTeamId);
        var away = await _db.Teams.FindAsync(request.AwayTeamId);
        var match = new Match {
            TournamentId = request.TournamentId, HomeTeamId = request.HomeTeamId, AwayTeamId = request.AwayTeamId,
            VenueId = request.VenueId, MatchRulesId = request.MatchRulesId, OverlayThemeId = request.OverlayThemeId,
            Title = string.IsNullOrWhiteSpace(request.Title) ? $"{home!.Name} vs {away!.Name}" : request.Title.Trim(),
            MatchFormat = Enum.Parse<MatchFormat>(request.MatchFormat, true), ScheduledStart = request.ScheduledStart,
            Version = 1,
        };
        _db.Matches.Add(match);
        await _db.SaveChangesAsync();
        return Result<MatchResponse>.Ok((await GetAsync(match.Id))!);
    }

    public async Task<Result<MatchResponse>> UpdateAsync(Guid id, CreateMatchRequest request) {
        var bad = await _createValidator.FirstErrorAsync(request);
        if (bad is not null) return Result<MatchResponse>.Fail(Fail.Validation(bad));
        var match = await _db.Matches.FirstOrDefaultAsync(m => m.Id == id);
        if (match is null) return Result<MatchResponse>.Fail(Fail.NotFound("Match not found."));
        if (match.Status != MatchStatus.Scheduled) return Result<MatchResponse>.Fail(Fail.Unprocessable("Only a scheduled match can be edited."));
        var refs = await CheckRefsAsync(request);
        if (refs is not null) return Result<MatchResponse>.Fail(refs);

        if (match.HomeTeamId != request.HomeTeamId || match.AwayTeamId != request.AwayTeamId) {
            var old = await _db.MatchPlayers.Where(p => p.MatchId == id).ToListAsync();
            _db.MatchPlayers.RemoveRange(old); // squads belonged to the old teams
            _db.MatchStaff.RemoveRange(await _db.MatchStaff.Where(s => s.MatchId == id).ToListAsync());
        }
        match.TournamentId = request.TournamentId; match.HomeTeamId = request.HomeTeamId; match.AwayTeamId = request.AwayTeamId;
        match.VenueId = request.VenueId; match.MatchRulesId = request.MatchRulesId; match.OverlayThemeId = request.OverlayThemeId;
        match.MatchFormat = Enum.Parse<MatchFormat>(request.MatchFormat, true); match.ScheduledStart = request.ScheduledStart;
        if (!string.IsNullOrWhiteSpace(request.Title)) match.Title = request.Title.Trim();
        match.Version++; match.UpdatedAt = DateTimeOffset.UtcNow;
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return Result<MatchResponse>.Fail(Fail.Conflict("The match changed; refresh and try again.")); }
        return Result<MatchResponse>.Ok((await GetAsync(id))!);
    }

    public async Task<Result<bool>> DeleteAsync(Guid id) {
        var match = await _db.Matches.Include(m => m.Innings).FirstOrDefaultAsync(m => m.Id == id);
        if (match is null) return Result<bool>.Fail(Fail.NotFound("Match not found."));
        if (match.Status != MatchStatus.Scheduled || match.Innings.Count > 0)
            return Result<bool>.Fail(Fail.Unprocessable("Only a match that has not started can be deleted."));
        var gfx = await _db.OverlayGraphicStates.Where(g => g.MatchId == id).ToListAsync();
        _db.OverlayGraphicStates.RemoveRange(gfx);
        _db.Matches.Remove(match);
        await _db.SaveChangesAsync();
        return Result<bool>.Ok(true);
    }

    // ---------------- squads ----------------
    public async Task<Result<List<SquadDto>>> GetSquadsAsync(Guid id) {
        var match = await _db.Matches.AsNoTracking().Include(m => m.MatchPlayers).ThenInclude(p => p.Player).Include(m => m.MatchStaff).FirstOrDefaultAsync(m => m.Id == id);
        if (match is null) return Result<List<SquadDto>>.Fail(Fail.NotFound("Match not found."));
        SquadDto Squad(Guid team) => new() {
            TeamId = team,
            Players = match.MatchPlayers.Where(p => p.TeamId == team).OrderBy(p => p.IsPlayingXI ? 0 : 1).ThenBy(p => p.BattingOrder)
                .Select(p => new SquadMemberDto {
                    PlayerId = p.PlayerId, Name = p.Player.DisplayName, PhotoUrl = p.Player.PhotoUrl, Role = p.Player.PlayerRole.ToString(), IsPlayingXI = p.IsPlayingXI,
                    IsCaptain = p.IsCaptain, IsWicketKeeper = p.IsWicketKeeper, BattingOrder = p.BattingOrder,
                }).ToList(),
            Staff = match.MatchStaff.Where(s => s.TeamId == team).OrderBy(s => s.SortOrder).Select(s => new SquadStaffRequest { Name = s.Name, Role = s.Role }).ToList(),
        };
        return Result<List<SquadDto>>.Ok(new() { Squad(match.HomeTeamId), Squad(match.AwayTeamId) });
    }

    public async Task<Result<MatchStateDto>> SetSquadAsync(Guid id, SetSquadRequest request) {
        var bad = await _squadValidator.FirstErrorAsync(request);
        if (bad is not null) return Fail_(ErrorKind.Validation, bad);
        var match = await _db.Matches.Include(m => m.MatchPlayers).Include(m => m.MatchStaff).FirstOrDefaultAsync(m => m.Id == id);
        if (match is null) return NotFound();
        if (match.Status is not (MatchStatus.Scheduled or MatchStatus.TossCompleted))
            return Fail_(ErrorKind.Unprocessable, "The squad is locked once the match has started.");
        if (request.TeamId != match.HomeTeamId && request.TeamId != match.AwayTeamId)
            return Fail_(ErrorKind.Validation, "The team is not playing in this match.");

        var ids = request.Players.Select(p => p.PlayerId).ToList();
        var found = await _db.Players.Where(p => ids.Contains(p.Id) && p.IsActive).Select(p => p.Id).ToListAsync();
        if (found.Count != ids.Count) return Fail_(ErrorKind.Validation, "One or more players were not found or are inactive.");
        var otherTeam = match.MatchPlayers.Where(p => p.TeamId != request.TeamId).Select(p => p.PlayerId).ToHashSet();
        if (ids.Any(otherTeam.Contains)) return Fail_(ErrorKind.Validation, "A player cannot be in both squads.");

        _db.MatchPlayers.RemoveRange(match.MatchPlayers.Where(p => p.TeamId == request.TeamId).ToList());
        var order = 1;
        foreach (var p in request.Players)
            _db.MatchPlayers.Add(new MatchPlayer {
                MatchId = id, TeamId = request.TeamId, PlayerId = p.PlayerId, IsPlayingXI = p.IsPlayingXI,
                IsSubstitute = !p.IsPlayingXI, IsCaptain = p.IsCaptain, IsWicketKeeper = p.IsWicketKeeper,
                BattingOrder = p.IsPlayingXI ? (p.BattingOrder > 0 ? p.BattingOrder : order++) : 0,
            });
        _db.MatchStaff.RemoveRange(match.MatchStaff.Where(s => s.TeamId == request.TeamId).ToList());
        var staffOrder = 0;
        foreach (var s in request.Staff)
            _db.MatchStaff.Add(new MatchStaff { MatchId = id, TeamId = request.TeamId, Name = s.Name.Trim(), Role = s.Role.Trim(), SortOrder = staffOrder++ });
        return await _committer.CommitAsync(match);
    }

    // ---------------- lifecycle ----------------
    public async Task<Result<MatchStateDto>> RecordTossAsync(Guid id, TossRequest request) {
        var match = await _db.Matches.Include(m => m.MatchPlayers).FirstOrDefaultAsync(m => m.Id == id);
        if (match is null) return NotFound();
        if (match.Status != MatchStatus.Scheduled) return Fail_(ErrorKind.Unprocessable, "The toss can only be recorded while the match is scheduled.");
        if (request.TossWinnerTeamId != match.HomeTeamId && request.TossWinnerTeamId != match.AwayTeamId)
            return Fail_(ErrorKind.Validation, "The toss winner must be one of the two teams.");
        if (!Enum.TryParse<TossDecision>(request.TossDecision, true, out var decision))
            return Fail_(ErrorKind.Validation, "Toss decision must be Bat or Bowl.");
        foreach (var team in new[] { match.HomeTeamId, match.AwayTeamId })
            if (match.MatchPlayers.Count(p => p.TeamId == team && p.IsPlayingXI) < 2)
                return Fail_(ErrorKind.Unprocessable, "Both teams need a playing XI (at least 2 players) before the toss.");
        match.TossWinnerTeamId = request.TossWinnerTeamId; match.TossDecision = decision; match.Status = MatchStatus.TossCompleted;
        return await _committer.CommitAsync(match);
    }

    public async Task<Result<MatchStateDto>> StartMatchAsync(Guid id) {
        var match = await _db.Matches.FirstOrDefaultAsync(m => m.Id == id);
        if (match is null) return NotFound();
        if (match.Status != MatchStatus.TossCompleted) return Fail_(ErrorKind.Unprocessable, "The match can only start after the toss.");
        match.Status = MatchStatus.Live; match.ActualStart = DateTimeOffset.UtcNow; match.CurrentDay = 1; match.CurrentSession = 1;
        return await _committer.CommitAsync(match);
    }

    public async Task<Result<MatchStateDto>> StartInningsAsync(Guid id, StartInningsRequest request) {
        var bad = await _inningsValidator.FirstErrorAsync(request);
        if (bad is not null) return Fail_(ErrorKind.Validation, bad);
        var match = await Full().FirstOrDefaultAsync(m => m.Id == id);
        if (match is null) return NotFound();
        var rules = match.MatchRules;
        if (match.Status is not (MatchStatus.Live or MatchStatus.InningsBreak)) return Fail_(ErrorKind.Unprocessable, "Start the match first.");

        var all = match.Innings.OrderBy(i => i.InningsNumber).ToList();
        var last = all.LastOrDefault();
        if (last is not null && last.Status != InningsStatus.Completed) return Fail_(ErrorKind.Unprocessable, "The current innings is still in progress.");
        if (all.Count >= Math.Max(1, rules.InningsPerSide) * 2) return Fail_(ErrorKind.Unprocessable, "All innings have been played. Complete the match.");

        var expected = MatchStateBuilder.NextBattingTeamId(match, all);
        var batting = request.BattingTeamId ?? expected;
        if (batting is null) return Fail_(ErrorKind.Unprocessable, "Record the toss first.");
        if (batting != match.HomeTeamId && batting != match.AwayTeamId) return Fail_(ErrorKind.Validation, "The batting team is not playing in this match.");

        var followOn = false;
        if (batting != expected) {
            var fo = rules.IsTest && all.Count == 2 && last!.BattingTeamId == batting &&
                     CricketRules.FollowOnAvailable(rules, all[0].TotalRuns, all[1].TotalRuns);
            if (!fo) return Fail_(ErrorKind.Validation, "That batting order is not allowed (follow-on is only possible when enabled and the lead is large enough).");
            followOn = true;
        }
        var bowling = batting == match.HomeTeamId ? match.AwayTeamId : match.HomeTeamId;

        bool InXi(Guid team, Guid player) => match.MatchPlayers.Any(p => p.TeamId == team && p.PlayerId == player && p.IsPlayingXI);
        if (!InXi(batting.Value, request.StrikerId) || !InXi(batting.Value, request.NonStrikerId))
            return Fail_(ErrorKind.Validation, "The openers must be in the batting team's playing XI.");
        if (!InXi(bowling, request.BowlerId)) return Fail_(ErrorKind.Validation, "The opening bowler must be in the fielding team's playing XI.");

        var number = all.Count + 1;
        var mine = all.Where(i => i.BattingTeamId == batting).ToList();
        var theirs = all.Where(i => i.BattingTeamId != batting).ToList();
        var target = CricketRules.ComputeTarget(rules, number, mine.Sum(i => i.TotalRuns), mine.Count,
            theirs.Sum(i => i.TotalRuns), theirs.Count(i => i.Status == InningsStatus.Completed), all.Count > 0 ? all[0].TotalRuns : 0);

        var innings = new Innings {
            MatchId = id, BattingTeamId = batting.Value, BowlingTeamId = bowling, InningsNumber = number,
            Status = InningsStatus.InProgress, IsFollowOn = followOn, Target = target,
            CurrentStrikerId = request.StrikerId, CurrentNonStrikerId = request.NonStrikerId, CurrentBowlerId = request.BowlerId,
        };
        _db.Innings.Add(innings);
        match.CurrentInningsId = innings.Id;
        match.Status = MatchStatus.Live;
        return await _committer.CommitAsync(match);
    }

    public async Task<Result<MatchStateDto>> EndInningsAsync(Guid id, EndInningsRequest request) {
        var load = await InningsWork.LoadAsync(_db, id);
        if (!load.Succeeded) return Result<MatchStateDto>.Fail(load.Error!);
        var w = load.Value!;
        if (request.Declared && !w.Rules.IsTest) return Fail_(ErrorKind.Validation, "Only Test innings can be declared.");
        w.RecomputeTotals();
        MatchCompletion.EndInnings(w, request.Declared);
        return await _committer.CommitAsync(w.Match);
    }

    public async Task<Result<MatchStateDto>> CompleteAsync(Guid id, CompleteMatchRequest request) {
        if (!Enum.TryParse<MatchResultType>(request.ResultType, true, out var type) || type == MatchResultType.None)
            return Fail_(ErrorKind.Validation, "Result type must be Win, Tie, Draw, NoResult or Abandoned.");
        var match = await Full().FirstOrDefaultAsync(m => m.Id == id);
        if (match is null) return NotFound();
        var started = match.Status is MatchStatus.Live or MatchStatus.InningsBreak or MatchStatus.RainDelay or MatchStatus.Paused or MatchStatus.Completed;
        if (!started && type is not (MatchResultType.Abandoned or MatchResultType.NoResult))
            return Fail_(ErrorKind.Unprocessable, "The match has not started; it can only be abandoned.");
        if (type == MatchResultType.Win && request.WinnerTeamId is null)
            return Fail_(ErrorKind.Validation, "Pick the winning team.");
        if (request.WinnerTeamId is { } wt && wt != match.HomeTeamId && wt != match.AwayTeamId)
            return Fail_(ErrorKind.Validation, "The winner must be one of the two teams.");

        foreach (var i in match.Innings.Where(i => i.Status == InningsStatus.InProgress)) i.Status = InningsStatus.Completed;
        match.ResultType = type;
        match.WinnerTeamId = type == MatchResultType.Win ? request.WinnerTeamId : null;
        var winnerName = match.WinnerTeamId is { } wid ? (wid == match.HomeTeamId ? match.HomeTeam.Name : match.AwayTeam.Name) : null;
        match.ResultText = !string.IsNullOrWhiteSpace(request.ResultText) ? request.ResultText.Trim() : type switch {
            MatchResultType.Win => $"{winnerName} won",
            MatchResultType.Tie => "Match tied",
            MatchResultType.Draw => "Match drawn",
            MatchResultType.NoResult => "No result",
            _ => "Match abandoned",
        };
        match.Status = type == MatchResultType.Abandoned ? MatchStatus.Abandoned : MatchStatus.Completed;
        match.ActualEnd = DateTimeOffset.UtcNow;
        return await _committer.CommitAsync(match);
    }

    public async Task<Result<MatchStateDto>> SetWinProbabilityAsync(Guid id, WinProbabilityRequest request) {
        var bad = await _probValidator.FirstErrorAsync(request);
        if (bad is not null) return Fail_(ErrorKind.Validation, bad);
        var match = await _db.Matches.FirstOrDefaultAsync(m => m.Id == id);
        if (match is null) return NotFound();
        match.WinProbHome = request.Home; match.WinProbDraw = request.Draw; match.WinProbAway = request.Away;
        return await _committer.CommitAsync(match);
    }

    public async Task<Result<MatchStateDto>> AdvanceSessionAsync(Guid id) {
        var match = await _db.Matches.Include(m => m.MatchRules).FirstOrDefaultAsync(m => m.Id == id);
        if (match is null) return NotFound();
        var r = match.MatchRules;
        if (!r.IsTest) return Fail_(ErrorKind.Unprocessable, "Days and sessions only apply to Test matches.");
        if (match.Status is not (MatchStatus.Live or MatchStatus.InningsBreak or MatchStatus.RainDelay or MatchStatus.Paused))
            return Fail_(ErrorKind.Unprocessable, "The match is not in play.");
        if (match.CurrentSession < r.SessionsPerDay) match.CurrentSession++;
        else if (match.CurrentDay < r.Days) { match.CurrentDay++; match.CurrentSession = 1; match.OversLostToday = 0; }
        else return Fail_(ErrorKind.Unprocessable, "This is the last session of the last day.");
        return await _committer.CommitAsync(match);
    }

    public async Task<Result<MatchStateDto>> SetOversLostAsync(Guid id, OversLostRequest request) {
        var match = await _db.Matches.Include(m => m.MatchRules).FirstOrDefaultAsync(m => m.Id == id);
        if (match is null) return NotFound();
        if (!match.MatchRules.IsTest) return Fail_(ErrorKind.Unprocessable, "Overs-lost only applies to Test matches.");
        if (request.Overs < 0 || request.Overs > match.MatchRules.OversPerDay) return Fail_(ErrorKind.Validation, "Overs lost must be between 0 and the day's overs.");
        match.OversLostToday = request.Overs;
        return await _committer.CommitAsync(match);
    }

    public async Task<Result<MatchStateDto>> PauseAsync(Guid id, bool rain) {
        var match = await _db.Matches.FirstOrDefaultAsync(m => m.Id == id);
        if (match is null) return NotFound();
        if (match.Status is not (MatchStatus.Live or MatchStatus.InningsBreak)) return Fail_(ErrorKind.Unprocessable, "Only a live match can be paused.");
        match.Status = rain ? MatchStatus.RainDelay : MatchStatus.Paused;
        return await _committer.CommitAsync(match);
    }

    public async Task<Result<MatchStateDto>> ResumeAsync(Guid id) {
        var match = await _db.Matches.Include(m => m.Innings).FirstOrDefaultAsync(m => m.Id == id);
        if (match is null) return NotFound();
        if (match.Status is not (MatchStatus.RainDelay or MatchStatus.Paused)) return Fail_(ErrorKind.Unprocessable, "The match is not paused.");
        var latest = match.Innings.OrderBy(i => i.InningsNumber).LastOrDefault();
        match.Status = latest is { Status: InningsStatus.Completed } ? MatchStatus.InningsBreak : MatchStatus.Live;
        return await _committer.CommitAsync(match);
    }
}


