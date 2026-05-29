// Models/AffiliationInfo.cs
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class AffiliationInfo
    {
        [Key]
        public int Id { get; set; }

        // العلاقات مع الجداول الجديدة (باستخدام Foreign Keys)
        public int? AffiliationEntityId { get; set; }
        public int? DivisionId { get; set; }
        public int? SectionId { get; set; }
        public int? GroupId { get; set; }

        // بيانات المزكي
        [Display(Name = "اسم المزكي أو المعرف")]
        public string? MozakeName { get; set; }

        [Display(Name = "رقم هاتف المزكي")]
        [DataType(DataType.PhoneNumber)]
        [RegularExpression(@"^07\d{9}$", ErrorMessage = "رقم الهاتف يجب أن يبدأ بـ 07 ويتكون من 11 رقم")]
        public string? MozakePhoneNumber { get; set; }

        [Display(Name = "رقم الباج")]
        public string? BadgeNumber { get; set; }

        [Display(Name = "تاريخ الانتماء")]
        [DataType(DataType.Date)]
        public DateTime? AffiliationDate { get; set; }

        // رابط المستخدم
        public string UserId { get; set; } = string.Empty;

        // العلاقات Navigation Properties
        public AffiliationEntity? AffiliationEntity { get; set; }
        public Division? Division { get; set; }
        public Section? Section { get; set; }
        public Group? Group { get; set; }
    }
}