// Models/Section.cs
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class Section
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "الشعبة")]
        public string Name { get; set; } = string.Empty;

        [Required]
        public int DivisionId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public Division? Division { get; set; }
        public ICollection<Group> Groups { get; set; } = new List<Group>();
    }
}