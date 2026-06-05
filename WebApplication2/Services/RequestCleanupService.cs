using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;

namespace WebApplication2.Services
{
    public class RequestCleanupService : BackgroundService
    {
        private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(24);
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
                try
                {
                    await CleanupExpiredRequestsAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خطأ أثناء حذف الطلبات القديمة");
                }

                try
                {
                    await Task.Delay(CleanupInterval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        private async Task CleanupExpiredRequestsAsync(CancellationToken cancellationToken)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-30);

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var expiredRequests = await context.Requests
                .Where(r => r.CreatedAt < cutoffDate)
                .Select(r => new
                {
                    r.Id,
                    r.AttachmentPath
                })
                .ToListAsync(cancellationToken);

            if (expiredRequests.Count == 0)
                return;

            var requestIds = expiredRequests.Select(r => r.Id).ToList();
            var requestLinks = requestIds.Select(id => $"/Request/Details/{id}").ToList();

            var recipients = await context.RequestRecipients
                .Where(r => requestIds.Contains(r.RequestId))
                .ToListAsync(cancellationToken);

            var replies = await context.RequestReplies
                .Where(r => requestIds.Contains(r.RequestId))
                .ToListAsync(cancellationToken);

            var notifications = await context.Notifications
                .Where(n => n.ClickUrl != null && requestLinks.Contains(n.ClickUrl))
                .ToListAsync(cancellationToken);

            var requests = await context.Requests
                .Where(r => requestIds.Contains(r.Id))
                .ToListAsync(cancellationToken);

            context.RequestRecipients.RemoveRange(recipients);
            context.RequestReplies.RemoveRange(replies);
            context.Notifications.RemoveRange(notifications);
            context.Requests.RemoveRange(requests);

            await context.SaveChangesAsync(cancellationToken);

            foreach (var request in expiredRequests)
            {
                DeleteAttachmentFile(request.AttachmentPath);
            }

            _logger.LogInformation("تم حذف {Count} طلب قديم تجاوز 30 يوم", expiredRequests.Count);
        }

        private void DeleteAttachmentFile(string? attachmentPath)
        {
            if (string.IsNullOrWhiteSpace(attachmentPath))
                return;

            var fileName = Path.GetFileName(attachmentPath);
            if (string.IsNullOrWhiteSpace(fileName))
                return;

            var filePath = Path.Combine(_requestUploadPath, fileName);
            if (!File.Exists(filePath))
                return;

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
