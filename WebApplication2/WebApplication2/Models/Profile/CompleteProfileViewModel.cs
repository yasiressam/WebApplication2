// ملف: Models/Profile/CompleteProfileViewModel.cs
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace WebApplication2.Models.Profile
{
    public class CompleteProfileViewModel
    {
        // ========== معلومات المستخدم الأساسية ==========
        public string UserId { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? UserRole { get; set; }
        public bool IsEmailConfirmed { get; set; }
        public string? WhatsAppNumber { get; set; }
        public bool IsWhatsAppVerified { get; set; }
        public DateTime? WhatsAppVerifiedAt { get; set; }

        // ========== حالة المستخدم ==========
        [Display(Name = "نوع الحساب")]
        public string? AccountType { get; set; }
        public bool IsPromoted { get; set; } = false;
        public DateTime? PromotionDate { get; set; }
        public string? PromotedBy { get; set; }

        // ========== الكلاسات الفرعية ==========
        public PersonalInfoViewModel PersonalInfo { get; set; } = new();
        public AddressViewModel Address { get; set; } = new();
        public WorkLocationViewModel WorkLocation { get; set; } = new();
        public DocumentsViewModel Documents { get; set; } = new();
        public EmploymentViewModel Employment { get; set; } = new();
        public AffiliationViewModel Affiliation { get; set; } = new();
        public MembershipViewModel Memberships { get; set; } = new();

        // ✅ رقم البطاقة الموحدة (12 رقم - نصي)
        [Display(Name = "رقم البطاقة الموحدة")]
        [StringLength(12, MinimumLength = 12, ErrorMessage = "رقم البطاقة الموحدة يجب أن يكون 12 رقم")]
        [RegularExpression(@"^\d{12}$", ErrorMessage = "رقم البطاقة الموحدة يجب أن يتكون من 12 رقم فقط")]
        public string IdentityCardN { get; set; } = string.Empty;

        [Display(Name = "تاريخ إصدار البطاقة الموحدة")]
        public DateTime IdentityDate { get; set; }

        // ========== ملف الصورة ==========
        public IFormFile? CoverImageFile { get; set; }

        // ========== التواريخ العامة ==========
        public DateTime CreatedAt { get; set; }

        // ========== القوائم المنسدلة الأساسية ==========
        public List<string> Governorates { get; set; } = new();
        public List<string> Genders { get; set; } = new();
        public List<string> Educations { get; set; } = new();
        public List<string> Ministries { get; set; } = new();
        public List<string> EmploymentStatuses { get; set; } = new();

        // ✅ القوائم المنسدلة للعناوين (جديدة)
        public List<string> Districts { get; set; } = new();
        public List<string> Areas { get; set; } = new();

        // ✅ ✅ ✅ خاصيات بغداد الجديدة (مع set;)
        public List<string> BaghdadDistricts { get; set; } = new List<string> { "الكرخ", "الرصافة" };
        public bool IsBaghdadSelected { get; set; }

        // ✅ قائمة الدرجات الوظيفية (جديدة)
        public List<string> JobGradesList { get; set; } = new();

        // ✅ قائمة المراحل الدراسية (جديدة)
        public List<string> StudyStagesList { get; set; } = new();

        // ========== قوائم جهات الانتساب (من السوبر أدمن) ==========
        public List<string> AffiliationEntities { get; set; } = new();
        public List<string> DivisionsList { get; set; } = new();
        public List<string> SectionsList { get; set; } = new();
        public List<string> GroupsList { get; set; } = new();

        // ========== قوائم النقابات والاتحادات والجمعيات (من السوبر أدمن) ==========
        public List<string> UnionsList { get; set; } = new();
        public List<string> FederationsList { get; set; } = new();
        public List<string> FederationDivisionsList { get; set; } = new();
        public List<string> FederationSectionsList { get; set; } = new();
        public List<string> FederationGroupsList { get; set; } = new();
        public List<string> AssociationsList { get; set; } = new();
        public List<string> NgosList { get; set; } = new();

        // ========== حالة طلب الترقية ==========
        public bool RequestedPromotion { get; set; } = false;
        public DateTime? RequestedPromotionDate { get; set; }
        public string? PromotionRequestNotes { get; set; }
        public string? RejectionReason { get; set; }

        // ✅ ✅ ✅ الخصائص الجديدة لحفظ معرفات الانتساب (مهم جداً للحفاظ على البيانات) ✅ ✅ ✅
        public int? AffiliationEntityId { get; set; }
        public int? DivisionId { get; set; }
        public int? SectionId { get; set; }
        public int? GroupId { get; set; }
    }
}
