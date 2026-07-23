using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using WebApplication2.Services;

namespace WebApplication2.Hubs
{
    [Authorize]
    public class NotificationHub : Hub
    {
        private readonly OnlineUsersTracker _onlineUsersTracker;

        public NotificationHub(OnlineUsersTracker onlineUsersTracker)
        {
            _onlineUsersTracker = onlineUsersTracker;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier;
            if (!string.IsNullOrEmpty(userId))
            {
                _onlineUsersTracker.AddConnection(userId, Context.ConnectionId);
                await Groups.AddToGroupAsync(Context.ConnectionId, userId);
                await Groups.AddToGroupAsync(Context.ConnectionId, "All");
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.UserIdentifier;
            if (!string.IsNullOrEmpty(userId))
            {
                _onlineUsersTracker.RemoveConnection(userId, Context.ConnectionId);
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, "All");
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}
