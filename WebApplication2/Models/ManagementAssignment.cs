using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication2.Models
{
    public class ManagementAssignment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [Display(Name = "المحافظة")]
        public string Governorate { get; set; } = string.Empty;

        [Display(Name = "نطاق بغداد")]
        public string? BaghdadScope { get; set; }
        // مركزي / الكرخ / الرصافة

        [Display(Name = "جهة الانتساب")]
        public int? AffiliationEntityId { get; set; }

        [Display(Name = "القسم")]
        public int? DivisionId { get; set; }

        [Display(Name = "الشعبة")]
        public int? SectionId { get; set; }

        [Display(Name = "الوحدة")]
        public int? GroupId { get; set; }

        [Required]
        [Display(Name = "المستوى الإداري")]
        public string ManagementLevel { get; set; } = string.Empty;
        // Entity / Division / Section / Group

        [Required]
        [Display(Name = "نوع التكليف")]
        public string AssignmentRole { get; set; } = string.Empty;
        // Manager / Assistant

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
