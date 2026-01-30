using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class PersonalProfileViewModel
    {
        // ⚠️ هذا للعرض فقط، ليس جدولاً في قاعدة البيانات

        [Required(ErrorMessage = "معرف المستخدم مطلوب")]
        public string UserId { get; set; } = "";

        // بيانات الشخص
        [Required(ErrorMessage = "الاسم الكامل مطلوب")]
        [Display(Name = "الاسم الكامل")]
        [StringLength(100, ErrorMessage = "الاسم الكامل يجب أن يكون بين 3 و 100 حرف", MinimumLength = 3)]
        public string FullName { get; set; } = "";

        [Display(Name = "اسم العائلة")]
        public string LastName { get; set; } = "";

        [Display(Name = "اسم الأم")]
        public string MotherName { get; set; } = "";

        [Required(ErrorMessage = "تاريخ الميلاد مطلوب")]
        [DataType(DataType.Date, ErrorMessage = "تاريخ الميلاد غير صحيح")]
        [Display(Name = "تاريخ الميلاد")]
        public DateTime DateOfBirth { get; set; } = DateTime.Now.AddYears(-20);

        [Required(ErrorMessage = "الجنس مطلوب")]
        [Display(Name = "الجنس")]
        public string Gender { get; set; } = "ذكر";

        [Display(Name = "اللقب")]
        public string MozakeName { get; set; } = "";

        [Display(Name = "المؤهل العلمي")]
        public string Education { get; set; } = "";

        [Display(Name = "التخصص")]
        public string Specialization { get; set; } = "";

        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        [Phone(ErrorMessage = "رقم الهاتف غير صحيح")]
        [Display(Name = "رقم الهاتف")]
        public string PhoneNumber { get; set; } = "";

        [Required(ErrorMessage = "رقم البطاقة الوطنية مطلوب")]
        [Range(1, 999999999, ErrorMessage = "رقم البطاقة الوطنية غير صحيح")]
        [Display(Name = "رقم البطاقة الوطنية")]
        public int IdentityCardN { get; set; } = 0;

        [Required(ErrorMessage = "تاريخ إصدار البطاقة مطلوب")]
        [DataType(DataType.Date, ErrorMessage = "تاريخ إصدار البطاقة غير صحيح")]
        [Display(Name = "تاريخ إصدار البطاقة")]
        public DateTime IdentityDate { get; set; } = DateTime.Now;

        [Display(Name = "رقم البطاقة التموينية")]
        public int RationN { get; set; } = 0;

        [Display(Name = "مركز التوزيع")]
        public int RationCenter { get; set; } = 0;

        // بيانات العنوان
        [Required(ErrorMessage = "المحافظة مطلوبة")]
        [Display(Name = "المحافظة")]
        public string Governorate { get; set; } = "بغداد";

        [Display(Name = "القضاء/المديرية")]
        public string District { get; set; } = "";

        [Display(Name = "الناحية")]
        public string SubDistrict { get; set; } = "";

        [Display(Name = "المحلة/الزقاق")]
        public string Alley { get; set; } = "";

        [Display(Name = "الشارع")]
        public string Street { get; set; } = "";

        [Display(Name = "المنزل")]
        public string House { get; set; } = "";

        [Display(Name = "أقرب نقطة دالة")]
        public string NearestPoint { get; set; } = "";

        // للعرض فقط
        public List<string> Governorates { get; set; } = new List<string>();
    }
}