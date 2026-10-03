using CricketStream.Application.Features.Teams;
using CricketStream.Contracts.Teams;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CricketStream.API.Controllers;

[Route("api/[controller]"), Authorize(Roles = Roles.AnyStaff)]
public class TeamsController : ApiControllerBase {
    private readonly ITeamService _teams;
    public TeamsController(ITeamService teams) { _teams = teams; }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false) => Ok(await _teams.GetAllAsync(includeInactive));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id) {
        var t = await _teams.GetByIdAsync(id);
        return t is null ? NotFound() : Ok(t);
    }

    [HttpPost, Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Create(CreateTeamRequest request) => Created(await _teams.CreateAsync(request), nameof(Get), t => new { id = t.Id });

    [HttpPut("{id:guid}"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Update(Guid id, CreateTeamRequest request) => From(await _teams.UpdateAsync(id, request));

    /// <summary>Removes an unused team, or deactivates one that appears in a match (deactivated = true).</summary>
    [HttpDelete("{id:guid}"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(Guid id) {
        var r = await _teams.DeleteAsync(id);
        return r.Succeeded ? Ok(new { deactivated = r.Value }) : Problem(r.Error!);
    }
}
