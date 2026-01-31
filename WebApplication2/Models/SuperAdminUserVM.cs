using System;

namespace WebApplication2.Models
{
    // كلاس لعرض قائمة المستخدمين
    public class SuperAdminUserVM
    {
        public string Id { get; set; }
        public string Email { get; set; }
        public string Roles { get; set; }
        public string Governorate { get; set; } // محافظة المستخدم (من Address)
        public string ManagedGovernorate { get; set; } // المحافظة التي يديرها (إذا كان أدمن)
        public bool IsActive { get; set; }
        public string FullName { get; set; }
    }

    // كلاس لعرض تفاصيل المستخدم الكاملة
    public class SuperAdminUserDetailsVM
    {
        // معلومات الحساب
        public string UserId { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public bool IsActive { get; set; }
        public string Roles { get; set; }

        // المعلومات الشخصية
        public string FullName { get; set; }
        public string LastName { get; set; }
        public string MotherName { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string Gender { get; set; }
        public string MozakeName { get; set; }
        public string Education { get; set; }
        public string Specialization { get; set; }
        public int IdentityCardN { get; set; }
        public DateTime IdentityDate { get; set; }
        public int RationN { get; set; }
        public int RationCenter { get; set; }

        // معلومات العنوان
        public Address Address { get; set; }

        // المحافظة المدارة (إذا كان أدمن)
        public string ManagedGovernorate { get; set; }
    }
}