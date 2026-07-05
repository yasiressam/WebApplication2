using WebApplication2.Models.Audit;

namespace WebApplication2.Services
{
    public interface IAuditTrailService
    {
        Task LogLoginAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);
        Task LogErrorAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);
        Task LogActivityAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);
        Task<AuditTrailViewModel> GetTrailAsync(int limitPerCategory = 12, CancellationToken cancellationToken = default);
    }
}
