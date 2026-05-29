using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class FederationSection
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "شعبة الاتحاد")]
        public string Name { get; set; } = string.Empty;

        [Required]
        public int FederationDivisionId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public FederationDivision? FederationDivision { get; set; }
        public ICollection<FederationGroup> Groups { get; set; } = new List<FederationGroup>();
    }
}