// ملف: Models/Profile/MembershipViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models.Profile
{
    public class MembershipViewModel
    {
        // ========== النقابة ==========
        [Display(Name = "اسم النقابة")]
        public string? UnionName { get; set; }

        [Display(Name = "الصفة في النقابة")]
        public string? UnionPosition { get; set; }

        [Display(Name = "رقم هوية النقابة")]
        public string? UnionIdNumber { get; set; }

        [Display(Name = "تاريخ النفاذ للنقابة")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime? UnionAffiliationDate { get; set; }



        // ========== الاتحاد ==========
        [Display(Name = "اسم الاتحاد")]
        public string? FederationName { get; set; }

        [Display(Name = "قسم الاتحاد")]
        public string? FederationDivisionName { get; set; }

        [Display(Name = "شعبة الاتحاد")]
        public string? FederationSectionName { get; set; }

        [Display(Name = "وحدة الاتحاد")]
        public string? FederationGroupName { get; set; }

        [Display(Name = "الصفة في الاتحاد")]
        public string? FederationPosition { get; set; }

        [Display(Name = "رقم هوية الاتحاد")]
        public string? FederationIdNumber { get; set; }

        [Display(Name = "تاريخ النفاذ للاتحاد")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime? FederationAffiliationDate { get; set; }

        // ========== الجمعية ==========
        [Display(Name = "اسم الجمعية")]
        public string? AssociationName { get; set; }

        [Display(Name = "الصفة في الجمعية")]
        public string? AssociationPosition { get; set; }

        [Display(Name = "رقم هوية الجمعية")]
        public string? AssociationIdNumber { get; set; }

        [Display(Name = "تاريخ النفاذ للجمعية")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime? AssociationAffiliationDate { get; set; }

       

        // ========== الجمعية غير الحكومية ==========
        [Display(Name = "اسم الجمعية غير الحكومية")]
        public string? NgoName { get; set; }

        [Display(Name = "الصفة في الجمعية غير الحكومية")]
        public string? NgoPosition { get; set; }

        [Display(Name = "رقم هوية الجمعية غير الحكومية")]
        public string? NgoIdNumber { get; set; }

        [Display(Name = "تاريخ النفاذ للمنظمة غير الحكومية")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime? NgoAffiliationDate { get; set; }

        
    }
}
