// ملف: Models/NgoMembership.cs
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class NgoMembership
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = "اسم الجمعية غير الحكومية")]
        public string? NgoName { get; set; }

        [Display(Name = "الصفة في الجمعية")]
        public string? Position { get; set; }

        [Display(Name = "رقم هوية الجمعية")]
        public string? IdNumber { get; set; }

        [Display(Name = "تاريخ الانتماء للجمعية")]
        [DataType(DataType.Date)]
        public DateTime? AffiliationDate { get; set; }

        

        // رابط المستخدم
        public string UserId { get; set; } = string.Empty;
    }
}