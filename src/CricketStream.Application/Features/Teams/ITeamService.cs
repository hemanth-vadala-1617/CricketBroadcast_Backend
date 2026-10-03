using CricketStream.Application.Common;
using CricketStream.Contracts.Teams;
namespace CricketStream.Application.Features.Teams;
public interface ITeamService {
    Task<List<TeamDto>> GetAllAsync(bool includeInactive = false);
    Task<TeamDto?> GetByIdAsync(Guid id);
    Task<Result<TeamDto>> CreateAsync(CreateTeamRequest request);
    Task<Result<TeamDto>> UpdateAsync(Guid id, CreateTeamRequest request);
    Task<Result<bool>> DeleteAsync(Guid id);
}
