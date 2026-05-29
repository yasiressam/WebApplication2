// DTOs/Dashboard/UserStatsDto.cs
using System;

namespace WebApplication2.DTOs.Dashboard
{
    /// <summary>
    /// إحصائيات المستخدمين
    /// </summary>
    public class UserStatsDto
    {
        // ========== إحصائيات عامة ==========
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int InactiveUsers { get; set; }

        // ========== إحصائيات حسب الجنس ==========
        public int MaleCount { get; set; }
        public int FemaleCount { get; set; }

        // ========== إحصائيات حسب الدور ==========
        public int SuperAdminCount { get; set; }
        public int AdminCount { get; set; }
        public int MemberCount { get; set; }
        public int NewsEditorCount { get; set; }
        public int MapViewerCount { get; set; }
        public int RegularUserCount { get; set; }

        // ========== إحصائيات حسب المحافظة ==========
        public Dictionary<string, int> UsersByGovernorate { get; set; } = new();

        // ========== إحصائيات حسب الشهر ==========
        public Dictionary<string, int> NewUsersByMonth { get; set; } = new();

        // ========== إحصائيات الملفات ==========
        public int CompletedProfiles { get; set; }
        public int IncompleteProfiles { get; set; }
        public int PendingApproval { get; set; }

        // ========== إحصائيات الترقيات ==========
        public int PromotionRequests { get; set; }
        public int PromotedUsers { get; set; }
    }
}