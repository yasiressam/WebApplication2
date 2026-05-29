using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Http;

namespace WebApplication2.Models.Request
{
    // ✅ نموذج إنشاء طلب جديد (للـ Create View)
    public class RequestCreateViewModel
    {
        [Required(ErrorMessage = "العنوان مطلوب")]
        [Display(Name = "عنوان الطلب")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "المحتوى مطلوب")]
        [Display(Name = "محتوى الطلب")]
        public string Content { get; set; } = string.Empty;

        [Display(Name = "نوع الطلب")]
        public RequestType Type { get; set; } = RequestType.General;

        [Display(Name = "الأولوية")]
        public RequestPriority Priority { get; set; } = RequestPriority.Normal;

        [Required(ErrorMessage = "الرجاء اختيار مستلم واحد على الأقل")]
        [Display(Name = "المستلمون")]
        public List<string> RecipientIds { get; set; } = new List<string>();

        [Display(Name = "إرفاق ملف")]
        public IFormFile? AttachmentFile { get; set; }
    }

    public class RequestRecipientOptionViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string RoleText { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
    }

    // ✅ عرض الطلب في القائمة
    public class RequestListItemViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string StatusName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string SenderRole { get; set; } = string.Empty;
        public string RecipientsNames { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public string PriorityName { get; set; } = string.Empty;
        public RequestPriority Priority { get; set; }
        public bool IsRead { get; set; }
        public string CreatedAtFormatted => CreatedAt.ToString("yyyy/MM/dd HH:mm");
    }

    // ✅ عرض تفاصيل الطلب
    public class RequestDetailsViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string StatusName { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public string PriorityName { get; set; } = string.Empty;
        public RequestPriority Priority { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }

        public string SenderId { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string SenderRole { get; set; } = string.Empty;

        public List<RecipientInfoViewModel> Recipients { get; set; } = new List<RecipientInfoViewModel>();

        public string? AdminResponse { get; set; }
        public string? ProcessorName { get; set; }
        public string? ProcessorRole { get; set; }
        public string? Notes { get; set; }
        public string? AttachmentPath { get; set; }
        public string? AttachmentFileName { get; set; }
        public string? AttachmentContentType { get; set; }
        public long? AttachmentSize { get; set; }

        // ✅ إضافة قائمة الردود (للمحادثة الكاملة)
        public List<ReplyInfoViewModel> Replies { get; set; } = new List<ReplyInfoViewModel>();
    }

    // ✅ معلومات المستلم
    public class RecipientInfoViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public bool HasResponded { get; set; }
        public DateTime? RespondedAt { get; set; }
    }

    // ✅ نموذج الرد على الطلب
    public class RequestResponseViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "الرد مطلوب")]
        [Display(Name = "الرد على الطلب")]
        public string AdminResponse { get; set; } = string.Empty;

        [Display(Name = "تغيير الحالة")]
        public RequestStatus NewStatus { get; set; } = RequestStatus.Processed;

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }
    }

    // ✅ ✅ ✅ كلاس جديد لعرض الردود في المحادثة ✅ ✅ ✅
    public class ReplyInfoViewModel
    {
        public int Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string UserRole { get; set; } = string.Empty;
        public string ReplyContent { get; set; } = string.Empty;
        public DateTime RepliedAt { get; set; }
        public string? Notes { get; set; }
        public string RepliedAtFormatted => RepliedAt.ToString("yyyy/MM/dd HH:mm");
    }
}
