// ملف: Models/SuperAdminUserDetailsVM.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace WebApplication2.Models
{
    public class SuperAdminUserDetailsVM
    {
        // ========== معلومات الحساب ==========
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string WhatsAppNumber { get; set; } = string.Empty;
        public bool IsWhatsAppVerified { get; set; }
        public DateTime? WhatsAppVerifiedAt { get; set; }
        public bool IsActive { get; set; }
        public string Roles { get; set; } = string.Empty;

        // ========== المعلومات الشخصية ==========
        public string FullName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string MotherName { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string MozakeName { get; set; } = string.Empty;
        public string Education { get; set; } = string.Empty;
        public string Specialization { get; set; } = string.Empty;

        // ========== الوثائق والأرقام الرسمية ==========
        public string IdentityCardN { get; set; } = string.Empty;
        public DateTime IdentityDate { get; set; }

        // ========== معلومات العنوان ==========
        public Address? Address { get; set; }

        // ========== المحافظة / القضاء المُدار ==========
        public string? ManagedGovernorate { get; set; }
        public string? ManagedDistrict { get; set; }

        // ========== الصورة الشخصية ==========
        public string? CoverImage { get; set; }

        // ========== تواريخ مهمة ==========
        public DateTime CreatedAt { get; set; }
        public DateTime? AffiliationDate { get; set; }
        public DateTime? RegistrationDate { get; set; }

        // ========== معلومات العمل ==========
        public string? EmploymentStatus { get; set; }
        public string? Work { get; set; }
        public string? Ministry { get; set; }
        public string? Department { get; set; }
        public string? Position { get; set; }
        public string? JobTitle { get; set; }
        public string? JobGrade { get; set; }
        public string? WorkGovernorate { get; set; }
        public string? WorkDistrict { get; set; }

        // ========== حالة طلب الترقية ==========
        public bool RequestedPromotion { get; set; }
        public DateTime? RequestedPromotionDate { get; set; }
        public string? RejectionReason { get; set; }
        public string? AccountType { get; set; }
        public bool IsPromoted { get; set; }
        public DateTime? PromotionDate { get; set; }
        public string? PromotedBy { get; set; }

        // ========== معلومات الانتساب ==========
        public string? AffiliationEntity { get; set; }
        public string? Division { get; set; }
        public string? Section { get; set; }
        public string? Group { get; set; }
        public string? AffiliationMozakeName { get; set; }
        public string? MozakePhoneNumber { get; set; }
        public string? BadgeNumber { get; set; }
        public DateTime? AffiliationEntryDate { get; set; }

        // ========== معلومات النقابة ==========
        public string? UnionName { get; set; }
        public string? UnionPosition { get; set; }
        public string? UnionIdNumber { get; set; }
        public DateTime? UnionAffiliationDate { get; set; }
        public DateTime? UnionExpiryDate { get; set; }

        // ========== معلومات الاتحاد ==========
        public string? FederationName { get; set; }
        public string? FederationDivisionName { get; set; }
        public string? FederationSectionName { get; set; }
        public string? FederationGroupName { get; set; }
        public string? FederationPosition { get; set; }
        public string? FederationIdNumber { get; set; }
        public DateTime? FederationAffiliationDate { get; set; }
        public DateTime? FederationExpiryDate { get; set; }

        // ========== معلومات الجمعية ==========
        public string? AssociationName { get; set; }
        public string? AssociationPosition { get; set; }
        public string? AssociationIdNumber { get; set; }
        public DateTime? AssociationAffiliationDate { get; set; }
        public DateTime? AssociationExpiryDate { get; set; }

        // ========== معلومات المنظمة ==========
        public string? NgoName { get; set; }
        public string? NgoPosition { get; set; }
        public string? NgoIdNumber { get; set; }
        public DateTime? NgoAffiliationDate { get; set; }
        public DateTime? NgoExpiryDate { get; set; }

        // ========== معلومات بطاقة الناخب ==========
        public string? VoterCardNumber { get; set; }
        public string? PollingCenterNumber { get; set; }

        // ========== معلومات إضافية ==========
        public string? MaritalStatus { get; set; }
        public string? UniversityType { get; set; }
        public string? InstitutionType { get; set; }
        public string? InstitutionName { get; set; }
        public string? FacultyDepartment { get; set; }
        public string? StudyType { get; set; }
        public string? StudyStage { get; set; }

        // ========== معلومات المسؤولية الإدارية القديمة ==========
        public string? ManagedAffiliationEntity { get; set; }
        public string? ManagedDivision { get; set; }
        public string? ManagedSection { get; set; }
        public string? ManagedGroup { get; set; }

        // ========== التعيينات الإدارية ==========
        public List<ManagementAssignmentDisplayVM> ManagementAssignments { get; set; } = new();

        public bool IsManager => ManagementAssignments.Any();

        public string ManagerSummary => GetManagerSummary();

        private string GetManagerSummary()
        {
            if (!ManagementAssignments.Any())
                return "لا يوجد";

            return string.Join("، ", ManagementAssignments.Select(x => x.Description));
        }
    }

    public class ManagementAssignmentDisplayVM
    {
        public int Id { get; set; }

        // Entity / Division / Section / Group
        public string Level { get; set; } = string.Empty;

        // Manager / Assistant
        public string AssignmentRole { get; set; } = string.Empty;

        // مسؤول جهة / معاون قسم / ...
        public string LevelArabic { get; set; } = string.Empty;

        public string Governorate { get; set; } = string.Empty;

        public string? EntityName { get; set; }
        public string? DivisionName { get; set; }
        public string? SectionName { get; set; }
        public string? GroupName { get; set; }

        public DateTime CreatedAt { get; set; }

        public string ManagedEntityFullName
        {
            get
            {
                return Level switch
                {
                    "Entity" => EntityName ?? "",
                    "Division" => DivisionName ?? "",
                    "Section" => SectionName ?? "",
                    "Group" => GroupName ?? "",
                    _ => ""
                };
            }
        }

        public string Description
        {
            get
            {
                string managedName = ManagedEntityFullName;

                if (!string.IsNullOrWhiteSpace(managedName))
                    return $"{LevelArabic} - {managedName} - {Governorate}";

                return $"{LevelArabic} - {Governorate}";
            }
        }
    }
}
