using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;

namespace WebApplication2.Hubs
{
    [Authorize]
    public class NotificationHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier;
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, userId);
                await Groups.AddToGroupAsync(Context.ConnectionId, "All");
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var userId = Context.UserIdentifier;
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, "All");
            }
            await base.OnDisconnectedAsync(exception);
        }
    }
}