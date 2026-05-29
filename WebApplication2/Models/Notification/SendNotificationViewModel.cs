using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class SendNotificationViewModel
    {
        [Required(ErrorMessage = "العنوان مطلوب")]
        [Display(Name = "عنوان الإشعار")]
        [StringLength(100, ErrorMessage = "العنوان لا يزيد عن 100 حرف")]
        public string Title { get; set; } = "إشعار جديد";

        [Required(ErrorMessage = "الرسالة مطلوبة")]
        [Display(Name = "نص الإشعار")]
        [StringLength(500, ErrorMessage = "الرسالة لا تزيد عن 500 حرف")]
        public string Message { get; set; } = "هذا إشعار من الإدارة";

        [Display(Name = "المستلم")]
        public string? TargetUserId { get; set; }

        [Display(Name = "الأيقونة")]
        public string Icon { get; set; } = "bi-bell";

        [Display(Name = "رابط النقر")]
        [Url(ErrorMessage = "الرابط غير صحيح")]
        public string? ClickUrl { get; set; }

        [Display(Name = "صورة مصغرة")]
        [Url(ErrorMessage = "الرابط غير صحيح")]
        public string? ImageUrl { get; set; }
    }
}