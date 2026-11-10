using Microsoft.AspNetCore.SignalR;

namespace TickPulse.Api.Hubs;

// SignalR hub for live ticks. Clients join a group per ticker they watch,
// or the "all" group for the full feed. The engine pushes ticks and
// analyst events through the hub context, clients never call methods
// except to subscribe and unsubscribe.
public sealed class MarketHub : Hub
{
    public async Task Subscribe(string ticker)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"ticker:{ticker.ToUpperInvariant()}");
    }

    public async Task Unsubscribe(string ticker)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"ticker:{ticker.ToUpperInvariant()}");
    }

    public async Task SubscribeAll()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "all");
    }
}
