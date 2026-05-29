using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class FederationDivision
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "قسم الاتحاد")]
        public string Name { get; set; } = string.Empty;

        [Required]
        public int FederationId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public Federation? Federation { get; set; }
        public ICollection<FederationSection> Sections { get; set; } = new List<FederationSection>();
    }
}