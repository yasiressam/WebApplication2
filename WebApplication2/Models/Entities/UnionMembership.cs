// ملف: Models/UnionMembership.cs
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class UnionMembership
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = "اسم النقابة")]
        public string? UnionName { get; set; }

        [Display(Name = "الصفة في النقابة")]
        public string? Position { get; set; }

        [Display(Name = "رقم هوية النقابة")]
        public string? IdNumber { get; set; }

        [Display(Name = "تاريخ الانتماء للنقابة")]
        [DataType(DataType.Date)]
        public DateTime? AffiliationDate { get; set; }

      

        // رابط المستخدم
        public string UserId { get; set; } = string.Empty;
    }
}