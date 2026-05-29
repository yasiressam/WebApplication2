// ملف: Models/Notification.cs
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class Notification
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Message { get; set; } = string.Empty;

        public string? Icon { get; set; } = "bi-bell";
        public string? ImageUrl { get; set; }
        public string? ClickUrl { get; set; }
        public DateTime SentAt { get; set; } = DateTime.Now;
        public bool IsRead { get; set; } = false;
        public DateTime? ReadAt { get; set; }
        public string? TargetUserId { get; set; }
        public bool IsForAll { get; set; } = false;
        public string? AdditionalData { get; set; }
    }
}