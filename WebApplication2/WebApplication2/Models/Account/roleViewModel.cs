
namespace WebApplication2.Models
{
    public class roleViewModel
    {
        public string? RoleId { get; set; }
        public string? RoleName { get; set; }
        public string? Description { get; set; }
        public bool AssignedToUser { get; set; }
        // إضافة محافظة المسؤول
        public string? Governorate { get; set; }
    }
}