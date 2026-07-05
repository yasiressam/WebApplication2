namespace WebApplication2.Models.Audit
{
    public class AuditTrailViewModel
    {
        public IReadOnlyList<AuditLogEntry> LoginEntries { get; set; } = Array.Empty<AuditLogEntry>();
        public IReadOnlyList<AuditLogEntry> ErrorEntries { get; set; } = Array.Empty<AuditLogEntry>();
        public IReadOnlyList<AuditLogEntry> ActivityEntries { get; set; } = Array.Empty<AuditLogEntry>();
        public int LoginCount => LoginEntries.Count;
        public int ErrorCount => ErrorEntries.Count;
        public int ActivityCount => ActivityEntries.Count;
    }
}
