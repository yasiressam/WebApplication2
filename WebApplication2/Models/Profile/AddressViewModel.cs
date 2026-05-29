// ملف: Models/Profile/AddressViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models.Profile
{
    public class AddressViewModel
    {
        [Display(Name = "المحافظة")]
        [Required(ErrorMessage = "المحافظة مطلوبة")]
        public string? Governorate { get; set; }

        [Display(Name = "القضاء")]
        public string? District { get; set; }

        [Display(Name = "المنطقة")]
        [Required(ErrorMessage = "المنطقة مطلوبة")]
        public string? Area { get; set; }

        [Display(Name = "المحلة")]
        [Required(ErrorMessage = "المحلة مطلوبة")]
        public string? Alley { get; set; }

        [Display(Name = "الزقاق")]
        [Required(ErrorMessage = "الزقاق مطلوب")]
        public string? Street { get; set; }

        [Display(Name = "الدار")]
        [Required(ErrorMessage = "رقم الدار مطلوب")]
        public string? House { get; set; }

        [Display(Name = "أقرب نقطة دالة")]
        public string? NearestPoint { get; set; }
    }
}
