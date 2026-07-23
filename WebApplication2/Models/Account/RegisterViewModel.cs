using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class RegisterViewModel
    {
        [Display(Name = "طريقة التسجيل")]
        public string RegistrationMethod { get; set; } = "whatsapp";

        [EmailAddress]
        [Display(Name = "البريد الإلكتروني")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "رقم الهاتف")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "كلمة المرور")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "تأكيد كلمة المرور")]
        [Compare("Password", ErrorMessage = "كلمة المرور وتأكيدها غير متطابقين")]
        public string ConfirmPassword { get; set; } = string.Empty;

        // الدور
        [Display(Name = "الدور")]
        public string? Role { get; set; } = "User";
        // ✅ أضف هذه الخاصية الجديدة
        [Display(Name = "الاسم الكامل")]
        public string? FullName { get; set; }

        // المحافظة المدارة (للأدمن فقط) - مع التحقق المخصص
        [Display(Name = "المحافظة المدارة")]
        [RequiredIf("Role", "Admin", ErrorMessage = "يجب تحديد المحافظة للمستخدمين بدور أدمن")]
        public string? ManagedGovernorate { get; set; }

        // ✅ القضاء المُدار (للأدمن في بغداد فقط)
        [Display(Name = "القضاء المُدار")]
        public string? ManagedDistrict { get; set; }

        // هل الحساب تم إنشاؤه من قبل أدمن؟
        public bool IsCreatedByAdmin { get; set; } = false;
    }

    // إضافة Validator مخصص
    public class RequiredIfAttribute : RequiredAttribute
    {
        private string PropertyName { get; set; }
        private object DesiredValue { get; set; }

        public RequiredIfAttribute(string propertyName, object desiredValue)
        {
            PropertyName = propertyName;
            DesiredValue = desiredValue;
        }

        protected override ValidationResult IsValid(object value, ValidationContext context)
        {
            var instance = context.ObjectInstance;
            var type = instance.GetType();
            var propertyValue = type.GetProperty(PropertyName).GetValue(instance, null);

            if (propertyValue?.ToString() == DesiredValue.ToString() && string.IsNullOrEmpty(value?.ToString()))
            {
                return new ValidationResult(FormatErrorMessage(context.DisplayName));
            }

            return ValidationResult.Success;
        }
    }
}
