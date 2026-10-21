using CricketStream.Application.Common;
using CricketStream.Application.Interfaces;
using CricketStream.Contracts.Teams;
using CricketStream.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CricketStream.Application.Features.Teams;

public class TeamService : ITeamService {
    private readonly ICricketDbContext _db;
    private readonly IValidator<CreateTeamRequest> _validator;
    public TeamService(ICricketDbContext db, IValidator<CreateTeamRequest> validator) { _db = db; _validator = validator; }

    private IQueryable<TeamDto> Project(IQueryable<Team> q) => q.Select(t => new TeamDto {
        Id = t.Id, Name = t.Name, ShortName = t.ShortName, Country = t.Country, LogoUrl = t.LogoUrl,
        PrimaryColor = t.PrimaryColor, SecondaryColor = t.SecondaryColor, IsActive = t.IsActive,
        PlayerCount = t.Players.Count(p => p.IsActive),
        TournamentIds = _db.TournamentTeams.Where(x => x.TeamId == t.Id).Select(x => x.TournamentId).ToList(),
    });

    public Task<List<TeamDto>> GetAllAsync(bool includeInactive = false) =>
        Project(_db.Teams.AsNoTracking().Where(t => includeInactive || t.IsActive).OrderBy(t => t.Name)).ToListAsync();

    public Task<TeamDto?> GetByIdAsync(Guid id) => Project(_db.Teams.AsNoTracking().Where(t => t.Id == id)).FirstOrDefaultAsync();

    public async Task<Result<TeamDto>> CreateAsync(CreateTeamRequest r) {
        var bad = await _validator.FirstErrorAsync(r);
        if (bad is not null) return Result<TeamDto>.Fail(Fail.Validation(bad));
        var name = r.Name.Trim();
        if (await _db.Teams.AnyAsync(t => t.Name == name)) return Result<TeamDto>.Fail(Fail.Conflict($"A team named '{name}' already exists."));
        var team = new Team {
            Name = name, ShortName = r.ShortName.Trim().ToUpperInvariant(), Country = r.Country?.Trim() ?? "", LogoUrl = r.LogoUrl,
            PrimaryColor = r.PrimaryColor, SecondaryColor = r.SecondaryColor,
        };
        var ids = r.TournamentIds.Distinct().ToList();
        if (await _db.Tournaments.CountAsync(x => ids.Contains(x.Id)) != ids.Count) return Result<TeamDto>.Fail(Fail.Validation("One or more competitions were not found."));
        _db.Teams.Add(team);
        foreach (var tid in ids) _db.TournamentTeams.Add(new TournamentTeam { TeamId = team.Id, TournamentId = tid });
        await _db.SaveChangesAsync();
        return Result<TeamDto>.Ok((await GetByIdAsync(team.Id))!);
    }

    public async Task<Result<TeamDto>> UpdateAsync(Guid id, CreateTeamRequest r) {
        var bad = await _validator.FirstErrorAsync(r);
        if (bad is not null) return Result<TeamDto>.Fail(Fail.Validation(bad));
        var t = await _db.Teams.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return Result<TeamDto>.Fail(Fail.NotFound("Team not found."));
        var name = r.Name.Trim();
        if (await _db.Teams.AnyAsync(x => x.Name == name && x.Id != id)) return Result<TeamDto>.Fail(Fail.Conflict($"A team named '{name}' already exists."));
        t.Name = name; t.ShortName = r.ShortName.Trim().ToUpperInvariant(); t.Country = r.Country?.Trim() ?? "";
        t.LogoUrl = r.LogoUrl; t.PrimaryColor = r.PrimaryColor; t.SecondaryColor = r.SecondaryColor; t.UpdatedAt = DateTimeOffset.UtcNow;
        var ids = r.TournamentIds.Distinct().ToList();
        if (await _db.Tournaments.CountAsync(x => ids.Contains(x.Id)) != ids.Count) return Result<TeamDto>.Fail(Fail.Validation("One or more competitions were not found."));
        var links = await _db.TournamentTeams.Where(x => x.TeamId == id).ToListAsync();
        _db.TournamentTeams.RemoveRange(links.Where(l => !ids.Contains(l.TournamentId)));
        foreach (var tid in ids.Where(i => links.All(l => l.TournamentId != i))) _db.TournamentTeams.Add(new TournamentTeam { TeamId = id, TournamentId = tid });
        await _db.SaveChangesAsync();
        return Result<TeamDto>.Ok((await GetByIdAsync(id))!);
    }

    /// <summary>Deletes an unused team; a team that appears in any match is deactivated instead.</summary>
    public async Task<Result<bool>> DeleteAsync(Guid id) {
        var t = await _db.Teams.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return Result<bool>.Fail(Fail.NotFound("Team not found."));
        var used = await _db.Matches.AnyAsync(m => m.HomeTeamId == id || m.AwayTeamId == id);
        if (used) { t.IsActive = false; t.UpdatedAt = DateTimeOffset.UtcNow; }
        else {
            var players = await _db.Players.Where(p => p.CurrentTeamId == id).ToListAsync();
            foreach (var p in players) p.CurrentTeamId = null;
            _db.Teams.Remove(t);
        }
        await _db.SaveChangesAsync();
        return Result<bool>.Ok(used);   // true = deactivated, false = removed
    }
}
