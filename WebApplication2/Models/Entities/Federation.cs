// Models/Federation.cs
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class Federation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "اسم الاتحاد")]
        public string Name { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string? CreatedBy { get; set; }
        public ICollection<FederationDivision> Divisions { get; set; } = new List<FederationDivision>();
    }
}