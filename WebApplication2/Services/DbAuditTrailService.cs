using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models.Audit;

namespace WebApplication2.Services
{
    public class DbAuditTrailService : IAuditTrailService
    {
        private readonly ApplicationDbContext _context;

        public DbAuditTrailService(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task LogLoginAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
            => AppendAsync("login", entry, cancellationToken);

        public Task LogErrorAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
            => AppendAsync("error", entry, cancellationToken);

        public Task LogActivityAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
            => AppendAsync("activity", entry, cancellationToken);

        public async Task<AuditTrailViewModel> GetTrailAsync(int limitPerCategory = 12, CancellationToken cancellationToken = default)
        {
            var loginEntries = await ReadRecentAsync("login", limitPerCategory, cancellationToken);
            var errorEntries = await ReadRecentAsync("error", limitPerCategory, cancellationToken);
            var activityEntries = await ReadRecentAsync("activity", limitPerCategory, cancellationToken);

            return new AuditTrailViewModel
            {
                LoginEntries = loginEntries,
                ErrorEntries = errorEntries,
                ActivityEntries = activityEntries
            };
        }

        private async Task AppendAsync(string category, AuditLogEntry entry, CancellationToken cancellationToken)
        {
            entry.Category = category;
            entry.TimestampUtc = entry.TimestampUtc == default ? DateTime.UtcNow : entry.TimestampUtc;

            _context.AuditLogs.Add(entry);
            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task<IReadOnlyList<AuditLogEntry>> ReadRecentAsync(string category, int limit, CancellationToken cancellationToken)
        {
            return await _context.AuditLogs
                .AsNoTracking()
                .Where(entry => entry.Category == category)
                .OrderByDescending(entry => entry.TimestampUtc)
                .Take(limit)
                .ToListAsync(cancellationToken);
        }
    }
}
