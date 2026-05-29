// Models/Division.cs
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class Division
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "القسم")]
        public string Name { get; set; } = string.Empty;

        [Required]
        public int AffiliationEntityId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public AffiliationEntity? AffiliationEntity { get; set; }
        public ICollection<Section> Sections { get; set; } = new List<Section>();
    }
}