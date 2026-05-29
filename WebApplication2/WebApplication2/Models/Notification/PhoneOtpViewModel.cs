using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class PhoneOtpViewModel
    {
        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        [Phone(ErrorMessage = "رقم الهاتف غير صحيح")]
        [Display(Name = "رقم الهاتف")]
        public string PhoneNumber { get; set; }

        [Display(Name = "كود التحقق")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "الكود يجب أن يكون 6 أرقام")]
        public string OtpCode { get; set; }

        public bool IsOtpSent { get; set; } = false;
    }
}