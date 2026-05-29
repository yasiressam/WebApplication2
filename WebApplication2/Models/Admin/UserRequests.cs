// ملف: Models/UserRequests.cs
namespace WebApplication2.Models
{
    public class UpdateRolesRequest
    {
        public string UserId { get; set; } = string.Empty;
        public List<string> SelectedRoles { get; set; } = new List<string>();
    }

    public class ToggleStatusRequest
    {
        public string UserId { get; set; } = string.Empty;
    }

    public class DeleteUserRequest
    {
        public string UserId { get; set; } = string.Empty;
    }
}