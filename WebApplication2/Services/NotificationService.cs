using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Text;
using WebApplication2.Data;
using WebApplication2.Models;

namespace WebApplication2.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<NotificationService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public NotificationService(
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger<NotificationService> logger,
            IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<Notification> CreateNotification(string title, string message, string? targetUserId = null, string? icon = null, string? clickUrl = null)
        {
            var notification = new Notification
            {
                Title = title,
                Message = message,
                Icon = icon ?? "bi-bell",
                ClickUrl = clickUrl,
                SentAt = DateTime.Now,
                IsRead = false,
                TargetUserId = targetUserId,
                IsForAll = string.IsNullOrEmpty(targetUserId)
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
            return notification;
        }

        public async Task<List<Notification>> GetUserNotifications(string userId)
        {
            return await _context.Notifications
                .Where(n => n.IsForAll || n.TargetUserId == userId)
                .OrderByDescending(n => n.SentAt)
                .Take(50)
                .ToListAsync();
        }

        public async Task<int> GetUnreadCount(string userId)
        {
            return await _context.Notifications
                .Where(n => (n.IsForAll || n.TargetUserId == userId) && !n.IsRead)
                .CountAsync();
        }

        public async Task<bool> MarkAsRead(int notificationId, string userId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && (n.IsForAll || n.TargetUserId == userId));

            if (notification == null)
                return false;

            notification.IsRead = true;
            notification.ReadAt = DateTime.Now;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> MarkAllAsRead(string userId)
        {
            var notifications = await _context.Notifications
                .Where(n => (n.IsForAll || n.TargetUserId == userId) && !n.IsRead)
                .ToListAsync();

            foreach (var notification in notifications)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteNotification(int notificationId, string userId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.TargetUserId == userId);

            if (notification == null)
                return false;

            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> SendToOneSignal(Notification notification, List<string>? playerIds = null, string? externalUserId = null)
        {
            try
            {
                var appId = _configuration["OneSignal:AppId"];
                var apiKey = _configuration["OneSignal:ApiKey"];

                if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogError("❌ OneSignal settings are missing");
                    return false;
                }

                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("Authorization", $"Basic {apiKey}");
                client.DefaultRequestHeaders.Add("Accept", "application/json");

                object targetAudience;

                if (playerIds != null && playerIds.Any())
                {
                    targetAudience = new { include_player_ids = playerIds };
                }
                else if (!string.IsNullOrEmpty(externalUserId))
                {
                    targetAudience = new { include_external_user_ids = new[] { externalUserId } };
                }
                else
                {
                    targetAudience = new { included_segments = new[] { "All" } };
                }

                var payload = new
                {
                    app_id = appId,
                    headings = new { en = notification.Title, ar = notification.Title },
                    contents = new { en = notification.Message, ar = notification.Message },
                    data = new
                    {
                        notificationId = notification.Id,
                        clickUrl = notification.ClickUrl,
                        icon = notification.Icon
                    },
                    url = notification.ClickUrl
                };

                var finalPayload = MergeObjects(payload, targetAudience);
                var json = JsonConvert.SerializeObject(finalPayload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("https://onesignal.com/api/v1/notifications", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"✅ OneSignal notification sent");
                    return true;
                }
                else
                {
                    _logger.LogError($"❌ OneSignal error: {responseContent}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error sending OneSignal notification");
                return false;
            }
        }

        private object MergeObjects(object obj1, object obj2)
        {
            var dict1 = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(obj1));
            var dict2 = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(obj2));

            foreach (var kvp in dict2)
            {
                dict1[kvp.Key] = kvp.Value;
            }

            return dict1;
        }
    }
}