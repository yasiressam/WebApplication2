using System.Text;
using System.Text.Json;
using WebApplication2.Models.Audit;

namespace WebApplication2.Services
{
    public class FileAuditTrailService : IAuditTrailService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = false
        };

        private static readonly SemaphoreSlim WriteLock = new(1, 1);
        private readonly IWebHostEnvironment _environment;

        public FileAuditTrailService(IWebHostEnvironment environment)
        {
            _environment = environment;
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

            var filePath = GetFilePath(category);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            var payload = JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine;
            await WriteLock.WaitAsync(cancellationToken);
            try
            {
                await File.AppendAllTextAsync(filePath, payload, Encoding.UTF8, cancellationToken);
            }
            finally
            {
                WriteLock.Release();
            }
        }

        private async Task<IReadOnlyList<AuditLogEntry>> ReadRecentAsync(string category, int limit, CancellationToken cancellationToken)
        {
            var filePath = GetFilePath(category);
            if (!File.Exists(filePath))
            {
                return Array.Empty<AuditLogEntry>();
            }

            var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
            var results = new List<AuditLogEntry>();

            for (var i = lines.Length - 1; i >= 0 && results.Count < limit; i--)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    continue;
                }

                try
                {
                    var entry = JsonSerializer.Deserialize<AuditLogEntry>(lines[i], JsonOptions);
                    if (entry != null)
                    {
                        results.Add(entry);
                    }
                }
                catch (JsonException)
                {
                }
            }

            return results;
        }

        private string GetFilePath(string category)
        {
            return Path.Combine(_environment.ContentRootPath, "App_Data", "AuditLogs", $"{category}.jsonl");
        }
    }
}
