// DTOs/Dashboard/GovernorateStatsDto.cs
using System;
using System.Collections.Generic;

namespace WebApplication2.DTOs.Dashboard
{
    /// <summary>
    /// إحصائيات المحافظة
    /// </summary>
    public class GovernorateStatsDto
    {
        public string Name { get; set; } = string.Empty;
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int MaleCount { get; set; }
        public int FemaleCount { get; set; }
        public int AdminCount { get; set; }
        public int MemberCount { get; set; }
        public int RegularUserCount { get; set; }
        public int CompletedProfiles { get; set; }
        public int IncompleteProfiles { get; set; }
        public int PromotionRequests { get; set; }
        public DateTime? LastActivity { get; set; }

        // إحداثيات الخريطة
        public double? CenterX { get; set; }
        public double? CenterY { get; set; }

        // لون المحافظة على الخريطة
        public string ColorClass { get; set; } = "governorate-default";

        // نسبة اكتمال الملفات
        public double CompletionRate => TotalUsers > 0 ? (double)CompletedProfiles / TotalUsers * 100 : 0;
    }
}