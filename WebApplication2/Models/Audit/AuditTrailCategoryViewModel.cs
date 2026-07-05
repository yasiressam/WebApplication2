namespace WebApplication2.Models.Audit
{
    public class AuditTrailCategoryViewModel
    {
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public IReadOnlyList<AuditLogEntry> Entries { get; set; } = Array.Empty<AuditLogEntry>();
    }
}
