namespace WebApplication2.Models
{
    public class UpdateRolesRequest
    {
        public string UserId { get; set; }
        public List<string> SelectedRoles { get; set; }
    }

    public class ToggleStatusRequest
    {
        public string UserId { get; set; }
    }

    public class DeleteUserRequest
    {
        public string UserId { get; set; }
    }
}