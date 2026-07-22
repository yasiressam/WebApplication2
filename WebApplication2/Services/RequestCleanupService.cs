using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;

namespace WebApplication2.Services
{
    public class RequestCleanupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<RequestCleanupService> _logger;
        private readonly string _requestUploadPath;

        public RequestCleanupService(IServiceScopeFactory scopeFactory, ILogger<RequestCleanupService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _requestUploadPath = Path.Combine("C:\\Users", "Public", "MyApp_Uploads", "Requests");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = CleanupSchedule.GetDelayUntilNextRun(DateTimeOffset.Now);

                try
                {
                    _logger.LogInformation("تمت جدولة تنظيف الطلبات بعد {Delay}.", delay);
                    await Task.Delay(delay, stoppingToken);
                    await CleanupExpiredRequestsAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "حدث خطأ أثناء حذف الطلبات القديمة.");
                }
            }
        }

        private async Task CleanupExpiredRequestsAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var cutoffDate = DateTime.UtcNow.AddDays(-30);
            var deletedCount = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                var expiredRequests = await context.Requests
                    .Where(r => r.CreatedAt < cutoffDate)
                    .OrderBy(r => r.Id)
                    .Select(r => new
                    {
                        r.Id,
                        r.AttachmentPath
                    })
                    .Take(CleanupBatching.BatchSize)
                    .ToListAsync(cancellationToken);

                if (expiredRequests.Count == 0)
                {
                    break;
                }

                var requestIds = expiredRequests.Select(r => r.Id).ToList();
                var requestLinks = requestIds.Select(id => $"/Request/Details/{id}").ToList();

                await context.RequestRecipients
                    .Where(r => requestIds.Contains(r.RequestId))
                    .ExecuteDeleteAsync(cancellationToken);

                await context.RequestReplies
                    .Where(r => requestIds.Contains(r.RequestId))
                    .ExecuteDeleteAsync(cancellationToken);

                await context.Notifications
                    .Where(n => n.ClickUrl != null && requestLinks.Contains(n.ClickUrl))
                    .ExecuteDeleteAsync(cancellationToken);

                await context.Requests
                    .Where(r => requestIds.Contains(r.Id))
                    .ExecuteDeleteAsync(cancellationToken);

                foreach (var request in expiredRequests)
                {
                    DeleteAttachmentFile(request.AttachmentPath);
                }

                deletedCount += expiredRequests.Count;
            }

            if (deletedCount > 0)
            {
                _logger.LogInformation("تم حذف {Count} طلب قديم تجاوز 30 يوماً.", deletedCount);
            }
        }

        private void DeleteAttachmentFile(string? attachmentPath)
        {
            if (string.IsNullOrWhiteSpace(attachmentPath))
            {
                return;
            }

            var fileName = Path.GetFileName(attachmentPath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return;
            }

            var filePath = Path.Combine(_requestUploadPath, fileName);
            if (!File.Exists(filePath))
            {
                return;
            }

            try
            {
                File.Delete(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "تعذر حذف مرفق طلب قديم: {FilePath}", filePath);
            }
        }
    }
}
