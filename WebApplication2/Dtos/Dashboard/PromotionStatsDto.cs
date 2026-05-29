// DTOs/Dashboard/PromotionStatsDto.cs
using System.Collections.Generic;

namespace WebApplication2.DTOs.Dashboard
{
    /// <summary>
    /// إحصائيات طلبات الترقية
    /// </summary>
    public class PromotionStatsDto
    {
        public int TotalRequests { get; set; }
        public int PendingRequests { get; set; }
        public int ApprovedRequests { get; set; }
        public int RejectedRequests { get; set; }

        public List<PromotionRequestSummaryDto> RecentRequests { get; set; } = new();
        public Dictionary<string, int> RequestsByGovernorate { get; set; } = new();
        public Dictionary<string, int> RequestsByAccountType { get; set; } = new();
    }

    /// <summary>
    /// ملخص طلب ترقية
    /// </summary>
    public class PromotionRequestSummaryDto
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Governorate { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
        public DateTime RequestDate { get; set; }
        public int CompletionPercentage { get; set; }
        public string? RejectionReason { get; set; }
    }
}