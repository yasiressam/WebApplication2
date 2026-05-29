// Models/Request/RequestReply.cs
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models.Request
{
    public class RequestReply
    {
        [Key]
        public int Id { get; set; }

        public int RequestId { get; set; }

        public string UserId { get; set; } = string.Empty;

        [Required]
        [Display(Name = "الرد")]
        public string Reply { get; set; } = string.Empty;

        public DateTime RepliedAt { get; set; } = DateTime.UtcNow;

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        [Display(Name = "الحالة الجديدة")]
        public RequestStatus NewStatus { get; set; } = RequestStatus.Processed;
    }
}