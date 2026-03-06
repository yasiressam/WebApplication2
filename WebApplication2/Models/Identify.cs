using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace WebApplication2.Models
{
    public class Identify
    {
        public int Id { get; set; }

        // البيانات الشخصية
        public string FullName { get; set; }
        public string LastName { get; set; }
        public string MotherName { get; set; }
        public DateTime Date { get; set; }
        public string Gender { get; set; }
        public string MozakeName { get; set; }
        public string Education { get; set; }
        public string Specialization { get; set; }
        public string PhoneNumber { get; set; }
        public string Email { get; set; }

        public int IdentityCardN { get; set; }
        public DateTime identityDate { get; set; }
        public int RationN { get; set; }
        public int RationCenter { get; set; }

        // 🔥 **هذا الحقل الجديد: المحافظة التي يديرها (للأدمن فقط)**
        public string? ManagedGovernorate { get; set; }

        // 🔥 **العلاقة مع المستخدم Identity (مهم جداً)**
        public string UserId { get; set; } // هذا مفتاح الربط

        [ForeignKey("UserId")]
        public virtual IdentityUser User { get; set; } // ← هذا يربط مع AspNetUsers

        // 🔥 **العلاقة مع العنوان**
        public int? AddressId { get; set; }

        [ForeignKey("AddressId")]
        public virtual Address Address { get; set; } // ← هذا يربط مع Addresses
    }
}