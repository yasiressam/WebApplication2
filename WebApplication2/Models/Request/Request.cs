// Models/Request/Request.cs
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models.Request
{
    public enum RequestStatus
    {
        [Display(Name = "قيد الانتظار")]
        Pending = 0,

        [Display(Name = "قيد المراجعة")]
        UnderReview = 1,

        [Display(Name = "تمت الموافقة")]
        Approved = 2,

        [Display(Name = "مرفوض")]
        Rejected = 3,

        [Display(Name = "تمت المعالجة")]
        Processed = 4,

        [Display(Name = "بانتظار رد المستخدم")]
        WaitingForUser = 5,

        [Display(Name = "مغلق")]
        Closed = 6
    }

    public enum RequestType
    {
        [Display(Name = "عام")]
        General = 0,

        [Display(Name = "طلب إداري")]
        Administrative = 1,

        [Display(Name = "طلب تعديل بيانات")]
        DataCorrection = 2,

        [Display(Name = "شكوى")]
        Complaint = 3,

        [Display(Name = "دعم")]
        Support = 4,

        [Display(Name = "أخرى")]
        Other = 5
    }

    public enum RequestPriority
    {
        [Display(Name = "عادي")]
        Normal = 0,

        [Display(Name = "مهم")]
        Important = 1,

        [Display(Name = "عاجل")]
        Urgent = 2
    }

    public class Request
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "عنوان الطلب")]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [Display(Name = "محتوى الطلب")]
        public string Content { get; set; } = string.Empty;

        [Display(Name = "حالة الطلب")]
        public RequestStatus Status { get; set; } = RequestStatus.Pending;

        [Display(Name = "نوع الطلب")]
        public RequestType Type { get; set; } = RequestType.General;

        [Display(Name = "الأولوية")]
        public RequestPriority Priority { get; set; } = RequestPriority.Normal;

        [Display(Name = "تاريخ الإنشاء")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Display(Name = "آخر تحديث")]
        public DateTime? UpdatedAt { get; set; }

        [Display(Name = "تاريخ المعالجة")]
        public DateTime? ProcessedAt { get; set; }

        [Required]
        public string SenderId { get; set; } = string.Empty;

        public string? ProcessedById { get; set; }

        [Display(Name = "رد الإدارة")]
        public string? AdminResponse { get; set; }

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        [Display(Name = "مسار المرفق")]
        public string? AttachmentPath { get; set; }

        [Display(Name = "اسم المرفق")]
        [StringLength(260)]
        public string? AttachmentFileName { get; set; }

        [Display(Name = "نوع المرفق")]
        [StringLength(120)]
        public string? AttachmentContentType { get; set; }

        [Display(Name = "حجم المرفق")]
        public long? AttachmentSize { get; set; }

        public ICollection<RequestRecipient> Recipients { get; set; } = new List<RequestRecipient>();
    }
}
