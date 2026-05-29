// Models/Association.cs
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class Association
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "اسم الجمعية")]
        public string Name { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string? CreatedBy { get; set; }
    }
}