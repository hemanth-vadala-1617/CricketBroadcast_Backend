using CricketStream.Application.Common;
using CricketStream.Application.Features;
using CricketStream.Application.Features.Matches;
using CricketStream.Application.Interfaces;
using CricketStream.Contracts.Master;
using CricketStream.Contracts.Players;
using CricketStream.Domain.Entities;
using CricketStream.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CricketStream.Application.Features.Master;

public class MasterDataService : IMasterDataService {
    private readonly ICricketDbContext _db;
    private readonly IValidator<PlayerDto> _player;
    private readonly IValidator<VenueDto> _venue;
    private readonly IValidator<TournamentDto> _tournament;
    private readonly IValidator<MatchRulesDto> _rules;
    private readonly IValidator<OverlayThemeDto> _theme;

    private readonly IMatchStateService _state;
    private readonly IMatchBroadcaster _broadcaster;

    public MasterDataService(ICricketDbContext db, IValidator<PlayerDto> player, IValidator<VenueDto> venue,
        IValidator<TournamentDto> tournament, IValidator<MatchRulesDto> rules, IValidator<OverlayThemeDto> theme,
        IMatchStateService state, IMatchBroadcaster broadcaster) {
        _db = db; _player = player; _venue = venue; _tournament = tournament; _rules = rules; _theme = theme;
        _state = state; _broadcaster = broadcaster;
    }

    // A theme edit must reach overlays that are already on air: bump each affected match and broadcast its new state.
    private async Task PushThemeAsync(Guid themeId)
    {
        var affected = await _db.Matches.Where(m => m.OverlayThemeId == themeId
            || (m.OverlayThemeId == null && _db.Tournaments.Any(t => t.Id == m.TournamentId && t.OverlayThemeId == themeId))).ToListAsync();
        if (affected.Count == 0) return;
        foreach (var m in affected) { m.Version++; m.UpdatedAt = DateTimeOffset.UtcNow; }
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return; }   // a scorer was mid-ball; their next update carries the new theme anyway
        foreach (var m in affected)
        {
            var state = await _state.GetAsync(m.Id);
            if (state is not null) await _broadcaster.BroadcastStateAsync(m.Id, state);
        }
    }

    private static Result<T> Bad<T>(string message) => Result<T>.Fail(Fail.Validation(message));
    private static Result<T> Missing<T>(string what) => Result<T>.Fail(Fail.NotFound($"{what} not found."));
    private static string? Like(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim().ToLower();

    // ---------------- players ----------------
    private static PlayerDto Map(Player p) => new() {
        Id = p.Id, CurrentTeamId = p.CurrentTeamId, CurrentTeamName = p.CurrentTeam?.Name, FirstName = p.FirstName, LastName = p.LastName,
        DisplayName = p.DisplayName, ShortName = p.ShortName, PhotoUrl = p.PhotoUrl, JerseyNumber = p.JerseyNumber,
        BattingStyle = p.BattingStyle, BowlingStyle = p.BowlingStyle, PlayerRole = p.PlayerRole.ToString(), IsActive = p.IsActive,
        CareerLabel = p.CareerLabel, CareerMatches = p.CareerMatches, CareerRuns = p.CareerRuns, CareerAverage = p.CareerAverage,
        CareerStrikeRate = p.CareerStrikeRate, CareerFifties = p.CareerFifties, CareerHundreds = p.CareerHundreds,
    };

    public async Task<List<PlayerDto>> ListPlayersAsync(string? search, Guid? teamId, bool includeInactive) {
        var q = _db.Players.AsNoTracking().Include(p => p.CurrentTeam).Where(p => includeInactive || p.IsActive);
        if (teamId is { } t) q = q.Where(p => p.CurrentTeamId == t);
        if (Like(search) is { } like) q = q.Where(p => p.DisplayName.ToLower().Contains(like) || p.ShortName.ToLower().Contains(like));
        return (await q.OrderBy(p => p.DisplayName).Take(500).ToListAsync()).Select(Map).ToList();
    }

    public async Task<PlayerDto?> GetPlayerAsync(Guid id) {
        var p = await _db.Players.AsNoTracking().Include(x => x.CurrentTeam).FirstOrDefaultAsync(x => x.Id == id);
        return p is null ? null : Map(p);
    }

    public async Task<Result<PlayerDto>> SavePlayerAsync(Guid? id, PlayerDto dto) {
        var bad = await _player.FirstErrorAsync(dto);
        if (bad is not null) return Bad<PlayerDto>(bad);
        if (dto.CurrentTeamId is { } tid && !await _db.Teams.AnyAsync(t => t.Id == tid)) return Bad<PlayerDto>("Team not found.");
        Player? p;
        if (id is null) { p = new Player(); _db.Players.Add(p); }
        else { p = await _db.Players.FirstOrDefaultAsync(x => x.Id == id); if (p is null) return Missing<PlayerDto>("Player"); }
        p.CurrentTeamId = dto.CurrentTeamId; p.FirstName = dto.FirstName?.Trim() ?? ""; p.LastName = dto.LastName?.Trim() ?? "";
        p.DisplayName = dto.DisplayName.Trim(); p.ShortName = dto.ShortName.Trim(); p.PhotoUrl = dto.PhotoUrl; p.JerseyNumber = dto.JerseyNumber;
        p.BattingStyle = dto.BattingStyle ?? ""; p.BowlingStyle = dto.BowlingStyle ?? "";
        p.PlayerRole = Enum.Parse<PlayerRole>(dto.PlayerRole, true); p.IsActive = dto.IsActive; p.UpdatedAt = DateTimeOffset.UtcNow;
        p.CareerLabel = dto.CareerLabel?.Trim() ?? ""; p.CareerMatches = dto.CareerMatches; p.CareerRuns = dto.CareerRuns; p.CareerAverage = dto.CareerAverage;
        p.CareerStrikeRate = dto.CareerStrikeRate; p.CareerFifties = dto.CareerFifties; p.CareerHundreds = dto.CareerHundreds;
        await _db.SaveChangesAsync();
        return Result<PlayerDto>.Ok((await GetPlayerAsync(p.Id))!);
    }

    /// <summary>True = deactivated because the player appears in match history; false = removed.</summary>
    public async Task<Result<bool>> DeletePlayerAsync(Guid id) {
        var p = await _db.Players.FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return Missing<bool>("Player");
        var used = await _db.MatchPlayers.AnyAsync(m => m.PlayerId == id) || await _db.Balls.AnyAsync(b => b.StrikerId == id || b.NonStrikerId == id || b.BowlerId == id);
        if (used) { p.IsActive = false; p.UpdatedAt = DateTimeOffset.UtcNow; } else _db.Players.Remove(p);
        await _db.SaveChangesAsync();
        return Result<bool>.Ok(used);
    }

    // ---------------- venues ----------------
    private static VenueDto Map(Venue v) => new() {
        Id = v.Id, Name = v.Name, City = v.City, State = v.State, Country = v.Country, Capacity = v.Capacity,
        Timezone = v.Timezone, LogoUrl = v.LogoUrl, GroundImageUrl = v.GroundImageUrl,
    };

    public async Task<List<VenueDto>> ListVenuesAsync(string? search) {
        var q = _db.Venues.AsNoTracking().AsQueryable();
        if (Like(search) is { } like) q = q.Where(v => v.Name.ToLower().Contains(like) || v.City.ToLower().Contains(like));
        return (await q.OrderBy(v => v.Name).ToListAsync()).Select(Map).ToList();
    }

    public async Task<Result<VenueDto>> SaveVenueAsync(Guid? id, VenueDto dto) {
        var bad = await _venue.FirstErrorAsync(dto);
        if (bad is not null) return Bad<VenueDto>(bad);
        Venue? v;
        if (id is null) { v = new Venue(); _db.Venues.Add(v); }
        else { v = await _db.Venues.FirstOrDefaultAsync(x => x.Id == id); if (v is null) return Missing<VenueDto>("Venue"); }
        v.Name = dto.Name.Trim(); v.City = dto.City?.Trim() ?? ""; v.State = dto.State?.Trim() ?? ""; v.Country = dto.Country?.Trim() ?? "";
        v.Capacity = dto.Capacity; v.Timezone = string.IsNullOrWhiteSpace(dto.Timezone) ? "UTC" : dto.Timezone; v.LogoUrl = dto.LogoUrl;
        v.GroundImageUrl = dto.GroundImageUrl; v.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Result<VenueDto>.Ok(Map(v));
    }

    public async Task<Result<bool>> DeleteVenueAsync(Guid id) {
        var v = await _db.Venues.FirstOrDefaultAsync(x => x.Id == id);
        if (v is null) return Missing<bool>("Venue");
        if (await _db.Matches.AnyAsync(m => m.VenueId == id)) return Result<bool>.Fail(Fail.Conflict("This venue is used by a match and cannot be deleted."));
        _db.Venues.Remove(v); await _db.SaveChangesAsync();
        return Result<bool>.Ok(false);
    }

    // ---------------- tournaments ----------------
    private static TournamentDto Map(Tournament t) => new() {
        Id = t.Id, Name = t.Name, ShortName = t.ShortName, Season = t.Season, Format = t.Format.ToString(), StartDate = t.StartDate,
        EndDate = t.EndDate, LogoUrl = t.LogoUrl, Status = t.Status, Description = t.Description,
        DefaultMatchRulesId = t.DefaultMatchRulesId, OverlayThemeId = t.OverlayThemeId,
    };

    public async Task<List<TournamentDto>> ListTournamentsAsync(string? search) {
        var q = _db.Tournaments.AsNoTracking().AsQueryable();
        if (Like(search) is { } like) q = q.Where(t => t.Name.ToLower().Contains(like) || t.ShortName.ToLower().Contains(like));
        return (await q.OrderByDescending(t => t.StartDate).ToListAsync()).Select(Map).ToList();
    }

    public async Task<Result<TournamentDto>> SaveTournamentAsync(Guid? id, TournamentDto dto) {
        var bad = await _tournament.FirstErrorAsync(dto);
        if (bad is not null) return Bad<TournamentDto>(bad);
        if (dto.DefaultMatchRulesId is { } r && !await _db.MatchRules.AnyAsync(x => x.Id == r)) return Bad<TournamentDto>("Default match rules not found.");
        if (dto.OverlayThemeId is { } th && !await _db.OverlayThemes.AnyAsync(x => x.Id == th)) return Bad<TournamentDto>("Overlay theme not found.");
        Tournament? t;
        if (id is null) { t = new Tournament(); _db.Tournaments.Add(t); }
        else { t = await _db.Tournaments.FirstOrDefaultAsync(x => x.Id == id); if (t is null) return Missing<TournamentDto>("Tournament"); }
        t.Name = dto.Name.Trim(); t.ShortName = dto.ShortName.Trim(); t.Season = dto.Season?.Trim() ?? ""; t.Format = Enum.Parse<MatchFormat>(dto.Format, true);
        t.StartDate = dto.StartDate; t.EndDate = dto.EndDate; t.LogoUrl = dto.LogoUrl; t.Status = string.IsNullOrWhiteSpace(dto.Status) ? "Upcoming" : dto.Status;
        t.Description = dto.Description; t.DefaultMatchRulesId = dto.DefaultMatchRulesId; t.OverlayThemeId = dto.OverlayThemeId; t.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Result<TournamentDto>.Ok(Map(t));
    }

    public async Task<Result<bool>> DeleteTournamentAsync(Guid id) {
        var t = await _db.Tournaments.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return Missing<bool>("Tournament");
        if (await _db.Matches.AnyAsync(m => m.TournamentId == id)) return Result<bool>.Fail(Fail.Conflict("This tournament has matches and cannot be deleted."));
        _db.Tournaments.Remove(t); await _db.SaveChangesAsync();
        return Result<bool>.Ok(false);
    }

    // ---------------- match rules ----------------
    private static MatchRulesDto Map(MatchRules r) => new() {
        Id = r.Id, Name = r.Name, IsTest = r.IsTest, BallsPerOver = r.BallsPerOver, OversPerInnings = r.OversPerInnings,
        MaxOversPerBowler = r.MaxOversPerBowler, PowerplayOvers = r.PowerplayOvers, WideRuns = r.WideRuns, NoBallRuns = r.NoBallRuns,
        FreeHitEnabled = r.FreeHitEnabled, FollowOnEnabled = r.FollowOnEnabled, InningsPerSide = r.InningsPerSide, Days = r.Days,
        OversPerDay = r.OversPerDay, SessionsPerDay = r.SessionsPerDay, NewBallAfterOvers = r.NewBallAfterOvers, FollowOnMargin = r.FollowOnMargin,
    };

    public async Task<List<MatchRulesDto>> ListRulesAsync() =>
        (await _db.MatchRules.AsNoTracking().OrderBy(r => r.Name).ToListAsync()).Select(Map).ToList();

    public async Task<Result<MatchRulesDto>> SaveRulesAsync(Guid? id, MatchRulesDto dto) {
        var bad = await _rules.FirstErrorAsync(dto);
        if (bad is not null) return Bad<MatchRulesDto>(bad);
        MatchRules? r;
        if (id is null) { r = new MatchRules(); _db.MatchRules.Add(r); }
        else {
            r = await _db.MatchRules.FirstOrDefaultAsync(x => x.Id == id); if (r is null) return Missing<MatchRulesDto>("Match rules");
            if (await _db.Matches.AnyAsync(m => m.MatchRulesId == id && m.Status != MatchStatus.Scheduled))
                return Result<MatchRulesDto>.Fail(Fail.Conflict("These rules are used by a started match. Create a copy instead."));
        }
        r.Name = dto.Name.Trim(); r.IsTest = dto.IsTest; r.BallsPerOver = dto.BallsPerOver; r.OversPerInnings = dto.OversPerInnings;
        r.MaxOversPerBowler = dto.MaxOversPerBowler; r.PowerplayOvers = dto.PowerplayOvers; r.WideRuns = dto.WideRuns; r.NoBallRuns = dto.NoBallRuns;
        r.FreeHitEnabled = dto.FreeHitEnabled; r.FollowOnEnabled = dto.FollowOnEnabled; r.InningsPerSide = dto.InningsPerSide; r.Days = dto.Days;
        r.OversPerDay = dto.OversPerDay; r.SessionsPerDay = dto.SessionsPerDay; r.NewBallAfterOvers = dto.NewBallAfterOvers;
        r.FollowOnMargin = dto.FollowOnMargin; r.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Result<MatchRulesDto>.Ok(Map(r));
    }

    public async Task<Result<bool>> DeleteRulesAsync(Guid id) {
        var r = await _db.MatchRules.FirstOrDefaultAsync(x => x.Id == id);
        if (r is null) return Missing<bool>("Match rules");
        if (await _db.Matches.AnyAsync(m => m.MatchRulesId == id)) return Result<bool>.Fail(Fail.Conflict("These rules are used by a match and cannot be deleted."));
        var defaults = await _db.Tournaments.Where(t => t.DefaultMatchRulesId == id).ToListAsync();
        foreach (var t in defaults) t.DefaultMatchRulesId = null;
        _db.MatchRules.Remove(r); await _db.SaveChangesAsync();
        return Result<bool>.Ok(false);
    }

    // ---------------- themes ----------------
    private static OverlayThemeDto Map(OverlayTheme t) => new() {
        Id = t.Id, Name = t.Name, PrimaryColor = t.PrimaryColor, SecondaryColor = t.SecondaryColor, AccentColor = t.AccentColor,
        BackgroundUrl = t.BackgroundUrl, LogoUrl = t.LogoUrl, WatermarkUrl = t.WatermarkUrl, WatermarkText = t.WatermarkText, UseTeamColors = t.UseTeamColors,
    };

    public async Task<List<OverlayThemeDto>> ListThemesAsync() =>
        (await _db.OverlayThemes.AsNoTracking().OrderBy(t => t.Name).ToListAsync()).Select(Map).ToList();

    public async Task<Result<OverlayThemeDto>> SaveThemeAsync(Guid? id, OverlayThemeDto dto) {
        var bad = await _theme.FirstErrorAsync(dto);
        if (bad is not null) return Bad<OverlayThemeDto>(bad);
        OverlayTheme? t;
        if (id is null) { t = new OverlayTheme(); _db.OverlayThemes.Add(t); }
        else { t = await _db.OverlayThemes.FirstOrDefaultAsync(x => x.Id == id); if (t is null) return Missing<OverlayThemeDto>("Theme"); }
        t.Name = dto.Name.Trim(); t.PrimaryColor = dto.PrimaryColor; t.SecondaryColor = dto.SecondaryColor; t.AccentColor = dto.AccentColor;
        t.BackgroundUrl = dto.BackgroundUrl; t.LogoUrl = dto.LogoUrl; t.WatermarkUrl = dto.WatermarkUrl; t.WatermarkText = dto.WatermarkText;
        t.UseTeamColors = dto.UseTeamColors;
        t.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        if (id is not null) await PushThemeAsync(t.Id);
        return Result<OverlayThemeDto>.Ok(Map(t));
    }

    public async Task<Result<bool>> DeleteThemeAsync(Guid id) {
        var t = await _db.OverlayThemes.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return Missing<bool>("Theme");
        var matches = await _db.Matches.Where(m => m.OverlayThemeId == id).ToListAsync();
        foreach (var m in matches) m.OverlayThemeId = null;
        var tournaments = await _db.Tournaments.Where(x => x.OverlayThemeId == id).ToListAsync();
        foreach (var x in tournaments) x.OverlayThemeId = null;
        _db.OverlayThemes.Remove(t); await _db.SaveChangesAsync();
        return Result<bool>.Ok(false);
    }
}

