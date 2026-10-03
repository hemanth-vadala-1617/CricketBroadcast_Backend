using CricketStream.Application.Features.Revisions;
using CricketStream.Contracts.Revisions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CricketStream.API.Controllers;

[Route("api/[controller]"), Authorize(Roles = Roles.Scoring)]
public class RevisionsController : ApiControllerBase {
    private readonly IRevisionService _revisions;
    public RevisionsController(IRevisionService revisions) { _revisions = revisions; }

    [HttpPost("match/{matchId:guid}/undo")]
    public async Task<IActionResult> Undo(Guid matchId, UndoBallRequest request) => From(await _revisions.UndoLastBallAsync(matchId, request, UserId));

    [HttpPut("match/{matchId:guid}/last-ball")]
    public async Task<IActionResult> EditLast(Guid matchId, EditBallRequest request) => From(await _revisions.EditLastBallAsync(matchId, request, UserId));

    [HttpPost("match/{matchId:guid}/cancel-innings")]
    public async Task<IActionResult> CancelInnings(Guid matchId) => From(await _revisions.CancelInningsStartAsync(matchId, UserId));
}
