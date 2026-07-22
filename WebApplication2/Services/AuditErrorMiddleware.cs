using WebApplication2.Models.Audit;

namespace WebApplication2.Services
{
    public class AuditErrorMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AuditErrorMiddleware> _logger;

        public AuditErrorMiddleware(
            RequestDelegate next,
            ILogger<AuditErrorMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(
            HttpContext context,
            IAuditTrailService auditTrailService)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                try
                {
                    var user = context.User;

                    await auditTrailService.LogErrorAsync(new AuditLogEntry
                    {
                        TimestampUtc = DateTime.UtcNow,
                        Severity = "Error",
                        EventType =
                            AuditTrailDisplayFormatter.ToArabicErrorType(
                                ex.GetType().Name),

                        Message =
                            AuditTrailDisplayFormatter.ToArabicErrorMessage(ex),

                        UserId = user.FindFirst(
                            System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,

                        UserEmail = user.FindFirst(
                            System.Security.Claims.ClaimTypes.Email)?.Value,

                        UserName = user.Identity?.Name,

                        IpAddress =
                            context.Connection.RemoteIpAddress?.ToString(),

                        Path = context.Request.Path,

                        HttpMethod = context.Request.Method,

                        Details = ex.ToString()
                    });
                }
                catch (Exception auditException)
                {
                    _logger.LogError(
                        auditException,
                        "فشل تسجيل الخطأ في قاعدة البيانات. الخطأ الأصلي: {OriginalError}",
                        ex.Message);
                }

                throw;
            }
        }
    }
}
