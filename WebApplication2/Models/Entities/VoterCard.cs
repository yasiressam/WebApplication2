// ملف: Models/VoterCard.cs
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class VoterCard
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = "رقم بطاقة الناخب")]
        public string? VoterCardNumber { get; set; }

        [Display(Name = "رقم مركز الاقتراع")]
        public string? PollingCenterNumber { get; set; }

        // رابط المستخدم
        public string UserId { get; set; } = string.Empty;
    }
}