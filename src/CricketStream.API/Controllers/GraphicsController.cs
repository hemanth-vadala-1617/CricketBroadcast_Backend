using CricketStream.Application.Features.Graphics;
using CricketStream.Contracts.Graphics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CricketStream.API.Controllers;

[Route("api/graphics"), Authorize(Roles = Roles.Graphics)]
public class GraphicsController : ApiControllerBase {
    private readonly IGraphicsService _graphics;
    public GraphicsController(IGraphicsService graphics) { _graphics = graphics; }

    [HttpPut("match/{matchId:guid}/{key}")]
    public async Task<IActionResult> Set(Guid matchId, string key, SetGraphicRequest request) => From(await _graphics.SetAsync(matchId, key, request));
}
