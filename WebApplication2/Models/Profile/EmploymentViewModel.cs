// ملف: Models/Profile/EmploymentViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models.Profile
{
    public class EmploymentViewModel
    {
        [Display(Name = "الحالة الوظيفية")]
        [Required(ErrorMessage = "الحالة الوظيفية مطلوبة")]
        public string? EmploymentStatus { get; set; }

        [Display(Name = "العمل / المهنة")]
        [Required(ErrorMessage = "المهنة مطلوبة")]
        public string? Work { get; set; }

        [Display(Name = "الوزارة")]
        public string? Ministry { get; set; }

        [Display(Name = "الدائرة")]
        public string? Department { get; set; }

        [Display(Name = "المنصب")]
        public string? Position { get; set; }

        // ===== الخصائص الجديدة للموظفين =====
        [Display(Name = "العنوان الوظيفي")]
        public string? JobTitle { get; set; }

        [Display(Name = "الدرجة الوظيفية")]
        public string? JobGrade { get; set; }

        // ===== قائمة الدرجات الوظيفية =====
        public List<string> JobGradesList { get; set; } = new();
    }
}
