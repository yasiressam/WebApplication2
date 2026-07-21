namespace WebApplication2.Models.Audit
{
    public static class AuditTrailDisplayFormatter
    {
        public static string ToArabicErrorType(string? exceptionType)
        {
            return exceptionType switch
            {
                "ConnectionResetException" => "انقطاع اتصال العميل",
                "OperationCanceledException" => "تم إلغاء الطلب",
                "TaskCanceledException" => "انتهت العملية قبل اكتمالها",
                "UnauthorizedAccessException" => "محاولة وصول غير مصرح بها",
                "InvalidOperationException" => "عملية غير صالحة",
                "ArgumentNullException" => "بيانات مطلوبة غير مكتملة",
                "ArgumentException" => "بيانات غير صالحة",
                "JsonException" => "بيانات الطلب غير صحيحة",
                "DbUpdateException" => "تعذر حفظ البيانات",
                "SqlException" => "خطأ في قاعدة البيانات",
                "Exception" => "خطأ عام",
                null or "" => "خطأ عام",
                _ => exceptionType
            };
        }

        public static string ToArabicErrorMessage(Exception exception)
        {
            return exception switch
            {
                OperationCanceledException => "تم إلغاء الطلب قبل اكتماله",
                _ when exception.GetType().Name == "ConnectionResetException" => "تم قطع الاتصال من جهة العميل",
                UnauthorizedAccessException => "لا توجد صلاحية لتنفيذ هذه العملية",
                InvalidOperationException => "تعذر تنفيذ العملية المطلوبة",
                ArgumentNullException => "بعض البيانات المطلوبة غير موجودة",
                ArgumentException => "البيانات المرسلة غير صحيحة",
                System.Text.Json.JsonException => "تعذر قراءة بيانات الطلب",
                _ => NormalizeArabicMessage(exception.Message)
            };
        }

        public static string BuildShortDetails(Exception exception)
        {
            var message = NormalizeArabicMessage(exception.Message);
            if (string.IsNullOrWhiteSpace(message) || message == ToArabicErrorMessage(exception))
            {
                return string.Empty;
            }

            return message.Length > 180 ? message[..180] + "..." : message;
        }

        public static string GetEventTitle(AuditLogEntry entry)
        {
            var translatedEventType = TranslateEventType(entry.EventType);
            if (!string.IsNullOrWhiteSpace(translatedEventType) &&
                !string.Equals(translatedEventType, "إجراء", StringComparison.Ordinal))
            {
                return translatedEventType;
            }

            return GetOperationLabel(entry);
        }

        public static string GetOperationLabel(AuditLogEntry entry)
        {
            var message = TranslateMessage(entry.Message);
            if (!string.IsNullOrWhiteSpace(message) && message != "-")
            {
                return message;
            }

            var pathLabel = TranslatePath(entry.Path);
            if (!string.IsNullOrWhiteSpace(pathLabel) && pathLabel != "عملية غير معروفة")
            {
                return pathLabel;
            }

            return "عملية غير معروفة";
        }

        public static string GetRequestLabel(AuditLogEntry entry)
        {
            return GetOperationLabel(entry);
        }

        public static string TranslateEventType(string? eventType)
        {
            return eventType switch
            {
                "Action" => "إجراء",
                "LoginSuccess" => "تسجيل دخول ناجح",
                "LoginFailed" => "فشل تسجيل الدخول",
                "Logout" => "تسجيل الخروج",
                "ConnectionResetException" => "انقطاع اتصال العميل",
                "UnauthorizedAccessException" => "محاولة وصول غير مصرح بها",
                "InvalidOperationException" => "عملية غير صالحة",
                "ArgumentNullException" => "قيمة مطلوبة غير موجودة",
                "ArgumentException" => "بيانات غير صالحة",
                "Exception" => "خطأ عام",
                null or "" => string.Empty,
                _ => ToArabicErrorType(eventType)
            };
        }

        public static string TranslateMessage(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return "-";
            }

            if (LooksArabic(message))
            {
                return message;
            }

            return message switch
            {
                "SuperAdmin/AuditTrail" => "عرض سجل النظام",
                "SuperAdmin/LoginAudit" => "عرض سجل الدخول",
                "SuperAdmin/ErrorAudit" => "عرض سجل الأخطاء",
                "SuperAdmin/ActivityAudit" => "عرض سجل الحركات",
                "SuperAdmin/DeleteUser" => "حذف مستخدم",
                "SuperAdmin/BulkDeleteUsers" => "حذف مجموعة مستخدمين",
                "SuperAdmin/Users" => "عرض المستخدمين",
                "SuperAdmin/UserDetails" => "عرض تفاصيل المستخدم",
                "SuperAdmin/EditUser" => "تعديل بيانات المستخدم",
                "SuperAdmin/Reports" => "عرض التقارير",
                "SuperAdmin/SendNotification" => "فتح صفحة إرسال الإشعارات",
                "SuperAdmin/BackupDatabase" => "إنشاء نسخة احتياطية",
                "SuperAdmin/RestoreDatabase" => "استعادة نسخة احتياطية",
                "Admin/DeleteUser" => "حذف مستخدم",
                "Admin/BulkDeleteUsers" => "حذف مجموعة مستخدمين",
                "Admin/Users" => "عرض المستخدمين",
                "Admin/EditUser" => "تعديل بيانات المستخدم",
                "Admin/SendNotification" => "فتح صفحة إرسال الإشعارات",
                "Account/CreateUserByAdmin" => "إنشاء مستخدم بواسطة الإدارة",
                "The client has disconnected" => "تم قطع الاتصال من جهة العميل قبل اكتمال الطلب",
                _ => message
            };
        }

        public static string TranslatePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "عملية غير معروفة";
            }

            return path switch
            {
                "/SuperAdmin/AuditTrail" => "صفحة سجل النظام",
                "/SuperAdmin/LoginAudit" => "صفحة سجل الدخول",
                "/SuperAdmin/ErrorAudit" => "صفحة سجل الأخطاء",
                "/SuperAdmin/ActivityAudit" => "صفحة سجل الحركات",
                "/SuperAdmin/DeleteUser" => "عملية حذف مستخدم",
                "/SuperAdmin/BulkDeleteUsers" => "عملية حذف مجموعة مستخدمين",
                "/SuperAdmin/Users" => "صفحة المستخدمين",
                "/SuperAdmin/UserDetails" => "صفحة تفاصيل المستخدم",
                "/SuperAdmin/EditUser" => "صفحة تعديل المستخدم",
                "/SuperAdmin/Reports" => "صفحة التقارير",
                "/SuperAdmin/SendNotification" => "صفحة إرسال الإشعارات",
                "/SuperAdmin/BackupDatabase" => "عملية إنشاء نسخة احتياطية",
                "/SuperAdmin/RestoreDatabase" => "عملية استعادة نسخة احتياطية",
                "/Admin/Users" => "صفحة المستخدمين",
                "/Admin/DeleteUser" => "عملية حذف مستخدم",
                "/Admin/BulkDeleteUsers" => "عملية حذف مجموعة مستخدمين",
                "/Admin/EditUser" => "صفحة تعديل المستخدم",
                "/api/notifications/mark-read" => "تعليم إشعار كمقروء",
                "/api/notifications/mark-all-read" => "تعليم كل الإشعارات كمقروءة",
                "/api/notifications/get" => "جلب الإشعارات",
                "/Identity/Account/Login" => "صفحة تسجيل الدخول",
                "/Identity/Account/Logout" => "عملية تسجيل الخروج",
                _ => BuildPathLabel(path)
            };
        }

        private static string BuildPathLabel(string path)
        {
            var cleanPath = path.Split('?', '#')[0].Trim('/');
            if (string.IsNullOrWhiteSpace(cleanPath))
            {
                return "الصفحة الرئيسية";
            }

            var segments = cleanPath
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(TranslateSegment)
                .Where(segment => !string.IsNullOrWhiteSpace(segment))
                .ToArray();

            if (segments.Length == 0)
            {
                return "عملية غير معروفة";
            }

            if (segments.Length == 1)
            {
                return $"صفحة {segments[0]}";
            }

            return string.Join(" / ", segments);
        }

        private static string TranslateSegment(string segment)
        {
            return segment switch
            {
                "SuperAdmin" => "السوبر أدمن",
                "Admin" => "الإدارة",
                "Identity" => "الهوية",
                "Account" => "الحساب",
                "Login" => "تسجيل الدخول",
                "Logout" => "تسجيل الخروج",
                "Notifications" => "الإشعارات",
                "Request" => "الطلبات",
                "Requests" => "الطلبات",
                "Reports" => "التقارير",
                "Users" => "المستخدمون",
                "UserDetails" => "تفاصيل المستخدم",
                "EditUser" => "تعديل المستخدم",
                "AuditTrail" => "سجل النظام",
                "LoginAudit" => "سجل الدخول",
                "ErrorAudit" => "سجل الأخطاء",
                "ActivityAudit" => "سجل الحركات",
                "mark-read" => "تعليم إشعار كمقروء",
                "mark-all-read" => "تعليم كل الإشعارات كمقروءة",
                "get" => "جلب البيانات",
                "api" => "واجهة النظام",
                _ => segment
            };
        }

        private static bool LooksArabic(string value)
        {
            return value.Any(ch => ch >= '\u0600' && ch <= '\u06FF');
        }

        private static string NormalizeArabicMessage(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return "حدث خطأ أثناء تنفيذ العملية";
            }

            if (LooksArabic(message))
            {
                return message.Length > 180 ? message[..180] + "..." : message;
            }

            return message switch
            {
                "The client has disconnected" => "تم قطع الاتصال من جهة العميل",
                _ => "حدث خطأ أثناء تنفيذ العملية"
            };
        }
    }
}
