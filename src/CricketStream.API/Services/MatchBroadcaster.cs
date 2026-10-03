using CricketStream.API.Hubs;
using CricketStream.Application.Interfaces;
using CricketStream.Contracts.Matches;
using Microsoft.AspNetCore.SignalR;

namespace CricketStream.API.Services;

public class MatchBroadcaster : IMatchBroadcaster {
    private readonly IHubContext<MatchHub> _hub;
    public MatchBroadcaster(IHubContext<MatchHub> hub) { _hub = hub; }
    public Task BroadcastStateAsync(Guid matchId, MatchStateDto state) =>
        _hub.Clients.Group(MatchHub.Group(matchId)).SendAsync("MatchStateUpdated", state);
}
