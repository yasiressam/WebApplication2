// DTOs/User/UserProfileDto.cs
using System;
using System.Collections.Generic;

namespace WebApplication2.DTOs.User
{
    /// <summary>
    /// الملف الشخصي الكامل للمستخدم (لصفحة التفاصيل)
    /// </summary>
    public class UserProfileDto
    {
        // ========== معلومات الحساب ==========
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public List<string> Roles { get; set; } = new();

        // ========== المعلومات الشخصية (من Identify) ==========
        public string FullName { get; set; } = string.Empty;
        public string? LastName { get; set; }
        public string MotherName { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string? MozakeName { get; set; }
        public string? Education { get; set; }
        public string? Specialization { get; set; }
        public string? CoverImage { get; set; }

        // ========== الحالة الاجتماعية ==========
        public string? MaritalStatus { get; set; }

        // ========== معلومات الطالب الجامعي ==========
        public string? UniversityType { get; set; }
        public string? InstitutionType { get; set; }
        public string? InstitutionName { get; set; }
        public string? FacultyDepartment { get; set; }
        public string? StudyType { get; set; }
        public string? StudyStage { get; set; }

        // ========== الوثائق والأرقام الرسمية ==========
        public int IdentityCardN { get; set; }
        public DateTime IdentityDate { get; set; }
        public int? RationN { get; set; }
        public int? RationCenter { get; set; }

        // ========== بطاقة الناخب ==========
        public string? VoterCardNumber { get; set; }
        public string? PollingCenterNumber { get; set; }

        // ========== العنوان ==========
        public AddressDto? Address { get; set; }

        // ========== معلومات العمل ==========
        public string? EmploymentStatus { get; set; }
        public string? Work { get; set; }
        public string? Ministry { get; set; }
        public string? Department { get; set; }
        public string? Position { get; set; }

        // ========== معلومات الانتساب ==========
        public AffiliationInfoDto? Affiliation { get; set; }

        // ========== العضويات ==========
        public List<MembershipDto> Memberships { get; set; } = new();

        // ========== حالة المستخدم ==========
        public string AccountType { get; set; } = string.Empty;
        public string? ManagedGovernorate { get; set; }

        // اعتماد البيانات الأساسية
        public bool IsBasicInfoApproved { get; set; }
        public DateTime? BasicInfoApprovalDate { get; set; }
        public string? BasicInfoApprovedBy { get; set; }
        public string? BasicInfoRejectionReason { get; set; }

        // حالة الترقية
        public bool IsPromoted { get; set; }
        public DateTime? PromotionDate { get; set; }
        public string? PromotedBy { get; set; }

        // طلب الترقية
        public bool RequestedPromotion { get; set; }
        public DateTime? RequestedPromotionDate { get; set; }
        public string? PromotionRequestNotes { get; set; }
        public string? RejectionReason { get; set; }

        // ========== تواريخ ==========
        public DateTime CreatedAt { get; set; }
        public DateTime? AffiliationDate { get; set; }
        public DateTime? RegistrationDate { get; set; }

        // ========== إحصائيات ==========
        public int CompletionPercentage { get; set; }
        public bool HasCompleteProfile { get; set; }
    }

    /// <summary>
    /// معلومات العنوان
    /// </summary>
    public class AddressDto
    {
        public string? Governorate { get; set; }
        public string? District { get; set; }
        public string? SubDistrict { get; set; }
        public string? Alley { get; set; }
        public string? Street { get; set; }
        public string? House { get; set; }
        public string? NearestPoint { get; set; }
    }

    /// <summary>
    /// معلومات الانتساب
    /// </summary>
    public class AffiliationInfoDto
    {
        public string? AffiliationEntity { get; set; }
        public string? Division { get; set; }
        public string? Section { get; set; }
        public string? Group { get; set; }
        public string? MozakeName { get; set; }
        public string? MozakePhoneNumber { get; set; }
        public string? BadgeNumber { get; set; }
        public DateTime? AffiliationDate { get; set; }
    }

    /// <summary>
    /// معلومات العضوية (نقابة، اتحاد، جمعية، منظمة)
    /// </summary>
    public class MembershipDto
    {
        public string Type { get; set; } = string.Empty; // Union, Federation, Association, NGO
        public string Name { get; set; } = string.Empty;
        public string? Position { get; set; }
        public string? IdNumber { get; set; }
        public DateTime? AffiliationDate { get; set; }
    }
}