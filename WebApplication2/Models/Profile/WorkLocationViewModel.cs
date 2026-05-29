using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models.Profile
{
    public class WorkLocationViewModel
    {
        [Display(Name = "محافظة العمل التنظيمي")]
        [Required(ErrorMessage = "محافظة العمل التنظيمي مطلوبة")]
        public string? Governorate { get; set; }

        [Display(Name = "قضاء العمل التنظيمي")]
        public string? District { get; set; }
    }
}
