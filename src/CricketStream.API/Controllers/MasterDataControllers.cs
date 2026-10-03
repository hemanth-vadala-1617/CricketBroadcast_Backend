using CricketStream.Application.Features.Master;
using CricketStream.Contracts.Master;
using CricketStream.Contracts.Players;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CricketStream.API.Controllers;

[Route("api/players"), Authorize(Roles = Roles.AnyStaff)]
public class PlayersController : ApiControllerBase {
    private readonly IMasterDataService _svc;
    public PlayersController(IMasterDataService svc) { _svc = svc; }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] Guid? teamId, [FromQuery] bool includeInactive = false) =>
        Ok(await _svc.ListPlayersAsync(search, teamId, includeInactive));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id) { var p = await _svc.GetPlayerAsync(id); return p is null ? NotFound() : Ok(p); }

    [HttpPost, Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Create(PlayerDto dto) => Created(await _svc.SavePlayerAsync(null, dto), nameof(Get), p => new { id = p.Id });

    [HttpPut("{id:guid}"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Update(Guid id, PlayerDto dto) => From(await _svc.SavePlayerAsync(id, dto));

    [HttpDelete("{id:guid}"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(Guid id) {
        var r = await _svc.DeletePlayerAsync(id);
        return r.Succeeded ? Ok(new { deactivated = r.Value }) : Problem(r.Error!);
    }
}

[Route("api/venues"), Authorize(Roles = Roles.AnyStaff)]
public class VenuesController : ApiControllerBase {
    private readonly IMasterDataService _svc;
    public VenuesController(IMasterDataService svc) { _svc = svc; }
    [HttpGet] public async Task<IActionResult> List([FromQuery] string? search) => Ok(await _svc.ListVenuesAsync(search));
    [HttpPost, Authorize(Roles = Roles.Admin)] public async Task<IActionResult> Create(VenueDto dto) => From(await _svc.SaveVenueAsync(null, dto));
    [HttpPut("{id:guid}"), Authorize(Roles = Roles.Admin)] public async Task<IActionResult> Update(Guid id, VenueDto dto) => From(await _svc.SaveVenueAsync(id, dto));
    [HttpDelete("{id:guid}"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(Guid id) { var r = await _svc.DeleteVenueAsync(id); return r.Succeeded ? NoContent() : Problem(r.Error!); }
}

[Route("api/tournaments"), Authorize(Roles = Roles.AnyStaff)]
public class TournamentsController : ApiControllerBase {
    private readonly IMasterDataService _svc;
    public TournamentsController(IMasterDataService svc) { _svc = svc; }
    [HttpGet] public async Task<IActionResult> List([FromQuery] string? search) => Ok(await _svc.ListTournamentsAsync(search));
    [HttpPost, Authorize(Roles = Roles.Admin)] public async Task<IActionResult> Create(TournamentDto dto) => From(await _svc.SaveTournamentAsync(null, dto));
    [HttpPut("{id:guid}"), Authorize(Roles = Roles.Admin)] public async Task<IActionResult> Update(Guid id, TournamentDto dto) => From(await _svc.SaveTournamentAsync(id, dto));
    [HttpDelete("{id:guid}"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(Guid id) { var r = await _svc.DeleteTournamentAsync(id); return r.Succeeded ? NoContent() : Problem(r.Error!); }
}

[Route("api/match-rules"), Authorize(Roles = Roles.AnyStaff)]
public class MatchRulesController : ApiControllerBase {
    private readonly IMasterDataService _svc;
    public MatchRulesController(IMasterDataService svc) { _svc = svc; }
    [HttpGet] public async Task<IActionResult> List() => Ok(await _svc.ListRulesAsync());
    [HttpPost, Authorize(Roles = Roles.Admin)] public async Task<IActionResult> Create(MatchRulesDto dto) => From(await _svc.SaveRulesAsync(null, dto));
    [HttpPut("{id:guid}"), Authorize(Roles = Roles.Admin)] public async Task<IActionResult> Update(Guid id, MatchRulesDto dto) => From(await _svc.SaveRulesAsync(id, dto));
    [HttpDelete("{id:guid}"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(Guid id) { var r = await _svc.DeleteRulesAsync(id); return r.Succeeded ? NoContent() : Problem(r.Error!); }
}

[Route("api/themes"), Authorize(Roles = Roles.AnyStaff)]
public class ThemesController : ApiControllerBase {
    private readonly IMasterDataService _svc;
    public ThemesController(IMasterDataService svc) { _svc = svc; }
    [HttpGet] public async Task<IActionResult> List() => Ok(await _svc.ListThemesAsync());
    [HttpPost, Authorize(Roles = Roles.Admin)] public async Task<IActionResult> Create(OverlayThemeDto dto) => From(await _svc.SaveThemeAsync(null, dto));
    [HttpPut("{id:guid}"), Authorize(Roles = Roles.Admin)] public async Task<IActionResult> Update(Guid id, OverlayThemeDto dto) => From(await _svc.SaveThemeAsync(id, dto));
    [HttpDelete("{id:guid}"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(Guid id) { var r = await _svc.DeleteThemeAsync(id); return r.Succeeded ? NoContent() : Problem(r.Error!); }
}
