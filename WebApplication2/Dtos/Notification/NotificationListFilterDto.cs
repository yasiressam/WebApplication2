// DTOs/Notification/NotificationListFilterDto.cs
using System;

namespace WebApplication2.DTOs.Notification
{
    /// <summary>
    /// فلترة وترتيب قائمة الإشعارات
    /// </summary>
    public class NotificationListFilterDto
    {
        public string? UserId { get; set; }                // فلتر بواسطة المستخدم
        public string? SearchTerm { get; set; }            // بحث بالعنوان أو الرسالة
        public bool? IsRead { get; set; }                  // فلتر بالقراءة
        public bool? IsForAll { get; set; }                // فلتر بالإشعارات العامة
        public DateTime? FromDate { get; set; }            // من تاريخ
        public DateTime? ToDate { get; set; }              // إلى تاريخ
        public string? SortBy { get; set; } = "SentAt";    // الترتيب
        public bool SortDescending { get; set; } = true;   // تنازلي
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    /// <summary>
    /// نتيجة البحث (مع معلومات الصفحات)
    /// </summary>
    public class PagedNotificationResult
    {
        public List<NotificationDto> Notifications { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;

        // إحصائيات إضافية
        public int UnreadCount { get; set; }
        public int ReadCount { get; set; }
    }
}