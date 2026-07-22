using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Text;
using System.Text.RegularExpressions;
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

            await TrySendOneSignalNotification(notification);

            return notification;
        }

        public async Task<Notification> CreateNotificationFromTemplate(string templateKey, string? targetUserId = null, Dictionary<string, string?>? tokens = null, string? icon = null, string? clickUrl = null)
        {
            var settings = await _context.SiteSettings.AsNoTracking().FirstOrDefaultAsync() ?? new SiteSettings();
            var (title, message) = GetTemplate(settings, templateKey);

            title = ReplaceTokens(title, tokens);
            message = ReplaceTokens(message, tokens);

            return await CreateNotification(title, message, targetUserId, icon, clickUrl);
        }

        private async Task TrySendOneSignalNotification(Notification notification)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(notification.TargetUserId))
                {
                    var playerIds = await _context.UserDevices
                        .AsNoTracking()
                        .Where(device =>
                            device.UserId == notification.TargetUserId &&
                            device.IsSubscribed &&
                            !string.IsNullOrWhiteSpace(device.PlayerId))
                        .Select(device => device.PlayerId)
                        .Distinct()
                        .ToListAsync();

                    if (!playerIds.Any())
                    {
                        _logger.LogInformation("No OneSignal devices found for user {UserId}.", notification.TargetUserId);
                        return;
                    }

                    await SendToOneSignal(notification, playerIds);
                    return;
                }

                await SendToOneSignal(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sending OneSignal notification {NotificationId}.", notification.Id);
            }
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
                client.DefaultRequestHeaders.Add("Authorization", $"Key {apiKey}");
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
            var dict1 = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(obj1))
                ?? new Dictionary<string, object>();
            var dict2 = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(obj2))
                ?? new Dictionary<string, object>();

            foreach (var kvp in dict2)
            {
                dict1[kvp.Key] = kvp.Value;
            }

            return dict1;
        }

        private static (string Title, string Message) GetTemplate(SiteSettings settings, string templateKey)
        {
            var defaults = new SiteSettings();
            return templateKey switch
            {
                NotificationTemplateKeys.PromotionApproved => (ValueOrDefault(settings.PromotionApprovedTitle, defaults.PromotionApprovedTitle), ValueOrDefault(settings.PromotionApprovedMessage, defaults.PromotionApprovedMessage)),
                NotificationTemplateKeys.PromotionRejected => (ValueOrDefault(settings.PromotionRejectedTitle, defaults.PromotionRejectedTitle), ValueOrDefault(settings.PromotionRejectedMessage, defaults.PromotionRejectedMessage)),
                NotificationTemplateKeys.BasicInfoApproved => (ValueOrDefault(settings.BasicInfoApprovedTitle, defaults.BasicInfoApprovedTitle), ValueOrDefault(settings.BasicInfoApprovedMessage, defaults.BasicInfoApprovedMessage)),
                NotificationTemplateKeys.BasicInfoRejected => (ValueOrDefault(settings.BasicInfoRejectedTitle, defaults.BasicInfoRejectedTitle), ValueOrDefault(settings.BasicInfoRejectedMessage, defaults.BasicInfoRejectedMessage)),
                NotificationTemplateKeys.DirectAssignment => (ValueOrDefault(settings.DirectAssignmentTitle, defaults.DirectAssignmentTitle), ValueOrDefault(settings.DirectAssignmentMessage, defaults.DirectAssignmentMessage)),
                NotificationTemplateKeys.AssignmentForm => (ValueOrDefault(settings.AssignmentFormTitle, defaults.AssignmentFormTitle), ValueOrDefault(settings.AssignmentFormMessage, defaults.AssignmentFormMessage)),
                NotificationTemplateKeys.AssignmentSubmitted => (ValueOrDefault(settings.AssignmentSubmittedTitle, defaults.AssignmentSubmittedTitle), ValueOrDefault(settings.AssignmentSubmittedMessage, defaults.AssignmentSubmittedMessage)),
                NotificationTemplateKeys.AssignmentApproved => (ValueOrDefault(settings.AssignmentApprovedTitle, defaults.AssignmentApprovedTitle), ValueOrDefault(settings.AssignmentApprovedMessage, defaults.AssignmentApprovedMessage)),
                NotificationTemplateKeys.AssignmentRejected => (ValueOrDefault(settings.AssignmentRejectedTitle, defaults.AssignmentRejectedTitle), ValueOrDefault(settings.AssignmentRejectedMessage, defaults.AssignmentRejectedMessage)),
                NotificationTemplateKeys.AssignmentRemoved => (ValueOrDefault(settings.AssignmentRemovedTitle, defaults.AssignmentRemovedTitle), ValueOrDefault(settings.AssignmentRemovedMessage, defaults.AssignmentRemovedMessage)),
                NotificationTemplateKeys.SuperAdminAssigned => (ValueOrDefault(settings.SuperAdminAssignedTitle, defaults.SuperAdminAssignedTitle), ValueOrDefault(settings.SuperAdminAssignedMessage, defaults.SuperAdminAssignedMessage)),
                NotificationTemplateKeys.AdminAssigned => (ValueOrDefault(settings.AdminAssignedTitle, defaults.AdminAssignedTitle), ValueOrDefault(settings.AdminAssignedMessage, defaults.AdminAssignedMessage)),
                NotificationTemplateKeys.NewsEditorAssigned => (ValueOrDefault(settings.NewsEditorAssignedTitle, defaults.NewsEditorAssignedTitle), ValueOrDefault(settings.NewsEditorAssignedMessage, defaults.NewsEditorAssignedMessage)),
                NotificationTemplateKeys.MapViewerAssigned => (ValueOrDefault(settings.MapViewerAssignedTitle, defaults.MapViewerAssignedTitle), ValueOrDefault(settings.MapViewerAssignedMessage, defaults.MapViewerAssignedMessage)),
                NotificationTemplateKeys.MemberAssigned => (ValueOrDefault(settings.MemberAssignedTitle, defaults.MemberAssignedTitle), ValueOrDefault(settings.MemberAssignedMessage, defaults.MemberAssignedMessage)),
                NotificationTemplateKeys.ProfileUpdated => (ValueOrDefault(settings.ProfileUpdatedTitle, defaults.ProfileUpdatedTitle), ValueOrDefault(settings.ProfileUpdatedMessage, defaults.ProfileUpdatedMessage)),
                _ => ("إشعار جديد", "لديك إشعار جديد")
            };
        }

        private static string ValueOrDefault(string? value, string defaultValue)
        {
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
        }

        private static string ReplaceTokens(string? template, Dictionary<string, string?>? tokens)
        {
            if (string.IsNullOrWhiteSpace(template))
                return string.Empty;

            var result = template;
            if (tokens != null)
            {
                foreach (var token in tokens)
                {
                    result = result.Replace("{" + token.Key + "}", token.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                }
            }

            result = Regex.Replace(result, @"\{[a-zA-Z0-9_]+\}", string.Empty);
            result = Regex.Replace(result, @"[ \t]{2,}", " ");
            result = Regex.Replace(result, @"(\r?\n){3,}", Environment.NewLine + Environment.NewLine);

            return result.Trim();
        }
    }
}
