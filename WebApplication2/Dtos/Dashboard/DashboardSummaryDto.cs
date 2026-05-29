// DTOs/Dashboard/DashboardSummaryDto.cs
using System;
using System.Collections.Generic;

namespace WebApplication2.DTOs.Dashboard
{
    /// <summary>
    /// ملخص عام للوحة التحكم
    /// </summary>
    public class DashboardSummaryDto
    {
        // ========== إحصائيات المستخدمين ==========
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int InactiveUsers { get; set; }
        public int NewUsersThisMonth { get; set; }
        public int NewUsersToday { get; set; }

        // ========== إحصائيات حسب النوع ==========
        public int TotalAdmins { get; set; }
        public int TotalSuperAdmins { get; set; }
        public int TotalMembers { get; set; }
        public int TotalRegularUsers { get; set; }
        public int TotalNewsEditors { get; set; }
        public int TotalMapViewers { get; set; }

        // ========== إحصائيات الملفات ==========
        public int CompletedProfiles { get; set; }
        public int IncompleteProfiles { get; set; }
        public int PendingApproval { get; set; }
        public int RejectedProfiles { get; set; }

        // ========== إحصائيات الطلبات ==========
        public int TotalRequests { get; set; }
        public int PendingRequests { get; set; }
        public int UnderReviewRequests { get; set; }
        public int ApprovedRequests { get; set; }
        public int RejectedRequests { get; set; }
        public int UnreadRequests { get; set; }

        // ========== إحصائيات الترقيات ==========
        public int PromotionRequests { get; set; }
        public int PromotedUsers { get; set; }

        // ========== إحصائيات الأخبار ==========
        public int TotalNews { get; set; }
        public int NewsThisMonth { get; set; }
        public int NewsToday { get; set; }

        // ========== إحصائيات الإشعارات ==========
        public int UnreadNotifications { get; set; }
        public int TotalNotifications { get; set; }

        // ========== تواريخ ==========
        public DateTime LastUpdated { get; set; }
        public DateTime? LastUserActivity { get; set; }

        // ========== نسب مئوية ==========
        public double ProfileCompletionRate => TotalUsers > 0 ? (double)CompletedProfiles / TotalUsers * 100 : 0;
        public double ActiveUsersRate => TotalUsers > 0 ? (double)ActiveUsers / TotalUsers * 100 : 0;
    }
}