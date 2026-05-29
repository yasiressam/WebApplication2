// ملف: Models/Profile/AffiliationViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models.Profile
{
    public class AffiliationViewModel
    {
        [Display(Name = "جهة الانتساب")]
        public string? AffiliationEntity { get; set; }

        [Display(Name = "القسم")]
        public string? Division { get; set; }

        [Display(Name = "الشعبة")]
        public string? Section { get; set; }

        [Display(Name = "الوحدة")]
        public string? Group { get; set; }

        [Display(Name = "اسم المزكي أو المعرف")]
        public string? MozakeName { get; set; }

        [Display(Name = "رقم هاتف المزكي")]
        [DataType(DataType.PhoneNumber)]
        [RegularExpression(@"^07\d{9}$", ErrorMessage = "رقم الهاتف يجب أن يبدأ بـ 07 ويتكون من 11 رقم")]
        public string? MozakePhoneNumber { get; set; }  // ✅ إضافة رقم هاتف المزكي

        [Display(Name = "رقم الباج الخاص بك")]
        public string? BadgeNumber { get; set; }

        [Display(Name = "تاريخ الانتماء")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime? AffiliationDate { get; set; }

        // ❌ تم إزالة ExpiryDate (تاريخ النفاذ)
    }
}
