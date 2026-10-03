using CricketStream.Application.Common;
using CricketStream.Contracts.Master;
using CricketStream.Contracts.Players;
namespace CricketStream.Application.Features.Master;

/// <summary>CRUD for the reference data the scorer needs before a match. Deletes that would orphan history
/// either deactivate (players) or are refused (venues, rules, tournaments, themes in use).</summary>
public interface IMasterDataService {
    Task<List<PlayerDto>> ListPlayersAsync(string? search, Guid? teamId, bool includeInactive);
    Task<PlayerDto?> GetPlayerAsync(Guid id);
    Task<Result<PlayerDto>> SavePlayerAsync(Guid? id, PlayerDto dto);
    Task<Result<bool>> DeletePlayerAsync(Guid id);

    Task<List<VenueDto>> ListVenuesAsync(string? search);
    Task<Result<VenueDto>> SaveVenueAsync(Guid? id, VenueDto dto);
    Task<Result<bool>> DeleteVenueAsync(Guid id);

    Task<List<TournamentDto>> ListTournamentsAsync(string? search);
    Task<Result<TournamentDto>> SaveTournamentAsync(Guid? id, TournamentDto dto);
    Task<Result<bool>> DeleteTournamentAsync(Guid id);

    Task<List<MatchRulesDto>> ListRulesAsync();
    Task<Result<MatchRulesDto>> SaveRulesAsync(Guid? id, MatchRulesDto dto);
    Task<Result<bool>> DeleteRulesAsync(Guid id);

    Task<List<OverlayThemeDto>> ListThemesAsync();
    Task<Result<OverlayThemeDto>> SaveThemeAsync(Guid? id, OverlayThemeDto dto);
    Task<Result<bool>> DeleteThemeAsync(Guid id);
}
