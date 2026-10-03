using CricketStream.Application.Features.Matches;
using CricketStream.Contracts.Matches;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CricketStream.API.Controllers;

[Route("api/[controller]"), Authorize(Roles = Roles.AnyStaff)]
public class MatchesController : ApiControllerBase {
    private readonly IMatchService _matches;
    public MatchesController(IMatchService matches) { _matches = matches; }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] Guid? tournamentId, [FromQuery] string? search) =>
        Ok(await _matches.ListAsync(status, tournamentId, search));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id) { var m = await _matches.GetAsync(id); return m is null ? NotFound() : Ok(m); }

    [HttpGet("{id:guid}/state")]
    public async Task<IActionResult> State(Guid id) { var s = await _matches.GetStateAsync(id); return s is null ? NotFound() : Ok(s); }

    [HttpPost, Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Create(CreateMatchRequest request) => Created(await _matches.CreateAsync(request), nameof(Get), m => new { id = m.Id });

    [HttpPut("{id:guid}"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Update(Guid id, CreateMatchRequest request) => From(await _matches.UpdateAsync(id, request));

    [HttpDelete("{id:guid}"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(Guid id) { var r = await _matches.DeleteAsync(id); return r.Succeeded ? NoContent() : Problem(r.Error!); }

    [HttpGet("{id:guid}/squads")]
    public async Task<IActionResult> Squads(Guid id) => From(await _matches.GetSquadsAsync(id));

    [HttpPut("{id:guid}/squad"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> SetSquad(Guid id, SetSquadRequest request) => From(await _matches.SetSquadAsync(id, request));

    [HttpPost("{id:guid}/toss"), Authorize(Roles = Roles.Scoring)]
    public async Task<IActionResult> Toss(Guid id, TossRequest request) => From(await _matches.RecordTossAsync(id, request));

    [HttpPost("{id:guid}/start"), Authorize(Roles = Roles.Scoring)]
    public async Task<IActionResult> Start(Guid id) => From(await _matches.StartMatchAsync(id));

    [HttpPost("{id:guid}/innings/start"), Authorize(Roles = Roles.Scoring)]
    public async Task<IActionResult> StartInnings(Guid id, StartInningsRequest request) => From(await _matches.StartInningsAsync(id, request));

    [HttpPost("{id:guid}/innings/end"), Authorize(Roles = Roles.Scoring)]
    public async Task<IActionResult> EndInnings(Guid id, EndInningsRequest request) => From(await _matches.EndInningsAsync(id, request));

    [HttpPost("{id:guid}/complete"), Authorize(Roles = Roles.Scoring)]
    public async Task<IActionResult> Complete(Guid id, CompleteMatchRequest request) => From(await _matches.CompleteAsync(id, request));

    [HttpPut("{id:guid}/win-probability"), Authorize(Roles = Roles.Graphics)]
    public async Task<IActionResult> WinProbability(Guid id, WinProbabilityRequest request) => From(await _matches.SetWinProbabilityAsync(id, request));

    [HttpPost("{id:guid}/clock/advance"), Authorize(Roles = Roles.Scoring)]
    public async Task<IActionResult> AdvanceSession(Guid id) => From(await _matches.AdvanceSessionAsync(id));

    [HttpPut("{id:guid}/clock/overs-lost"), Authorize(Roles = Roles.Scoring)]
    public async Task<IActionResult> OversLost(Guid id, OversLostRequest request) => From(await _matches.SetOversLostAsync(id, request));

    [HttpPost("{id:guid}/pause"), Authorize(Roles = Roles.Scoring)]
    public async Task<IActionResult> Pause(Guid id, [FromQuery] bool rain = true) => From(await _matches.PauseAsync(id, rain));

    [HttpPost("{id:guid}/resume"), Authorize(Roles = Roles.Scoring)]
    public async Task<IActionResult> Resume(Guid id) => From(await _matches.ResumeAsync(id));
}
