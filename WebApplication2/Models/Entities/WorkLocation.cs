using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication2.Models
{
    public class WorkLocation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int IdentifyId { get; set; }

        [Display(Name = "محافظة العمل التنظيمي")]
        public string Governorate { get; set; } = string.Empty;

        [Display(Name = "قضاء العمل التنظيمي")]
        public string? District { get; set; }

        [ForeignKey(nameof(IdentifyId))]
        public Identify? Identify { get; set; }
    }
}
