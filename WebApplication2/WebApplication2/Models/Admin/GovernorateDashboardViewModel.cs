// ملف: Models/GovernorateDashboardViewModel.cs
namespace WebApplication2.Models
{
    public class GovernorateDashboardViewModel
    {
        public string Name { get; set; } = string.Empty;
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int InactiveUsers { get; set; }
        public int MaleCount { get; set; }
        public int FemaleCount { get; set; }
        public int CompletedProfiles { get; set; }
        public int IncompleteProfiles { get; set; }
        public List<string> Admins { get; set; } = new();
        public DateTime? LastActivity { get; set; }
        public double CenterX { get; set; } // إحداثيات X للعرض على الخريطة
        public double CenterY { get; set; } // إحداثيات Y للعرض على الخريطة
        public string ColorClass { get; set; } = "governorate-default";
    }

    public class IraqMapViewModel
    {
        public List<GovernorateDashboardViewModel> Governorates { get; set; } = new();
        public int TotalUsers { get; set; }
        public int TotalAdmins { get; set; }
        public int TotalGovernorates { get; set; }
        public int CoveredGovernorates { get; set; }
        public Dictionary<string, int> UsersByGovernorate { get; set; } = new();
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }
}