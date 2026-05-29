// Models/Union.cs
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class Union
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "اسم النقابة")]
        public string Name { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string? CreatedBy { get; set; }
    }
}