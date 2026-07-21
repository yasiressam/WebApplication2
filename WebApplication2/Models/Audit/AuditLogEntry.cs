namespace WebApplication2.Models.Audit
{
    public class AuditLogEntry
    {
        public int Id { get; set; }
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        public string Category { get; set; } = string.Empty;
        public string Severity { get; set; } = "Information";
        public string EventType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? UserId { get; set; }
        public string? UserEmail { get; set; }
        public string? UserName { get; set; }
        public string? IpAddress { get; set; }
        public string? Path { get; set; }
        public string? HttpMethod { get; set; }
        public string? Details { get; set; }
    }
}
