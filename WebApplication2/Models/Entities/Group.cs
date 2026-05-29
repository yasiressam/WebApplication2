// Models/Group.cs
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class Group
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "الوحدة")]
        public string Name { get; set; } = string.Empty;

        [Required]
        public int SectionId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public Section? Section { get; set; }
    }
}
