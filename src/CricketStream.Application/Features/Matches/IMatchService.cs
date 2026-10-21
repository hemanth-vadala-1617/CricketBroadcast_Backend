using CricketStream.Application.Common;
using CricketStream.Contracts.Matches;
namespace CricketStream.Application.Features.Matches;
public interface IMatchService {
    Task<List<MatchResponse>> ListAsync(string? status, Guid? tournamentId, string? search);
    Task<MatchResponse?> GetAsync(Guid id);
    Task<MatchStateDto?> GetStateAsync(Guid id);
    Task<Result<MatchResponse>> CreateAsync(CreateMatchRequest request);
    Task<Result<MatchResponse>> UpdateAsync(Guid id, CreateMatchRequest request);
    Task<Result<bool>> DeleteAsync(Guid id);
    Task<Result<List<SquadDto>>> GetSquadsAsync(Guid id);
    Task<Result<MatchStateDto>> SetSquadAsync(Guid id, SetSquadRequest request);
    Task<Result<MatchStateDto>> RecordTossAsync(Guid id, TossRequest request);
    Task<Result<MatchStateDto>> StartMatchAsync(Guid id);
    Task<Result<MatchStateDto>> StartInningsAsync(Guid id, StartInningsRequest request);
    Task<Result<MatchStateDto>> EndInningsAsync(Guid id, EndInningsRequest request);
    Task<Result<MatchStateDto>> CompleteAsync(Guid id, CompleteMatchRequest request);
    Task<Result<MatchStateDto>> SetWinProbabilityAsync(Guid id, WinProbabilityRequest request);
    Task<Result<MatchStateDto>> AdvanceSessionAsync(Guid id);
    Task<Result<MatchStateDto>> SetOversLostAsync(Guid id, OversLostRequest request);
    Task<Result<MatchStateDto>> PauseAsync(Guid id, bool rain);
    Task<Result<MatchStateDto>> ResumeAsync(Guid id);
    Task<Result<MatchStateDto>> StartBreakAsync(Guid id, BreakRequest request);
}
