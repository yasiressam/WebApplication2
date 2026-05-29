// DTOs/Notification/SendNotificationDto.cs
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.DTOs.Notification
{
    /// <summary>
    /// بيانات إرسال إشعار جديد
    /// </summary>
    public class SendNotificationDto
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

        [Display(Name = "إرسال للجميع")]
        public bool SendToAll { get; set; } = false;

        [Display(Name = "الأيقونة")]
        public string Icon { get; set; } = "bi-bell";

        [Display(Name = "رابط النقر")]
        [Url(ErrorMessage = "الرابط غير صحيح")]
        public string? ClickUrl { get; set; }

        [Display(Name = "صورة مصغرة")]
        [Url(ErrorMessage = "الرابط غير صحيح")]
        public string? ImageUrl { get; set; }

        // لإرسال إشعارات OneSignal
        public bool SendPushNotification { get; set; } = false;
    }
}