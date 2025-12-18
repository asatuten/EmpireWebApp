using Microsoft.AspNetCore.SignalR;

namespace EmpireWebApp.Hubs;

public class EmpireHub : Hub
{
    public async Task JoinGameGroup(string code)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(code));
    }

    public static string GroupName(string code) => $"game:{code.ToUpperInvariant()}";
}
