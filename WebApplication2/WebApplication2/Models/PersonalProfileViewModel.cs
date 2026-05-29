// ملف: Models/Profile/PersonalProfileViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models.Profile
{
    public class PersonalProfileViewModel
    {
        // ========== معلومات المستخدم الأساسية ==========
        public string UserId { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? UserRole { get; set; }
        public bool IsEmailConfirmed { get; set; }

        // ========== حالة المستخدم ومراحل التسجيل ==========
        [Display(Name = "نوع الحساب")]
        public string? AccountType { get; set; }

        [Display(Name = "حالة الحساب")]
        public bool IsPromoted { get; set; } = false;

        [Display(Name = "تاريخ التصعيد")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime? PromotionDate { get; set; }

        [Display(Name = "تم التصعيد بواسطة")]
        public string? PromotedBy { get; set; }

        // ========== البيانات الشخصية (المرحلة 1) ==========
        public PersonalInfoViewModel PersonalInfo { get; set; } = new();

        // ========== العنوان (المرحلة 1) ==========
        public AddressViewModel Address { get; set; } = new();

        // ========== محافظة الاعتماد الإداري ==========
        public WorkLocationViewModel WorkLocation { get; set; } = new();

        // ========== الصورة الشخصية (المرحلة 1) ==========
        [Display(Name = "الصورة الشخصية")]
        public string? CoverImage { get; set; }
        public IFormFile? CoverImageFile { get; set; }

        // ========== جميع الحقول التالية تظهر فقط بعد تصعيد الحساب ==========

        // ========== الوثائق الرسمية (المرحلة 2) ==========
        public DocumentsViewModel Documents { get; set; } = new();

        // ========== الحالة الوظيفية والعمل (المرحلة 2) ==========
        public EmploymentViewModel Employment { get; set; } = new();

        // ========== معلومات الانتساب (المرحلة 2) ==========
        public AffiliationViewModel Affiliation { get; set; } = new();

        // ========== معلومات العضويات (المرحلة 3) ==========
        public MembershipViewModel Memberships { get; set; } = new();

        // ========== التواريخ العامة ==========
        [Display(Name = "تاريخ التسجيل في النظام")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}")]
        public DateTime CreatedAt { get; set; }

        [Display(Name = "تاريخ الانتماء")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime? AffiliationDate { get; set; }

        // ========== للعرض فقط ==========
        [Display(Name = "تاريخ التسجيل الأصلي")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}")]
        public DateTime? RegistrationDate { get; set; }

        // ========== القوائم المنسدلة ==========
        public List<string> Governorates { get; set; } = new();
        public List<string> Genders { get; set; } = new();
        public List<string> Educations { get; set; } = new();
        public List<string> Ministries { get; set; } = new();
        public List<string> EmploymentStatuses { get; set; } = new();

        // قوائم إضافية للعضويات
        public List<string> UnionsList { get; set; } = new();
        public List<string> FederationsList { get; set; } = new();
        public List<string> AssociationsList { get; set; } = new();
        public List<string> NgosList { get; set; } = new();
        public List<string> AffiliationEntitiesList { get; set; } = new();
    }
}
