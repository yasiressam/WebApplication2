using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class FederationMembership
    {
        [Key]
        public int Id { get; set; }

        public int? FederationId { get; set; }
        public int? FederationDivisionId { get; set; }
        public int? FederationSectionId { get; set; }
        public int? FederationGroupId { get; set; } // اختياري

        [Display(Name = "الصفة في الاتحاد")]
        public string? Position { get; set; }

        [Display(Name = "رقم هوية الاتحاد")]
        public string? IdNumber { get; set; }

        [Display(Name = "تاريخ الانتماء للاتحاد")]
        [DataType(DataType.Date)]
        public DateTime? AffiliationDate { get; set; }

        public string UserId { get; set; } = string.Empty;

        public Federation? Federation { get; set; }
        public FederationDivision? FederationDivision { get; set; }
        public FederationSection? FederationSection { get; set; }
        public FederationGroup? FederationGroup { get; set; }
    }
}