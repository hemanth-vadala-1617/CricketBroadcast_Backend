using CricketStream.Application.Common;
using CricketStream.Contracts.Matches;
using CricketStream.Contracts.Scoring;
namespace CricketStream.Application.Features.Scoring;
public interface IScoringService {
    Task<Result<MatchStateDto>> ScoreBallAsync(Guid matchId, ScoreBallRequest request, string userId);
    Task<Result<MatchStateDto>> NewBatterAsync(Guid matchId, NewBatterRequest request);
    Task<Result<MatchStateDto>> NewBowlerAsync(Guid matchId, NewBowlerRequest request);
    Task<Result<MatchStateDto>> SwapStrikeAsync(Guid matchId);
    Task<Result<MatchStateDto>> PowerSurgeAsync(Guid matchId, PowerSurgeRequest request);
    Task<Result<MatchStateDto>> SetSixDistanceAsync(Guid matchId, SixDistanceRequest request);
    Task<Result<MatchStateDto>> RetireAsync(Guid matchId, RetireRequest request, string userId);
    Task<Result<MatchStateDto>> PenaltyAsync(Guid matchId, PenaltyRequest request, string userId);
}
