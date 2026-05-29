using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models.Profile
{
    public class AssignManagerViewModel
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        public int IdentifyId { get; set; }

        [Display(Name = "الاسم")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "البريد الإلكتروني")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "المحافظة")]
        public string Governorate { get; set; } = string.Empty;

        [Display(Name = "القضاء")]
        public string District { get; set; } = string.Empty;

        public int? AffiliationEntityId { get; set; }
        public int? DivisionId { get; set; }
        public int? SectionId { get; set; }
        public int? GroupId { get; set; }

        [Display(Name = "جهة الانتساب")]
        public string AffiliationEntityName { get; set; } = string.Empty;

        [Display(Name = "القسم")]
        public string DivisionName { get; set; } = string.Empty;

        [Display(Name = "الشعبة")]
        public string SectionName { get; set; } = string.Empty;

        [Display(Name = "الوحدة")]
        public string GroupName { get; set; } = string.Empty;

        [Required(ErrorMessage = "يرجى اختيار المستوى الإداري")]
        [Display(Name = "المستوى الإداري")]
        public string SelectedLevel { get; set; } = string.Empty;
        // Entity / Division / Section / Group

        public string SelectedLevelArabic => SelectedLevel switch
        {
            "Entity" => "جهة",
            "Division" => "قسم",
            "Section" => "شعبة",
            "Group" => "وحدة",
            _ => ""
        };

        [Required(ErrorMessage = "يرجى اختيار نوع التكليف")]
        [Display(Name = "نوع التكليف")]
        public string AssignmentRole { get; set; } = "Manager";
        // Manager / Assistant

        public List<string> AvailableLevels { get; set; } = new();

        [Display(Name = "نوع المستخدم")]
        public bool IsSuperAdmin { get; set; }

        public bool IsAssigned { get; set; }
        public string CurrentAssignmentRole { get; set; } = string.Empty;
        public string CurrentAssignmentRoleArabic { get; set; } = string.Empty;
        public string CurrentManagementLevel { get; set; } = string.Empty;
        public string CurrentManagementLevelArabic { get; set; } = string.Empty;
        public string CurrentAssignmentDisplayArabic { get; set; } = string.Empty;
    }
}
