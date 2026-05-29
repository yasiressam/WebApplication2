// DTOs/Dashboard/MapDashboardDto.cs
using System;
using System.Collections.Generic;

namespace WebApplication2.DTOs.Dashboard
{
    /// <summary>
    /// بيانات خريطة العراق التفاعلية
    /// </summary>
    public class MapDashboardDto
    {
        public List<GovernorateStatsDto> Governorates { get; set; } = new();
        public int TotalUsers { get; set; }
        public int TotalAdmins { get; set; }
        public int TotalSuperAdmins { get; set; }
        public int TotalMembers { get; set; }
        public int TotalGovernorates { get; set; } = 18;
        public int CoveredGovernorates { get; set; }

        // إحصائيات العضويات
        public int TotalAffiliated { get; set; }
        public int TotalUnionMembers { get; set; }
        public int TotalFederationMembers { get; set; }
        public int TotalAssociationMembers { get; set; }
        public int TotalNgoMembers { get; set; }

        public DateTime LastUpdated { get; set; }

        // قوائم جهات الانتساب
        public List<AffiliationStatsDto> Affiliations { get; set; } = new();
    }

    /// <summary>
    /// إحصائيات جهات الانتساب
    /// </summary>
    public class AffiliationStatsDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}