// DTOs/Notification/NotificationDto.cs
using System;

namespace WebApplication2.DTOs.Notification
{
    /// <summary>
    /// عرض أساسي للإشعار (للقوائم والجرس)
    /// </summary>
    public class NotificationDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public string? ImageUrl { get; set; }
        public string? ClickUrl { get; set; }
        public DateTime SentAt { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public string? TargetUserId { get; set; }
        public bool IsForAll { get; set; }

        public string SentAtFormatted => SentAt.ToString("yyyy/MM/dd HH:mm");
        public string TimeAgo => GetTimeAgo(SentAt);

        private static string GetTimeAgo(DateTime date)
        {
            var diff = DateTime.Now - date;
            if (diff.TotalMinutes < 1) return "الآن";
            if (diff.TotalMinutes < 60) return $"منذ {(int)diff.TotalMinutes} دقيقة";
            if (diff.TotalHours < 24) return $"منذ {(int)diff.TotalHours} ساعة";
            if (diff.TotalDays < 7) return $"منذ {(int)diff.TotalDays} يوم";
            if (diff.TotalDays < 30) return $"منذ {(int)(diff.TotalDays / 7)} أسبوع";
            if (diff.TotalDays < 365) return $"منذ {(int)(diff.TotalDays / 30)} شهر";
            return $"منذ {(int)(diff.TotalDays / 365)} سنة";
        }
    }
}