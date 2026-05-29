using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

using WebApplication2.Models.Profile;

namespace WebApplication2.Models.ViewModels
{
    /// <summary>
    /// نموذج تعديل الملف الشخصي الكامل بواسطة السوبر أدمن
    /// </summary>
    public class AdminEditProfileViewModel
    {
        // ========== معلومات الحساب ==========
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string UserRole { get; set; } = string.Empty;

        // ========== علامة للتعديل بواسطة الأدمن ==========
        public bool IsAdminEditing { get; set; } = true;

        // ========== معلومات المستخدم المستهدف ==========
        public string TargetUserId { get; set; } = string.Empty;
        public string TargetUserEmail { get; set; } = string.Empty;
        public string TargetUserRole { get; set; } = string.Empty;

        // ========== حالة المستخدم ==========
        [Display(Name = "نوع الحساب")]
        public string? AccountType { get; set; }
        public bool IsPromoted { get; set; } = false;
        public DateTime? PromotionDate { get; set; }
        public string? PromotedBy { get; set; }

        // ========== حالة طلب الترقية ==========
        public bool RequestedPromotion { get; set; } = false;
        public DateTime? RequestedPromotionDate { get; set; }
        public string? PromotionRequestNotes { get; set; }
        public string? RejectionReason { get; set; }

        // ========== الصورة الشخصية ==========
        public IFormFile? CoverImageFile { get; set; }
        public string? ExistingCoverImage { get; set; }

        // ========== الأقسام الرئيسية (نفس الـ DTOs الموجودة) ==========
        public PersonalInfoViewModel PersonalInfo { get; set; } = new();
        public AddressViewModel Address { get; set; } = new();
        public DocumentsViewModel Documents { get; set; } = new();
        public EmploymentViewModel Employment { get; set; } = new();
        public AffiliationViewModel Affiliation { get; set; } = new();
        public MembershipViewModel Memberships { get; set; } = new();

        // ========== وثائق إضافية ==========
        [Display(Name = "رقم البطاقة الموحدة")]
        public string IdentityCardN { get; set; } = string.Empty;


        [Display(Name = "تاريخ إصدار البطاقة الموحدة")]
        public DateTime IdentityDate { get; set; } = DateTime.Now;

        // ========== القوائم المنسدلة الأساسية ==========
        public List<string> Governorates { get; set; } = new();
        public List<string> Genders { get; set; } = new();
        public List<string> Educations { get; set; } = new();
        public List<string> Ministries { get; set; } = new();
        public List<string> EmploymentStatuses { get; set; } = new();

        // ========== قوائم جهات الانتساب ==========
        public List<string> AffiliationEntities { get; set; } = new();
        public List<string> DivisionsList { get; set; } = new();
        public List<string> SectionsList { get; set; } = new();
        public List<string> GroupsList { get; set; } = new();

        // ========== قوائم النقابات والاتحادات والجمعيات ==========
        public List<string> UnionsList { get; set; } = new();
        public List<string> FederationsList { get; set; } = new();
        public List<string> FederationDivisionsList { get; set; } = new();
        public List<string> FederationSectionsList { get; set; } = new();
        public List<string> FederationGroupsList { get; set; } = new();
        public List<string> AssociationsList { get; set; } = new();
        public List<string> NgosList { get; set; } = new();
    }
}