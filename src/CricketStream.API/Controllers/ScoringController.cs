using CricketStream.Application.Features.Scoring;
using CricketStream.Contracts.Scoring;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CricketStream.API.Controllers;

[Route("api/[controller]"), Authorize(Roles = Roles.Scoring)]
public class ScoringController : ApiControllerBase {
    private readonly IScoringService _scoring;
    public ScoringController(IScoringService scoring) { _scoring = scoring; }

    [HttpPost("match/{matchId:guid}/ball")]
    public async Task<IActionResult> ScoreBall(Guid matchId, ScoreBallRequest request) => From(await _scoring.ScoreBallAsync(matchId, request, UserId));

    [HttpPost("match/{matchId:guid}/new-batter")]
    public async Task<IActionResult> NewBatter(Guid matchId, NewBatterRequest request) => From(await _scoring.NewBatterAsync(matchId, request));

    [HttpPost("match/{matchId:guid}/new-bowler")]
    public async Task<IActionResult> NewBowler(Guid matchId, NewBowlerRequest request) => From(await _scoring.NewBowlerAsync(matchId, request));

    [HttpPost("match/{matchId:guid}/swap-strike")]
    public async Task<IActionResult> SwapStrike(Guid matchId) => From(await _scoring.SwapStrikeAsync(matchId));

    [HttpPost("match/{matchId:guid}/retire")]
    public async Task<IActionResult> Retire(Guid matchId, RetireRequest request) => From(await _scoring.RetireAsync(matchId, request, UserId));

    [HttpPost("match/{matchId:guid}/penalty")]
    public async Task<IActionResult> Penalty(Guid matchId, PenaltyRequest request) => From(await _scoring.PenaltyAsync(matchId, request, UserId));
}
