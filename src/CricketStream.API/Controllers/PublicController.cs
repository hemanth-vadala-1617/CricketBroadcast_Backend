using CricketStream.Application.Features.Matches;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CricketStream.API.Controllers;

/// <summary>Anonymous, read-only: the public match page and the OBS overlay bootstrap.</summary>
[Route("api/public"), AllowAnonymous, EnableRateLimiting("public")]
public class PublicController : ApiControllerBase {
    private readonly IMatchService _matches;
    public PublicController(IMatchService matches) { _matches = matches; }

    [HttpGet("matches")]
    public async Task<IActionResult> Matches([FromQuery] string? status) => Ok(await _matches.ListAsync(status ?? "live", null, null));

    [HttpGet("matches/{id:guid}/state")]
    public async Task<IActionResult> State(Guid id) { var s = await _matches.GetStateAsync(id); return s is null ? NotFound() : Ok(s); }
}
