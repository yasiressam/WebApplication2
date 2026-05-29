// Models/MapDashboardViewModel.cs
using System;
using System.Collections.Generic;

namespace WebApplication2.Models
{
    public class GovernorateDetail
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int TotalUsers { get; set; }
        public int MaleCount { get; set; }
        public int FemaleCount { get; set; }
        public int AdminCount { get; set; }
        public int SuperAdminCount { get; set; }
        public int IndividualCount { get; set; }
        public int RegularUserCount { get; set; }
        public int AffiliatedCount { get; set; }
        public int TotalIndividualsSectionCount { get; set; }
        public int IraqStudentsOfficeCount { get; set; }
        public int IraqFemaleStudentsOfficeCount { get; set; }
        public int WomenOfficeCount { get; set; }
        public int ProfessionalOfficeCount { get; set; }
        public int SpecializedGatheringsCount { get; set; }
        public int UnionCount { get; set; }
        public int FederationCount { get; set; }
        public int AssociationCount { get; set; }
        public int NgoCount { get; set; }
        public DateTime? LastActivity { get; set; }
        public string ColorClass { get; set; } = "governorate-default";
        public double? CenterX { get; set; }
        public double? CenterY { get; set; }
    }

    public class AffiliationDetail
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }
    public class AffiliationDivisionDetail
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class AffiliationGroupDetail
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class AffiliationSectionDetail
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    // ✅ كلاس لإحصائيات الاتحادات
    public class FederationStat
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    // ✅ كلاس جديد لإحصائيات المؤهل العلمي
    public class EducationStat
    {
        public string EducationName { get; set; } = string.Empty;
        public int Count { get; set; }
        public string Icon { get; set; } = "fas fa-graduation-cap";
        public string Color { get; set; } = "#3498db";
    }

    // ✅ كلاس تفاصيل المستخدمين حسب المؤهل
    public class EducationUserDetail
    {
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Governorate { get; set; } = string.Empty;
        public string Specialization { get; set; } = string.Empty;
        public DateTime? DateOfBirth { get; set; }
        public bool IsPromoted { get; set; }
        public string? Education { get; set; }  // ✅ هذه الخاصية كانت مفقودة
    }

    public class MapDashboardViewModel
    {
        // ===== الإحصائيات العامة =====
        public int TotalUsers { get; set; }
        public int TotalAdmins { get; set; }
        public int TotalSuperAdmins { get; set; }
        public int TotalIndividuals { get; set; }
        public int TotalRegularUsers { get; set; }
        public int TotalAffiliated { get; set; }

        // ===== إحصائيات الجنس =====
        public int TotalMaleIndividuals { get; set; }
        public int TotalFemaleIndividuals { get; set; }

        // ===== إحصائيات العضويات (عدد الأعضاء الفريدين) =====
        public int TotalUnionMembers { get; set; }
        public int TotalFederationMembers { get; set; }
        public int TotalAssociationMembers { get; set; }
        public int TotalNgoMembers { get; set; }

        // ===== عدد أنواع العضويات (من الجداول الرئيسية) =====
        public int TotalUnionTypes { get; set; }
        public int TotalFederationTypes { get; set; }
        public int TotalAssociationTypes { get; set; }
        public int TotalNgoTypes { get; set; }

        // ===== إحصائيات الاتحادات =====
        public List<FederationStat> FederationsStats { get; set; } = new();

        // ===== إحصائيات المحافظات =====
        public int TotalGovernorates { get; set; } = 18;
        public int CoveredGovernorates { get; set; }
        public List<GovernorateDetail> Governorates { get; set; } = new();

        // ===== إحصائيات الانتساب =====
        public List<AffiliationDetail> Affiliations { get; set; } = new();
        public List<AffiliationDivisionDetail> AffiliationDivisions { get; set; } = new();
        public List<AffiliationSectionDetail> AffiliationSections { get; set; } = new();
        public List<AffiliationGroupDetail> AffiliationGroups { get; set; } = new();

        // ===== ✅ إحصائيات المؤهل العلمي =====
        public List<EducationStat> EducationStatistics { get; set; } = new();

        // ===== معلومات إضافية =====
        public DateTime LastUpdated { get; set; } = DateTime.Now;
        public string? AdminDistrictName { get; set; }  // ✅ أضف هذا السطر
        public bool IsManagerScopedView { get; set; }
        public string? ManagerScopeTitle { get; set; }
        public string? ManagerScopeDescription { get; set; }

    }
}
