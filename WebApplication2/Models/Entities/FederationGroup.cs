using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class FederationGroup
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "وحدة الاتحاد")]
        public string Name { get; set; } = string.Empty;

        [Required]
        public int FederationSectionId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public FederationSection? FederationSection { get; set; }
    }
}
