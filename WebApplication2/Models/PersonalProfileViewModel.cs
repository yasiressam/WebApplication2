
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class PersonalProfileViewModel
    {
        // ⚠️ هذا للعرض فقط، ليس جدولاً في قاعدة البيانات

        [Required(ErrorMessage = "معرف المستخدم مطلوب")]
        public string UserId { get; set; } = "";

        // =============== جميع بيانات Identify (الشخصية) ===============

        [Required(ErrorMessage = "الاسم الكامل مطلوب")]
        [Display(Name = "الاسم الكامل")]
        [StringLength(100, ErrorMessage = "الاسم الكامل يجب أن يكون بين 3 و 100 حرف", MinimumLength = 3)]
        public string FullName { get; set; } = "";

        [Display(Name = "اسم العائلة")]
        [StringLength(50, ErrorMessage = "اسم العائلة لا يزيد عن 50 حرف")]
        public string LastName { get; set; } = "";

        [Display(Name = "اسم الأم")]
        [StringLength(50, ErrorMessage = "اسم الأم لا يزيد عن 50 حرف")]
        public string MotherName { get; set; } = "";

        [Required(ErrorMessage = "تاريخ الميلاد مطلوب")]
        [DataType(DataType.Date, ErrorMessage = "تاريخ الميلاد غير صحيح")]
        [Display(Name = "تاريخ الميلاد")]
        public DateTime DateOfBirth { get; set; } = DateTime.Now.AddYears(-20);

        [Required(ErrorMessage = "الجنس مطلوب")]
        [Display(Name = "الجنس")]
        public string Gender { get; set; } = "ذكر";

        [Display(Name = "المزكي")]
        [StringLength(50, ErrorMessage = "المزكي لا يزيد عن 50 حرف")]
        public string MozakeName { get; set; } = "";

        [Display(Name = "المؤهل العلمي")]
        [StringLength(100, ErrorMessage = "المؤهل العلمي لا يزيد عن 100 حرف")]
        public string Education { get; set; } = "";

        [Display(Name = "التخصص")]
        [StringLength(100, ErrorMessage = "التخصص لا يزيد عن 100 حرف")]
        public string Specialization { get; set; } = "";

        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        [Phone(ErrorMessage = "رقم الهاتف غير صحيح")]
        [Display(Name = "رقم الهاتف")]
        [RegularExpression(@"^07[0-9]{9}$", ErrorMessage = "رقم الهاتف يجب أن يبدأ بـ 07 ويحتوي على 11 رقماً")]
        public string PhoneNumber { get; set; } = "";

        [Required(ErrorMessage = "رقم البطاقة الوطنية مطلوب")]
        [Range(100000, 999999999, ErrorMessage = "رقم البطاقة الوطنية يجب أن يكون بين 100,000 و 999,999,999")]
        [Display(Name = "رقم البطاقة الوطنية")]
        public int IdentityCardN { get; set; } = 0;

        [Required(ErrorMessage = "تاريخ إصدار البطاقة مطلوب")]
        [DataType(DataType.Date, ErrorMessage = "تاريخ إصدار البطاقة غير صحيح")]
        [Display(Name = "تاريخ إصدار البطاقة")]
        public DateTime IdentityDate { get; set; } = DateTime.Now;

        [Display(Name = "رقم البطاقة التموينية")]
        [Range(0, 999999999, ErrorMessage = "رقم البطاقة التموينية غير صحيح")]
        public int RationN { get; set; } = 0;

        [Display(Name = "مركز التوزيع")]
        [Range(0, 9999, ErrorMessage = "مركز التوزيع غير صحيح")]
        public int RationCenter { get; set; } = 0;

        // =============== جميع بيانات Address (العنوان) ===============

        [Required(ErrorMessage = "المحافظة مطلوبة")]
        [Display(Name = "المحافظة")]
        [StringLength(50, ErrorMessage = "اسم المحافظة لا يزيد عن 50 حرف")]
        public string Governorate { get; set; } = "بغداد";

        [Display(Name = "القضاء/المديرية")]
        [StringLength(100, ErrorMessage = "القضاء/المديرية لا يزيد عن 100 حرف")]
        public string District { get; set; } = "";

        [Display(Name = "الناحية")]
        [StringLength(100, ErrorMessage = "الناحية لا يزيد عن 100 حرف")]
        public string SubDistrict { get; set; } = "";

        [Display(Name = "المحلة/الزقاق")]
        [StringLength(100, ErrorMessage = "المحلة/الزقاق لا يزيد عن 100 حرف")]
        public string Alley { get; set; } = "";

        [Display(Name = "الشارع")]
        [StringLength(100, ErrorMessage = "الشارع لا يزيد عن 100 حرف")]
        public string Street { get; set; } = "";

        [Display(Name = "المنزل")]
        [StringLength(50, ErrorMessage = "المنزل لا يزيد عن 50 حرف")]
        public string House { get; set; } = "";

        [Display(Name = "أقرب نقطة دالة")]
        [StringLength(200, ErrorMessage = "أقرب نقطة دالة لا يزيد عن 200 حرف")]
        public string NearestPoint { get; set; } = "";

        // =============== بيانات إضافية للعرض فقط ===============

        // قائمة المحافظات للاختيار من القائمة المنسدلة
        public List<string> Governorates { get; set; } = new List<string>();

        // قائمة الجنس للاختيار
        public List<string> Genders { get; set; } = new List<string> { "ذكر", "أنثى" };

        // قائمة المؤهلات العلمية (يمكن إضافة المزيد)
        public List<string> Educations { get; set; } = new List<string>
        {
            "ابتدائي", "متوسط", "إعدادي", "ثانوي",
            "دبلوم", "بكالوريوس", "ماجستير", "دكتوراه"
        };

        // =============== خاصيات للعرض فقط (لا تدخل في الحفظ) ===============

        [Display(Name = "تاريخ التسجيل")]
        public DateTime? RegistrationDate { get; set; }

        [Display(Name = "الحساب مفعل")]
        public bool IsEmailConfirmed { get; set; }

        [Display(Name = "الدور")]
        public string? UserRole { get; set; }

        [Display(Name = "البريد الإلكتروني")]
        public string? Email { get; set; }
    }
}
