// DTOs/Notification/NotificationCountDto.cs
namespace WebApplication2.DTOs.Notification
{
    /// <summary>
    /// عدد الإشعارات (للعرض في الجرس)
    /// </summary>
    public class NotificationCountDto
    {
        public int UnreadCount { get; set; }
        public int TotalCount { get; set; }
        public bool HasUnread => UnreadCount > 0;
    }
}