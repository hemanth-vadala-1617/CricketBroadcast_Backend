using CricketStream.Application.Common;
using CricketStream.Contracts.Matches;
using CricketStream.Contracts.Revisions;
namespace CricketStream.Application.Features.Revisions;
public interface IRevisionService {
    Task<Result<MatchStateDto>> UndoLastBallAsync(Guid matchId, UndoBallRequest request, string userId);
    Task<Result<MatchStateDto>> EditLastBallAsync(Guid matchId, EditBallRequest request, string userId);
    Task<Result<MatchStateDto>> CancelInningsStartAsync(Guid matchId, string userId);
}
