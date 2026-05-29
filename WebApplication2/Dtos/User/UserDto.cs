// DTOs/User/UserDto.cs
using System;
using WebApplication2.Models;

namespace WebApplication2.DTOs.User
{
    /// <summary>
    /// عرض أساسي للمستخدم (للقوائم والبحث السريع)
    /// </summary>
    public class UserDto
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Governorate { get; set; } = string.Empty;
        public string? ManagedGovernorate { get; set; }
        public string AccountType { get; set; } = string.Empty;
        public string? CoverImage { get; set; }
        public string? MaritalStatus { get; set; }
        public string? UniversityType { get; set; }

        // حالة المستخدم
        public bool IsActive { get; set; }
        public bool IsBasicInfoApproved { get; set; }
        public bool IsPromoted { get; set; }
        public bool RequestedPromotion { get; set; }

        // الأدوار
        public List<string> Roles { get; set; } = new();

        // خصائص مساعدة
        public bool IsAdmin => Roles.Contains(clsRoles.Admin);
        public bool IsSuperAdmin => Roles.Contains(clsRoles.SuperAdmin);
        public bool IsMember => Roles.Contains(clsRoles.Member);

        // إحصائيات
        public int CompletionPercentage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLogin { get; set; }
    }
}