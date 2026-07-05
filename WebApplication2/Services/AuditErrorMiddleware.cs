using WebApplication2.Models.Audit;

namespace WebApplication2.Services
{
    public class AuditErrorMiddleware
    {
        private readonly RequestDelegate _next;

        public AuditErrorMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IAuditTrailService auditTrailService)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                var user = context.User;
                await auditTrailService.LogErrorAsync(new AuditLogEntry
                {
                    TimestampUtc = DateTime.UtcNow,
                    Severity = "Error",
                    EventType = ex.GetType().Name,
                    Message = ex.Message,
                    UserId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                    UserEmail = user.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
                    UserName = user.Identity?.Name,
                    IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                    Path = context.Request.Path,
                    HttpMethod = context.Request.Method,
                    Details = ex.StackTrace
                });

                throw;
            }
        }
    }
}
