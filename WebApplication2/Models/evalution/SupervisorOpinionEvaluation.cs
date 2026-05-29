using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class SupervisorOpinionEvaluation
    {
        [Key]  // ✅ هذا مهم جداً
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Range(1, 16)]
        public int Score { get; set; } = 8;

        [Required]
        public int Month { get; set; }

        [Required]
        public int Year { get; set; }

        public string? EvaluatedBy { get; set; }

        public DateTime? EvaluatedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}