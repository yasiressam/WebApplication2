
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class RegisterViewModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "البريد الإلكتروني")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "كلمة المرور")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "تأكيد كلمة المرور")]
        [Compare("Password", ErrorMessage = "كلمة المرور وتأكيدها غير متطابقين")]
        public string ConfirmPassword { get; set; } = string.Empty;

        // ⭐ خاصية جديدة: الدور (للاستخدام من قبل الأدمن فقط)
        [Display(Name = "الدور")]
        public string? Role { get; set; } = "User";

        // ⭐ خاصية جديدة: المحافظة المدارة (للأدمن فقط)
        [Display(Name = "المحافظة المدارة (للأدمن فقط)")]
        public string? ManagedGovernorate { get; set; }

        // ⭐ خاصية جديدة: هل الحساب تم إنشاؤه من قبل أدمن؟
        public bool IsCreatedByAdmin { get; set; } = false;
    }
}
