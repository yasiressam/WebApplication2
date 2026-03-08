namespace WebApplication2.Models
{
    // كلاس لعرض قائمة المستخدمين
    public class SuperAdminUserVM
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Roles { get; set; } = string.Empty;
        public string Governorate { get; set; } = string.Empty;
        public string ManagedGovernorate { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string FullName { get; set; } = string.Empty;
    }

    // كلاس لعرض تفاصيل المستخدم الكاملة
    public class SuperAdminUserDetailsVM
    {
        // معلومات الحساب
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string Roles { get; set; } = string.Empty;

        // المعلومات الشخصية
        public string FullName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string MotherName { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string MozakeName { get; set; } = string.Empty;
        public string Education { get; set; } = string.Empty;
        public string Specialization { get; set; } = string.Empty;
        public int IdentityCardN { get; set; }
        public DateTime IdentityDate { get; set; }

        // ✅ التعديل المهم: تغيير إلى int? لتتناسب مع Identify.cs
        public int? RationN { get; set; }           // ✅ int? (nullable)
        public int? RationCenter { get; set; }      // ✅ int? (nullable)

        // معلومات العنوان
        public Address? Address { get; set; }

        // المحافظة المدارة (إذا كان أدمن)
        public string? ManagedGovernorate { get; set; }
    }
}