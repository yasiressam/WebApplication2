using WebApplication2.Models;

namespace WebApplication2.Services
{
    public interface INotificationService
    {
        Task<Notification> CreateNotification(string title, string message, string? targetUserId = null, string? icon = null, string? clickUrl = null);
        Task<List<Notification>> GetUserNotifications(string userId);
        Task<int> GetUnreadCount(string userId);
        Task<bool> MarkAsRead(int notificationId, string userId);
        Task<bool> MarkAllAsRead(string userId);
        Task<bool> DeleteNotification(int notificationId, string userId);
        Task<bool> SendToOneSignal(Notification notification, List<string>? playerIds = null, string? externalUserId = null);
    }
}