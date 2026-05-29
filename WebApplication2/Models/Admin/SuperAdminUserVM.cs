namespace WebApplication2.Models
{
    public class SuperAdminUserVM
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Roles { get; set; } = string.Empty;
        public string ResidenceGovernorate { get; set; } = string.Empty;
        public string WorkGovernorate { get; set; } = string.Empty;
        public string WorkDistrict { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string Governorate { get; set; } = string.Empty;
        public string? ManagedGovernorate { get; set; }
        public string? ManagedDistrict { get; set; }

        public bool IsActive { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? CoverImage { get; set; }

        // خصائص للتحقق السريع
        public bool IsAdmin => Roles?.Contains("Admin") ?? false;
        public bool IsSuperAdmin => Roles?.Contains("SuperAdmin") ?? false;
        public bool IsMember => Roles?.Contains("فرد") ?? false;

        // ✅ خصائص الترقية
        public string? PromotionStatus { get; set; }
        public bool RequestedPromotion { get; set; }
        public string? RejectionReason { get; set; }
        public bool HasCompleteProfile { get; set; }
        public int CompletionPercentage { get; set; }
        public string? AccountType { get; set; }
        public int? ProfileId { get; set; }
        public bool IsPromoted { get; set; }

        // ✅ الخصائص الجديدة للمسؤولية الإدارية
        public bool IsManager { get; set; }

        // Entity / Division / Section / Group
        public string ManagementLevel { get; set; } = string.Empty;

        // Manager / Assistant
        public string AssignmentRole { get; set; } = string.Empty;

        // نص عربي جاهز مثل: مسؤول جهة / معاون قسم
        public string ManagementLevelArabic { get; set; } = string.Empty;

        // اسم الجهة / القسم / الشعبة / التجمع
        public string ManagedEntityName { get; set; } = string.Empty;
        public string SearchText { get; set; } = string.Empty;

        // ✅ نص جاهز كامل للعرض
        public string AdministrativeResponsibilityDisplay { get; set; } = string.Empty;

        // ✅ الخصائص الجديدة للعنوان والعمل (اختيارية للعرض في الجدول)
        public string? Area { get; set; }
        public string? Alley { get; set; }
        public string? Street { get; set; }
        public string? House { get; set; }
        public string? JobTitle { get; set; }
        public string? JobGrade { get; set; }
        public string? BadgeNumber { get; set; }

        // ✅ الخصائص الجديدة المطلوبة
        public string Education { get; set; } = string.Empty;
        public string StudyStage { get; set; } = string.Empty;
    }
}
