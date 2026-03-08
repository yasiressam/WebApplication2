using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace WebApplication2.Models
{
    public class Identify
    {
        public int Id { get; set; }

        // البيانات الشخصية - بدون Required
        public string FullName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string MotherName { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Gender { get; set; } = "ذكر";
        public string MozakeName { get; set; } = string.Empty;
        public string Education { get; set; } = string.Empty;
        public string Specialization { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        public int IdentityCardN { get; set; }
        public DateTime identityDate { get; set; }
        public int? RationN { get; set; }
        public int? RationCenter { get; set; }

        // المحافظة التي يديرها (للأدمن فقط)
        public string? ManagedGovernorate { get; set; }

        // العلاقة مع المستخدم Identity
        public string UserId { get; set; } = string.Empty;

        [ForeignKey("UserId")]
        public virtual IdentityUser? User { get; set; }

        // العلاقة مع العنوان
        public int? AddressId { get; set; }

        [ForeignKey("AddressId")]
        public virtual Address? Address { get; set; }
    }
}