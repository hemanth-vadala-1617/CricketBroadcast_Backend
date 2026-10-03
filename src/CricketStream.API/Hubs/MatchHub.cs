using CricketStream.Application.Features.Matches;
using Microsoft.AspNetCore.SignalR;

namespace CricketStream.API.Hubs;

/// <summary>Read-only realtime channel. Every mutation goes through authorised REST endpoints, so anyone
/// (OBS overlay, public page) may listen, and nobody can change the match from here.</summary>
public class MatchHub : Hub {
    private readonly IMatchStateService _state;
    public MatchHub(IMatchStateService state) { _state = state; }

    public static string Group(Guid matchId) => $"match:{matchId:N}";

    /// <summary>Joins the match group and immediately replies with the current state (also used to resync after a reconnect).</summary>
    public async Task JoinMatch(Guid matchId) {
        var state = await _state.GetAsync(matchId);
        if (state is null) throw new HubException("Match not found.");
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(matchId));
        await Clients.Caller.SendAsync("MatchStateUpdated", state);
    }

    public Task LeaveMatch(Guid matchId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(matchId));
}
