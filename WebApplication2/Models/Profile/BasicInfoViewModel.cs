using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models.Profile
{
    public class BasicInfoViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string? Email { get; set; }

        public PersonalInfoViewModel PersonalInfo { get; set; } = new();
        public AddressViewModel Address { get; set; } = new();
        public WorkLocationViewModel WorkLocation { get; set; } = new();
        public EmploymentViewModel Employment { get; set; } = new();
        public DocumentsViewModel Documents { get; set; } = new();

        // ✅ رقم البطاقة الموحدة (12 رقم - نصي)
        [Display(Name = "رقم البطاقة الموحدة")]
        [Required(ErrorMessage = "رقم البطاقة الموحدة مطلوب")]
        [StringLength(12, MinimumLength = 12, ErrorMessage = "رقم البطاقة الموحدة يجب أن يكون 12 رقم")]
        [RegularExpression(@"^\d{12}$", ErrorMessage = "رقم البطاقة الموحدة يجب أن يتكون من 12 رقم فقط")]
        public string IdentityCardN { get; set; } = string.Empty;

        [Display(Name = "تاريخ إصدار البطاقة الموحدة")]
        [DataType(DataType.Date)]
        [Required(ErrorMessage = "تاريخ إصدار البطاقة مطلوب")]
        public DateTime IdentityDate { get; set; } = DateTime.Now;

        public IFormFile? CoverImageFile { get; set; }

        // ✅ الصورة الحالية
        public string? ExistingCoverImage { get; set; }

        // ✅ قوائم البيانات الثابتة
        public List<string> Governorates { get; set; } = new();
        public List<string> Genders { get; set; } = new();
        public List<string> Educations { get; set; } = new();
        public List<string> Ministries { get; set; } = new();
        public List<string> EmploymentStatuses { get; set; } = new();

        // ✅ قوائم العناوين (اختيارية - للقوائم المنسدلة المتسلسلة)
        public List<string> Districts { get; set; } = new();      // قائمة الأقضية
        public List<string> Areas { get; set; } = new();          // قائمة المناطق

        // ✅ خاصيات خاصة ببغداد - تم تعديلها بإضافة set;
        public List<string> BaghdadDistricts { get; set; } = new List<string> { "الكرخ", "الرصافة" };
        public bool IsBaghdadSelected { get; set; }
    }
}
