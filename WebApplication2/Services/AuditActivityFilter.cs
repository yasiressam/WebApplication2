using Microsoft.AspNetCore.Mvc.Filters;
using WebApplication2.Models.Audit;

namespace WebApplication2.Services
{
    public class AuditActivityFilter : IAsyncActionFilter
    {
        private static readonly HashSet<string> ImportantGetActions = new(StringComparer.OrdinalIgnoreCase)
        {
            "BackupDatabase",
            "RestoreDatabase",
            "AuditTrail",
            "Reports",
            "SendNotification"
        };

        private readonly IAuditTrailService _auditTrailService;

        public AuditActivityFilter(IAuditTrailService auditTrailService)
        {
            _auditTrailService = auditTrailService;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var executedContext = await next();

            if (executedContext.Exception != null || executedContext.HttpContext.User.Identity?.IsAuthenticated != true)
            {
                return;
            }

            var request = executedContext.HttpContext.Request;
            var method = request.Method;
            var actionName = context.ActionDescriptor.RouteValues.TryGetValue("action", out var action) ? action : string.Empty;
            var controllerName = context.ActionDescriptor.RouteValues.TryGetValue("controller", out var controller) ? controller : string.Empty;

            if (HttpMethods.IsGet(method) && !ImportantGetActions.Contains(actionName ?? string.Empty))
            {
                return;
            }

            if (!HttpMethods.IsGet(method) &&
                !HttpMethods.IsPost(method) &&
                !HttpMethods.IsPut(method) &&
                !HttpMethods.IsPatch(method) &&
                !HttpMethods.IsDelete(method))
            {
                return;
            }

            var user = executedContext.HttpContext.User;
            var entry = new AuditLogEntry
            {
                TimestampUtc = DateTime.UtcNow,
                Severity = "Information",
                EventType = "Action",
                Message = GetArabicActionMessage(controllerName, actionName),
                UserId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                UserEmail = user.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
                UserName = user.Identity?.Name,
                IpAddress = executedContext.HttpContext.Connection.RemoteIpAddress?.ToString(),
                Path = request.Path,
                HttpMethod = method,
                Details = BuildDetails(context.ActionArguments)
            };

            await _auditTrailService.LogActivityAsync(entry);
        }

        private static string GetArabicActionMessage(string? controllerName, string? actionName)
        {
            var controller = controllerName ?? string.Empty;
            var action = actionName ?? string.Empty;

            return (controller, action) switch
            {
                ("SuperAdmin", "AuditTrail") => "عرض سجل النظام",
                ("SuperAdmin", "LoginAudit") => "عرض سجل الدخول",
                ("SuperAdmin", "ErrorAudit") => "عرض سجل الأخطاء",
                ("SuperAdmin", "ActivityAudit") => "عرض سجل الحركات",
                ("SuperAdmin", "DeleteUser") => "حذف مستخدم",
                ("SuperAdmin", "BulkDeleteUsers") => "حذف مجموعة مستخدمين",
                ("SuperAdmin", "Users") => "عرض المستخدمين",
                ("SuperAdmin", "UserDetails") => "عرض تفاصيل المستخدم",
                ("SuperAdmin", "EditUser") => "تعديل بيانات المستخدم",
                ("SuperAdmin", "Reports") => "عرض التقارير",
                ("SuperAdmin", "SendNotification") => "فتح صفحة إرسال الإشعارات",
                ("SuperAdmin", "BackupDatabase") => "إنشاء نسخة احتياطية",
                ("SuperAdmin", "RestoreDatabase") => "استعادة نسخة احتياطية",
                ("Admin", "DeleteUser") => "حذف مستخدم",
                ("Admin", "BulkDeleteUsers") => "حذف مجموعة مستخدمين",
                ("Admin", "Users") => "عرض المستخدمين",
                ("Admin", "EditUser") => "تعديل بيانات المستخدم",
                ("Admin", "SendNotification") => "فتح صفحة إرسال الإشعارات",
                ("Account", "CreateUserByAdmin") => "إنشاء مستخدم بواسطة الإدارة",
                _ => string.IsNullOrWhiteSpace(action)
                    ? controller
                    : $"{TranslateController(controller)} / {TranslateAction(action)}"
            };
        }

        private static string TranslateController(string controller) => controller switch
        {
            "SuperAdmin" => "السوبر أدمن",
            "Admin" => "الأدمن",
            "Account" => "الحسابات",
            "Register" => "التسجيل",
            "Request" => "الطلبات",
            "Notifications" => "الإشعارات",
            "News" => "الأخبار",
            _ => controller
        };

        private static string TranslateAction(string action) => action switch
        {
            "Index" => "عرض الصفحة",
            "Create" => "إنشاء",
            "Edit" => "تعديل",
            "Delete" => "حذف",
            "Details" => "عرض التفاصيل",
            "Login" => "تسجيل الدخول",
            "Logout" => "تسجيل الخروج",
            _ => action
        };

        private static string BuildDetails(IDictionary<string, object?> actionArguments)
        {
            if (actionArguments.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(" | ", actionArguments.Select(kvp => $"{TranslateArgumentName(kvp.Key)}: {ToLogValue(kvp.Value)}"));
        }

        private static string ToLogValue(object? value)
        {
            if (value == null)
            {
                return "فارغ";
            }

            var text = value.ToString() ?? string.Empty;
            return text.Length > 160 ? text[..160] + "..." : text;
        }

        private static string TranslateArgumentName(string argumentName) => argumentName switch
        {
            "id" => "المعرف",
            "userId" => "معرف المستخدم",
            "requestId" => "معرف الطلب",
            "model" => "البيانات",
            "returnUrl" => "رابط العودة",
            _ => argumentName
        };
    }
}
