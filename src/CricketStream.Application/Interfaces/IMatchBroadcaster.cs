using CricketStream.Contracts.Matches;
namespace CricketStream.Application.Interfaces;
public interface IMatchBroadcaster {
    /// <summary>The only realtime message: the full, versioned match state.</summary>
    Task BroadcastStateAsync(Guid matchId, MatchStateDto state);
}
