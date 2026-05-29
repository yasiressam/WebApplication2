using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models.Profile
{
    public class ManagementAssignmentRequestResponseViewModel
    {
        public int RequestId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Governorate { get; set; } = string.Empty;
        public string AssignmentRole { get; set; } = "Manager";
        public string ManagementLevel { get; set; } = "Entity";

        [Required(ErrorMessage = "يرجى اختيار جهة الانتساب")]
        [Display(Name = "جهة الانتساب")]
        public int? AffiliationEntityId { get; set; }

        [Display(Name = "القسم")]
        public int? DivisionId { get; set; }

        [Display(Name = "الشعبة")]
        public int? SectionId { get; set; }

        [Display(Name = "التجمع")]
        public int? GroupId { get; set; }

        [Display(Name = "ملاحظات")]
        public string? UserNotes { get; set; }

        public string RequestedRoleArabic => AssignmentRole == "Assistant" ? "معاون" : "مسؤول";
        public string ManagementLevelArabic => ManagementLevel switch
        {
            "Entity" => "جهة",
            "Division" => "قسم",
            "Section" => "شعبة",
            "Group" => "وحدة",
            _ => ManagementLevel
        };
        public string RequestedAssignmentArabic => $"{RequestedRoleArabic} {ManagementLevelArabic}";

        public List<SelectListItem> AffiliationEntities { get; set; } = new();
        public List<SelectListItem> Divisions { get; set; } = new();
        public List<SelectListItem> Sections { get; set; } = new();
        public List<SelectListItem> Groups { get; set; } = new();
    }

    public class ManagementAssignmentReviewViewModel
    {
        public int RequestId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Governorate { get; set; } = string.Empty;
        public string AssignmentRole { get; set; } = "Manager";
        public string AssignmentRoleArabic => AssignmentRole == "Assistant" ? "معاون" : "مسؤول";
        public string ManagementLevel { get; set; } = string.Empty;
        public string ManagementLevelArabic { get; set; } = string.Empty;
        public string AffiliationEntityName { get; set; } = string.Empty;
        public string DivisionName { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public string? UserNotes { get; set; }
        public string? SuperAdminNotes { get; set; }
        public string RequestedByUserId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UserRespondedAt { get; set; }
    }
}
