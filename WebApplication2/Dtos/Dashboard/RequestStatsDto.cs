// DTOs/Dashboard/RequestStatsDto.cs
using System.Collections.Generic;

namespace WebApplication2.DTOs.Dashboard
{
    /// <summary>
    /// إحصائيات الطلبات
    /// </summary>
    public class RequestStatsDto
    {
        public int TotalRequests { get; set; }
        public int PendingRequests { get; set; }
        public int UnderReviewRequests { get; set; }
        public int ApprovedRequests { get; set; }
        public int RejectedRequests { get; set; }
        public int ProcessedRequests { get; set; }
        public int UnreadRequests { get; set; }

        // حسب الشهر
        public Dictionary<string, int> RequestsByMonth { get; set; } = new();

        // حسب المستلم
        public Dictionary<string, int> RequestsByRecipient { get; set; } = new();

        // حسب المرسل
        public Dictionary<string, int> RequestsBySender { get; set; } = new();

        // متوسط وقت المعالجة
        public double AverageProcessingTimeHours { get; set; }
    }
}