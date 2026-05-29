using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication2.Models
{
    public class Event
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string EventType { get; set; } = string.Empty;

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public string? Location { get; set; }

        public string? MeetingLink { get; set; }

        public bool IsMandatory { get; set; } = false;

        public bool IsUrgent { get; set; } = false;

        public int? MaxAttendance { get; set; }

        public string Governorate { get; set; } = string.Empty;

        public string TargetCategory { get; set; } = "all";

        public string? Description { get; set; }

        public int? TargetAffiliationEntityId { get; set; }

        public bool IsActive { get; set; } = true;

        public string CreatedBy { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // ========== خصائص مساعدة (لا تخزن في قاعدة البيانات) ==========

        [NotMapped]
        public string Status
        {
            get
            {
                if (DateTime.Now < StartDate) return "قادم";
                if (DateTime.Now > EndDate) return "انتهى";
                return "جاري الآن";
            }
        }

        [NotMapped]
        public string StatusColor
        {
            get
            {
                if (DateTime.Now < StartDate) return "primary";
                if (DateTime.Now > EndDate) return "secondary";
                return "success";
            }
        }
    }
}