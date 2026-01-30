namespace WebApplication2.Models
{
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
}