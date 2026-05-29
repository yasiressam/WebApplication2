namespace WebApplication2.Models
{
    public class SuperAdminReportsViewModel
    {
        public List<ReportCardOptionVM> Cards { get; set; } = new();
        public int TotalUsers { get; set; }
    }

    public class ReportCardOptionVM
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class ReportSelectionRequest
    {
        public List<string> SelectedCards { get; set; } = new();
        public List<ReportFilterSelection> Filters { get; set; } = new();
    }

    public class ReportFilterSelection
    {
        public string Key { get; set; } = string.Empty;
        public List<string> Values { get; set; } = new();
    }

    public class ReportFilterOptionVM
    {
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }
}
