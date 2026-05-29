// ملف: Models/UserDevice.cs
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class UserDevice
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public string PlayerId { get; set; } = string.Empty;

        public string? DeviceType { get; set; }
        public string? Browser { get; set; }
        public string? OperatingSystem { get; set; }
        public DateTime LastActive { get; set; } = DateTime.Now;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsSubscribed { get; set; } = true;
    }
}