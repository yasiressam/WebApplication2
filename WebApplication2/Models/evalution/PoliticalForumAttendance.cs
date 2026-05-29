using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class PoliticalForumAttendance
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public int EventId { get; set; }

        public bool Attended { get; set; } = false;

        [Required]
        public int Month { get; set; }

        [Required]
        public int Year { get; set; }

        // تحسب تلقائياً: (عدد الحضور / إجمالي الأحداث) * 12
        public double Score { get; set; } = 0;

        public string? RecordedBy { get; set; }

        public DateTime? RecordedAt { get; set; }
    }
}