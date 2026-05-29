// DTOs/User/UpdateUserDto.cs
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.DTOs.User
{
    /// <summary>
    /// بيانات تحديث المستخدم (للسوبر أدمن والأدمن)
    /// </summary>
    public class UpdateUserDto
    {
        [Display(Name = "الاسم الكامل")]
        public string? FullName { get; set; }

        [RegularExpression(@"^07\d{9}$", ErrorMessage = "رقم الهاتف يجب أن يبدأ بـ 07 ويتكون من 11 رقم")]
        [Display(Name = "رقم الهاتف")]
        public string? PhoneNumber { get; set; }

        [Display(Name = "المحافظة")]
        public string? Governorate { get; set; }

        [Display(Name = "نوع الحساب")]
        public string? AccountType { get; set; }

        [Display(Name = "المحافظة المدارة")]
        public string? ManagedGovernorate { get; set; }

        [Display(Name = "الأدوار")]
        public List<string>? Roles { get; set; }

        [Display(Name = "حالة الحساب")]
        public bool? IsActive { get; set; }

        [Display(Name = "اعتماد البيانات الأساسية")]
        public bool? IsBasicInfoApproved { get; set; }

        [Display(Name = "سبب رفض البيانات الأساسية")]
        public string? BasicInfoRejectionReason { get; set; }
    }
}