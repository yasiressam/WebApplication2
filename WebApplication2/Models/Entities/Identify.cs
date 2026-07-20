// ملف: Models/Identify.cs
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class Identify
    {
        [Key]
        public int Id { get; set; }

        // ========== البيانات الشخصية الأساسية ==========
        [Display(Name = "الاسم الرباعي")]
        [Required(ErrorMessage = "الاسم الرباعي مطلوب")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "اللقب")]
        public string LastName { get; set; } = string.Empty;

        [Display(Name = "اسم الأم")]
        [Required(ErrorMessage = "اسم الأم مطلوب")]
        public string MotherName { get; set; } = string.Empty;

        [Display(Name = "تاريخ الميلاد")]
        [DataType(DataType.Date)]
        [Required(ErrorMessage = "تاريخ الميلاد مطلوب")]
        public DateTime Date { get; set; }

        [Display(Name = "الجنس")]
        [Required(ErrorMessage = "الجنس مطلوب")]
        public string Gender { get; set; } = "ذكر";

        [Display(Name = "التحصيل الدراسي")]
        public string Education { get; set; } = string.Empty;

        [Display(Name = "الاختصاص")]
        public string Specialization { get; set; } = string.Empty;

        [Display(Name = "رقم الهاتف")]
        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Display(Name = "رقم الواتساب")]
        public string? WhatsAppNumber { get; set; }

        [Display(Name = "تم تأكيد الواتساب")]
        public bool IsWhatsAppVerified { get; set; } = false;

        [Display(Name = "تاريخ تأكيد الواتساب")]
        public DateTime? WhatsAppVerifiedAt { get; set; }

        [Display(Name = "البريد الإلكتروني")]
        [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صالح")]
        public string Email { get; set; } = string.Empty;

        // ========== البطاقة الموحدة (12 رقم) ==========
        [Display(Name = "رقم البطاقة الموحدة")]
        [Required(ErrorMessage = "رقم البطاقة الموحدة مطلوب")]
        [StringLength(12, MinimumLength = 12, ErrorMessage = "رقم البطاقة الموحدة يجب أن يكون 12 رقم")]
        public string IdentityCardN { get; set; } = string.Empty;

        [Display(Name = "تاريخ إصدار البطاقة")]
        [DataType(DataType.Date)]
        public DateTime identityDate { get; set; }

        // ❌ تم إزالة RationN و RationCenter

        // ========== الصورة وتواريخ التسجيل ==========
        [Display(Name = "الصورة الشخصية")]
        public string? CoverImage { get; set; } = string.Empty;

        [Display(Name = "تاريخ التسجيل")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Display(Name = "تاريخ الانتماء")]
        public DateTime? AffiliationDate { get; set; }

        // ========== معلومات العمل ==========
        [Display(Name = "العمل")]
        public string? Work { get; set; }

        [Display(Name = "محافظة العمل التنظيمي")]
        public string? WorkGovernorate { get; set; }

        [Display(Name = "قضاء العمل التنظيمي")]
        public string? WorkDistrict { get; set; }

        [Display(Name = "الوزارة")]
        public string? Ministry { get; set; }

        [Display(Name = "الدائرة")]
        public string? Department { get; set; }

        [Display(Name = "المنصب")]
        public string? Position { get; set; }

        [Display(Name = "الحالة الوظيفية")]
        public string? EmploymentStatus { get; set; }

        // ✅ العنوان الوظيفي والدرجة الوظيفية (جديد)
        [Display(Name = "العنوان الوظيفي")]
        public string? JobTitle { get; set; }

        [Display(Name = "الدرجة الوظيفية")]
        public string? JobGrade { get; set; }

        // ========== المحافظة المدارة ==========
        [Display(Name = "المحافظة المدارة")]
        public string? ManagedGovernorate { get; set; }

        // ========== حالة المستخدم ==========
        [Display(Name = "نوع الحساب")]
        public string? AccountType { get; set; } = "عادي";

        [Display(Name = "تم التصعيد")]
        public bool IsPromoted { get; set; } = false;

        [Display(Name = "تاريخ التصعيد")]
        public DateTime? PromotionDate { get; set; }

        [Display(Name = "تم التصعيد بواسطة")]
        public string? PromotedBy { get; set; }

        // ========== رابط المستخدم ==========
        [Required]
        public string UserId { get; set; } = string.Empty;

        // ========== حالة طلب الترقية ==========
        [Display(Name = "طلب ترقية")]
        public bool RequestedPromotion { get; set; } = false;

        [Display(Name = "تاريخ طلب الترقية")]
        public DateTime? RequestedPromotionDate { get; set; }

        [Display(Name = "ملاحظات طلب الترقية")]
        public string? PromotionRequestNotes { get; set; }

        [Display(Name = "سبب الرفض")]
        public string? RejectionReason { get; set; }

        [Display(Name = "البيانات الأساسية معتمدة")]
        public bool IsBasicInfoApproved { get; set; } = false;

        [Display(Name = "تاريخ طلب مراجعة البيانات")]
        public DateTime? BasicInfoRequestedAt { get; set; }

        [Display(Name = "تاريخ اعتماد البيانات")]
        public DateTime? BasicInfoApprovalDate { get; set; }

        [Display(Name = "تم الاعتماد بواسطة")]
        public string? BasicInfoApprovedBy { get; set; }

        [Display(Name = "سبب رفض البيانات")]
        public string? BasicInfoRejectionReason { get; set; }

        // ========== الحالة الاجتماعية ==========
        [Display(Name = "الحالة الاجتماعية")]
        public string? MaritalStatus { get; set; }

        // ========== معلومات الطالب الجامعي ==========
        [Display(Name = "نوع الجامعة")]
        public string? UniversityType { get; set; }

        [Display(Name = "نوع المؤسسة")]
        public string? InstitutionType { get; set; }

        [Display(Name = "اسم الجامعة/المعهد")]
        public string? InstitutionName { get; set; }

        [Display(Name = "الكلية/القسم")]
        public string? FacultyDepartment { get; set; }

        [Display(Name = "نوع الدراسة")]
        public string? StudyType { get; set; }

        [Display(Name = "المرحلة")]
        public string? StudyStage { get; set; }
        [Display(Name = "القضاء المُدار")]
        public string? ManagedDistrict { get; set; } = string.Empty;

        public WorkLocation? WorkLocation { get; set; }
    }
}
