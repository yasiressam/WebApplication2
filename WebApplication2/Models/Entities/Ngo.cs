// Models/Ngo.cs
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class Ngo
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "اسم المنظمة غير الحكومية")]
        public string Name { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string? CreatedBy { get; set; }
    }
}