using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WebApplication2.Data;

using WebApplication2.Models;
using WebApplication2.Models.Audit;
using WebApplication2.Models.Profile;
using WebApplication2.Services;

namespace WebApplication2.Controllers
{
    [Authorize(Roles = clsRoles.SuperAdminOrSystemManager)]
    public class SuperAdminController : Controller
    {
        private const string SqlBackupDirectoryFallback = @"C:\Program Files\Microsoft SQL Server\MSSQL17.MSSQLSERVER\MSSQL\Backup";
        private const string ReportNotificationFiltersSessionKey = "SuperAdmin.ReportNotificationFilters";
        private readonly UserManager<IdentityUser> _userManager;
        private readonly INotificationService _notificationService;
        private readonly ILogger<SuperAdminController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IAuditTrailService _auditTrailService;
        private readonly OnlineUsersTracker _onlineUsersTracker;
        private static readonly string[] ReportIncludedRoleNames =
        {
            clsRoles.Member,
            clsRoles.Admin,
            clsRoles.SuperAdmin,
            clsRoles.DistrictAdmin,
            clsRoles.Manager,
            clsRoles.AssistantManager
        };

        public SuperAdminController(
            UserManager<IdentityUser> userManager,
            ApplicationDbContext context,
            INotificationService notificationService,
            ILogger<SuperAdminController> logger,
            IConfiguration configuration,
            IAuditTrailService auditTrailService,
            OnlineUsersTracker onlineUsersTracker)
        {
            _userManager = userManager;
            _context = context;
            _notificationService = notificationService;
            _logger = logger;
            _configuration = configuration;
            _auditTrailService = auditTrailService;
            _onlineUsersTracker = onlineUsersTracker;
        }

        private bool IsCurrentUserSystemManager()
        {
            return User.IsInRole(clsRoles.SystemManager);
        }

        private bool CanCurrentUserManageSystemManager()
        {
            return IsCurrentUserSystemManager();
        }

        private async Task<bool> IsCurrentUserPasswordResetAllowedAsync()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return false;

            var allowedEmails = _configuration
                .GetSection("SuperAdminPasswordReset:AllowedEmails")
                .Get<string[]>() ?? Array.Empty<string>();

            return allowedEmails.Any(email =>
                string.Equals(email?.Trim(), currentUser.Email, StringComparison.OrdinalIgnoreCase));
        }

        // ===== دوال مساعدة لجلب البيانات المرتبطة =====
        private async Task<Address?> GetUserAddressAsync(string userId)
        {
            var address = await _context.Addresses
                .FirstOrDefaultAsync(a => a.UserId == userId);
            if (address == null)
            {
                _logger.LogWarning($"⚠️ [GetUserAddressAsync] لم يتم العثور على عنوان للمستخدم {userId}");
            }
            return address;
        }

        private async Task<VoterCard?> GetUserVoterCardAsync(string userId)
        {
            return await _context.VoterCards
                .FirstOrDefaultAsync(v => v.UserId == userId);
        }

        private async Task<UnionMembership?> GetUserUnionAsync(string userId)
        {
            return await _context.UnionMemberships
                .FirstOrDefaultAsync(u => u.UserId == userId);
        }

        private async Task<FederationMembership?> GetUserFederationAsync(string userId)
        {
            return await _context.FederationMemberships
                .Include(f => f.Federation)
                .Include(f => f.FederationDivision)
                .Include(f => f.FederationSection)
                .Include(f => f.FederationGroup)
                .FirstOrDefaultAsync(f => f.UserId == userId);
        }

        private async Task<AssociationMembership?> GetUserAssociationAsync(string userId)
        {
            return await _context.AssociationMemberships
                .FirstOrDefaultAsync(a => a.UserId == userId);
        }

        private async Task<NgoMembership?> GetUserNgoAsync(string userId)
        {
            return await _context.NgoMemberships
                .FirstOrDefaultAsync(n => n.UserId == userId);
        }

        private async Task<AffiliationInfo?> GetUserAffiliationInfoAsync(string userId)
        {
            return await _context.AffiliationInfos
                .Include(a => a.AffiliationEntity)
                .Include(a => a.Division)
                .Include(a => a.Section)
                .Include(a => a.Group)
                .FirstOrDefaultAsync(a => a.UserId == userId);
        }

        private async Task<Identify?> GetUserProfileAsync(string userId)
        {
            return await _context.Identifies
                .Include(i => i.WorkLocation)
                .FirstOrDefaultAsync(i => i.UserId == userId);
        }

        private string GetEffectiveGovernorate(Identify? profile, Address? address)
        {
            var workLocation = profile != null
                ? _context.WorkLocations.AsNoTracking().FirstOrDefault(w => w.IdentifyId == profile.Id) ?? profile.WorkLocation
                : null;

            return !string.IsNullOrWhiteSpace(workLocation?.Governorate)
                ? workLocation.Governorate
                : !string.IsNullOrWhiteSpace(profile?.WorkGovernorate)
                ? profile.WorkGovernorate
                : string.Empty;
        }

        private string GetEffectiveDistrict(Identify? profile, Address? address)
        {
            var workLocation = profile != null
                ? _context.WorkLocations.AsNoTracking().FirstOrDefault(w => w.IdentifyId == profile.Id) ?? profile.WorkLocation
                : null;

            if (!string.IsNullOrWhiteSpace(workLocation?.Governorate) && workLocation.Governorate == "بغداد")
            {
                return workLocation.District ?? string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(profile?.WorkGovernorate) && profile.WorkGovernorate == "بغداد")
            {
                return profile.WorkDistrict ?? string.Empty;
            }

            return string.Empty;
        }

        // ===== دوال مساعدة للحصول على أسماء الكيانات =====
        private async Task<string?> GetAffiliationEntityNameAsync(int? id)
        {
            if (!id.HasValue) return null;
            var entity = await _context.AffiliationEntities
                .FirstOrDefaultAsync(e => e.Id == id.Value);
            return entity?.Name;
        }

        private async Task<string?> GetDivisionNameAsync(int? id)
        {
            if (!id.HasValue) return null;
            var division = await _context.Divisions
                .FirstOrDefaultAsync(d => d.Id == id.Value);
            return division?.Name;
        }

        private async Task<string?> GetSectionNameAsync(int? id)
        {
            if (!id.HasValue) return null;
            var section = await _context.Sections
                .FirstOrDefaultAsync(s => s.Id == id.Value);
            return section?.Name;
        }

        private async Task<string?> GetGroupNameAsync(int? id)
        {
            if (!id.HasValue) return null;
            var group = await _context.Groups
                .FirstOrDefaultAsync(g => g.Id == id.Value);
            return group?.Name;
        }

        // ✅ دالة مساعدة للحصول على الاسم الكامل للاتحاد من المستويات الأربعة
        private string GetFederationFullName(FederationMembership? federation)
        {
            if (federation == null) return "";

            string fullName = "";

            if (federation.Federation != null)
                fullName = federation.Federation.Name;

            if (federation.FederationDivision != null)
                fullName += " - " + federation.FederationDivision.Name;

            if (federation.FederationSection != null)
                fullName += " - " + federation.FederationSection.Name;

            if (federation.FederationGroup != null)
                fullName += " - " + federation.FederationGroup.Name;

            return fullName;
        }

        // ========== دوال المسؤولية الإدارية ==========
        private string GetArabicLevelName(string level, string role)
        {
            var prefix = role == "Assistant" ? "معاون" : "مسؤول";

            return level switch
            {
                "Entity" => $"{prefix} جهة",
                "Division" => $"{prefix} قسم",
                "Section" => $"{prefix} شعبة",
                "Group" => $"{prefix} وحدة",
                _ => prefix
            };
        }

        private async Task<string> GetManagedEntityDisplayNameAsync(
            string level,
            int? entityId,
            int? divisionId,
            int? sectionId,
            int? groupId)
        {
            switch (level)
            {
                case "Entity":
                    if (entityId.HasValue)
                    {
                        var entity = await _context.AffiliationEntities
                            .FirstOrDefaultAsync(e => e.Id == entityId.Value);
                        return entity?.Name ?? string.Empty;
                    }
                    break;
                case "Division":
                    if (divisionId.HasValue)
                    {
                        var division = await _context.Divisions
                            .FirstOrDefaultAsync(d => d.Id == divisionId.Value);
                        return division?.Name ?? string.Empty;
                    }
                    break;
                case "Section":
                    if (sectionId.HasValue)
                    {
                        var section = await _context.Sections
                            .FirstOrDefaultAsync(s => s.Id == sectionId.Value);
                        return section?.Name ?? string.Empty;
                    }
                    break;
                case "Group":
                    if (groupId.HasValue)
                    {
                        var group = await _context.Groups
                            .FirstOrDefaultAsync(g => g.Id == groupId.Value);
                        return group?.Name ?? string.Empty;
                    }
                    break;
            }
            return string.Empty;
        }

        // ========== الصفحة الرئيسية ==========
        public async Task<IActionResult> Index()
        {
            ViewBag.TotalUsers = await _userManager.Users.CountAsync();
            ViewBag.TotalMembers = await _context.Identifies.AsNoTracking().CountAsync(i =>
                !string.IsNullOrWhiteSpace(i.Education) &&
                (i.AccountType == "فرد" || i.IsPromoted));
            ViewBag.PendingRequests = await _context.Identifies.AsNoTracking().CountAsync(i => i.RequestedPromotion);
            ViewBag.NewThisWeek = await _context.Identifies.AsNoTracking().CountAsync(i => i.CreatedAt > DateTime.UtcNow.AddDays(-7));
            ViewBag.ActiveUsers = ViewBag.TotalUsers;
            ViewBag.Admins = await _context.UserRoles
                .Join(_context.Roles,
                    userRole => userRole.RoleId,
                    role => role.Id,
                    (userRole, role) => new { role.Name })
                .CountAsync(x => x.Name == clsRoles.Admin);
            ViewBag.SuperAdmins = await _context.UserRoles
                .Join(_context.Roles,
                    userRole => userRole.RoleId,
                    role => role.Id,
                    (userRole, role) => new { role.Name })
                .CountAsync(x => x.Name == clsRoles.SuperAdmin);
            ViewBag.OnlineUsersCount = 0;
            ViewBag.OnlineUsers = new List<OnlineUserViewModel>();

            if (IsCurrentUserSystemManager())
            {
                var onlineUserIds = _onlineUsersTracker.GetOnlineUserIds();
                ViewBag.OnlineUsersCount = _onlineUsersTracker.OnlineUsersCount;

                if (onlineUserIds.Count > 0)
                {
                    ViewBag.OnlineUsers = await _userManager.Users
                        .AsNoTracking()
                        .Where(u => onlineUserIds.Contains(u.Id))
                        .GroupJoin(
                            _context.Identifies.AsNoTracking(),
                            user => user.Id,
                            identify => identify.UserId,
                            (user, identifies) => new { user, identify = identifies.FirstOrDefault() })
                        .Select(x => new OnlineUserViewModel
                        {
                            UserId = x.user.Id,
                            DisplayName = x.identify != null && !string.IsNullOrWhiteSpace(x.identify.FullName)
                                ? x.identify.FullName
                                : (!string.IsNullOrWhiteSpace(x.user.UserName) ? x.user.UserName : (x.user.Email ?? "مستخدم")),
                            Email = x.user.Email ?? string.Empty,
                            AccountType = x.identify != null && !string.IsNullOrWhiteSpace(x.identify.AccountType)
                                ? x.identify.AccountType
                                : "غير محدد"
                        })
                        .OrderBy(x => x.DisplayName)
                        .ToListAsync();
                }
            }

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> AuditTrail()
        {
            if (!await IsCurrentUserPasswordResetAllowedAsync())
            {
                return Forbid();
            }

            var model = await _auditTrailService.GetTrailAsync();
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> LoginAudit()
        {
            if (!await IsCurrentUserPasswordResetAllowedAsync())
            {
                return Forbid();
            }

            var model = await _auditTrailService.GetTrailAsync(50);
            return View("AuditTrailCategory", new AuditTrailCategoryViewModel
            {
                Title = "سجل الدخول",
                Subtitle = "أحدث محاولات الدخول الناجحة والفاشلة.",
                Icon = "bi bi-box-arrow-in-right",
                Entries = model.LoginEntries
            });
        }

        [HttpGet]
        public async Task<IActionResult> ErrorAudit()
        {
            if (!await IsCurrentUserPasswordResetAllowedAsync())
            {
                return Forbid();
            }

            var model = await _auditTrailService.GetTrailAsync(50);
            return View("AuditTrailCategory", new AuditTrailCategoryViewModel
            {
                Title = "سجل الأخطاء",
                Subtitle = "الأخطاء غير المعالجة التي ظهرت أثناء تشغيل النظام.",
                Icon = "bi bi-exclamation-octagon",
                Entries = model.ErrorEntries
            });
        }

        [HttpGet]
        public async Task<IActionResult> ActivityAudit()
        {
            if (!await IsCurrentUserPasswordResetAllowedAsync())
            {
                return Forbid();
            }

            var model = await _auditTrailService.GetTrailAsync(50);
            return View("AuditTrailCategory", new AuditTrailCategoryViewModel
            {
                Title = "سجل الحركات",
                Subtitle = "عمليات النظام المهمة والتنقلات الإدارية والإجراءات المؤثرة.",
                Icon = "bi bi-activity",
                Entries = model.ActivityEntries
            });
        }

        [HttpGet]
        public async Task<IActionResult> BackupDatabase()
        {
            if (!await IsCurrentUserPasswordResetAllowedAsync())
            {
                return Forbid();
            }

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> RestoreDatabase()
        {
            if (!await IsCurrentUserPasswordResetAllowedAsync())
            {
                return Forbid();
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreDatabase(IFormFile? backupFile)
        {
            string? uploadedBackupPath = null;

            try
            {
                if (!await IsCurrentUserPasswordResetAllowedAsync())
                {
                    return Forbid();
                }

                if (backupFile == null || backupFile.Length == 0)
                {
                    TempData["ErrorMessage"] = "يرجى اختيار ملف نسخة احتياطية بصيغة .bak.";
                    return RedirectToAction(nameof(RestoreDatabase));
                }

                var extension = Path.GetExtension(backupFile.FileName);
                if (!string.Equals(extension, ".bak", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["ErrorMessage"] = "نوع الملف غير مدعوم. يرجى رفع ملف نسخة احتياطية بصيغة .bak فقط.";
                    return RedirectToAction(nameof(RestoreDatabase));
                }

                var connectionString = _context.Database.GetConnectionString();
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    TempData["ErrorMessage"] = "تعذر العثور على إعدادات قاعدة البيانات.";
                    return RedirectToAction(nameof(RestoreDatabase));
                }

                var connectionBuilder = new SqlConnectionStringBuilder(connectionString);
                var databaseName = connectionBuilder.InitialCatalog;
                if (string.IsNullOrWhiteSpace(databaseName))
                {
                    TempData["ErrorMessage"] = "اسم قاعدة البيانات غير متوفر في إعدادات الاتصال.";
                    return RedirectToAction(nameof(RestoreDatabase));
                }

                var masterConnectionBuilder = new SqlConnectionStringBuilder(connectionString)
                {
                    InitialCatalog = "master"
                };

                await using var connection = new SqlConnection(masterConnectionBuilder.ConnectionString);
                await connection.OpenAsync();

                var backupDirectory = await GetSqlBackupDirectoryAsync(connection);
                if (string.IsNullOrWhiteSpace(backupDirectory))
                {
                    throw new InvalidOperationException("تعذر العثور على مجلد النسخ الاحتياطية الخاص بـ SQL Server.");
                }

                Directory.CreateDirectory(backupDirectory);

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var tempFileName = $"{databaseName}_restore_{timestamp}_{Guid.NewGuid():N}.bak";
                uploadedBackupPath = Path.Combine(backupDirectory, tempFileName);

                await using (var fileStream = new FileStream(uploadedBackupPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await backupFile.CopyToAsync(fileStream);
                }

                await RestoreDatabaseFromBackupAsync(connection, databaseName, uploadedBackupPath);
                await TryDeleteSqlBackupFileAsync(connection, uploadedBackupPath);
                uploadedBackupPath = null;

                SqlConnection.ClearAllPools();

                _logger.LogInformation(
                    "Database restore completed successfully by SuperAdmin. Database: {DatabaseName}, BackupFile: {BackupFile}",
                    databaseName,
                    backupFile.FileName);

                TempData["SuccessMessage"] = "تمت استعادة قاعدة البيانات بنجاح من الملف المرفوع.";
                return RedirectToAction(nameof(RestoreDatabase));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database restore failed.");
                TempData["ErrorMessage"] =
                    $"فشلت استعادة قاعدة البيانات. {ex.Message} تأكد من أن ملف النسخة صالح وأن SQL Server يملك صلاحية القراءة على مجلد النسخ الاحتياطية.";
                return RedirectToAction(nameof(RestoreDatabase));
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(uploadedBackupPath))
                {
                    TryDeleteLocalFile(uploadedBackupPath);
                }
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BackupDatabaseDownload()
        {
            try
            {
                if (!await IsCurrentUserPasswordResetAllowedAsync())
                {
                    return Forbid();
                }

                var connectionString = _context.Database.GetConnectionString();
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    TempData["ErrorMessage"] = "تعذر العثور على إعدادات قاعدة البيانات.";
                    return RedirectToAction(nameof(BackupDatabase));
                }

                var connectionBuilder = new SqlConnectionStringBuilder(connectionString);
                var databaseName = connectionBuilder.InitialCatalog;
                if (string.IsNullOrWhiteSpace(databaseName))
                {
                    TempData["ErrorMessage"] = "اسم قاعدة البيانات غير متوفر في إعدادات الاتصال.";
                    return RedirectToAction(nameof(BackupDatabase));
                }

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupFileName = $"{databaseName}_{timestamp}.bak";

                var masterConnectionBuilder = new SqlConnectionStringBuilder(connectionString)
                {
                    InitialCatalog = "master"
                };

                await using (var connection = new SqlConnection(masterConnectionBuilder.ConnectionString))
                {
                    await connection.OpenAsync();

                    var backupDirectory = await GetSqlBackupDirectoryAsync(connection);
                    if (string.IsNullOrWhiteSpace(backupDirectory))
                    {
                        throw new InvalidOperationException("تعذر العثور على مجلد النسخ الاحتياطية الخاص بـ SQL Server.");
                    }

                    var backupPath = Path.Combine(backupDirectory, backupFileName);
                    if (string.IsNullOrWhiteSpace(backupPath))
                    {
                        throw new InvalidOperationException("تعذر تكوين مسار ملف النسخة الاحتياطية.");
                    }

                    await using (var backupCommand = connection.CreateCommand())
                    {
                        backupCommand.CommandType = CommandType.Text;
                        backupCommand.CommandTimeout = 0;
                        backupCommand.CommandText = $@"
BACKUP DATABASE [{databaseName}]
TO DISK = @backupPath
WITH INIT, COPY_ONLY, STATS = 10;";
                        backupCommand.Parameters.AddWithValue("@backupPath", backupPath);
                        await backupCommand.ExecuteNonQueryAsync();
                    }

                    byte[] fileBytes;
                    await using (var readCommand = connection.CreateCommand())
                    {
                        readCommand.CommandType = CommandType.Text;
                        readCommand.CommandTimeout = 0;
                        readCommand.CommandText = @"
DECLARE @sql nvarchar(max);
SET @sql = N'SELECT BulkColumn FROM OPENROWSET(BULK ' +
    QUOTENAME(@backupPath, '''') +
    N', SINGLE_BLOB) AS BackupFile;';
EXEC sp_executesql @sql;";
                        readCommand.Parameters.AddWithValue("@backupPath", backupPath);

                        var result = await readCommand.ExecuteScalarAsync();
                        if (result is not byte[] bytes || bytes.Length == 0)
                        {
                            throw new InvalidOperationException("تم إنشاء النسخة الاحتياطية لكن تعذر تحميل ملف النسخة من SQL Server.");
                        }

                        fileBytes = bytes;
                    }

                    await TryDeleteSqlBackupFileAsync(connection, backupPath);

                    _logger.LogInformation(
                        "Database backup created successfully by SuperAdmin. Database: {DatabaseName}, Path: {BackupPath}",
                        databaseName,
                        backupPath);

                    Response.Headers["Content-Disposition"] = $"attachment; filename=\"{backupFileName}\"";
                    return File(fileBytes, "application/octet-stream", backupFileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database backup failed.");
                TempData["ErrorMessage"] =
                    $"فشل إنشاء النسخة الاحتياطية. {ex.Message} إذا استمر الخطأ فامنح حساب خدمة SQL Server صلاحية الكتابة على مجلد النسخ الاحتياطية.";
                return RedirectToAction(nameof(BackupDatabase));
            }

        }

        private static async Task<string> GetSqlBackupDirectoryAsync(SqlConnection connection)
        {
            await using var command = connection.CreateCommand();
            command.CommandType = CommandType.Text;
            command.CommandText = "SELECT CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultBackupPath'));";

            var result = await command.ExecuteScalarAsync();
            var backupDirectory = result?.ToString()?.Trim();

            return string.IsNullOrWhiteSpace(backupDirectory)
                ? SqlBackupDirectoryFallback
                : backupDirectory;
        }

        private async Task TryDeleteSqlBackupFileAsync(SqlConnection connection, string backupPath)
        {
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandType = CommandType.Text;
                command.CommandTimeout = 0;
                command.CommandText = "EXEC master.dbo.xp_delete_file 0, @backupPath;";
                command.Parameters.AddWithValue("@backupPath", backupPath);
                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete SQL backup file at path {BackupPath}", backupPath);
            }
        }

        private async Task RestoreDatabaseFromBackupAsync(SqlConnection connection, string databaseName, string backupPath)
        {
            var currentFiles = await GetCurrentDatabaseFilesAsync(connection, databaseName);
            if (currentFiles.Count == 0)
            {
                throw new InvalidOperationException("تعذر قراءة ملفات قاعدة البيانات الحالية من SQL Server.");
            }

            var backupFiles = await GetBackupLogicalFilesAsync(connection, backupPath);
            if (backupFiles.Count == 0)
            {
                throw new InvalidOperationException("تعذر قراءة الملفات المنطقية من ملف النسخة الاحتياطية.");
            }

            var moveClauses = BuildRestoreMoveClauses(databaseName, currentFiles, backupFiles);
            var quotedDatabaseName = QuoteSqlIdentifier(databaseName);

            await ExecuteNonQueryAsync(connection, $@"
ALTER DATABASE {quotedDatabaseName}
SET SINGLE_USER WITH ROLLBACK IMMEDIATE;");

            try
            {
                var restoreSql = $@"
RESTORE DATABASE {quotedDatabaseName}
FROM DISK = @backupPath
WITH REPLACE, RECOVERY, STATS = 10,
{string.Join("," + Environment.NewLine, moveClauses)};";

                await using var restoreCommand = connection.CreateCommand();
                restoreCommand.CommandType = CommandType.Text;
                restoreCommand.CommandTimeout = 0;
                restoreCommand.CommandText = restoreSql;
                restoreCommand.Parameters.AddWithValue("@backupPath", backupPath);
                await restoreCommand.ExecuteNonQueryAsync();
            }
            finally
            {
                try
                {
                    await ExecuteNonQueryAsync(connection, $@"
ALTER DATABASE {quotedDatabaseName}
SET MULTI_USER;");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not return database {DatabaseName} to MULTI_USER immediately after restore attempt.", databaseName);
                }
            }
        }

        private static async Task ExecuteNonQueryAsync(SqlConnection connection, string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 0;
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }

        private static async Task<List<SqlDatabaseFileInfo>> GetCurrentDatabaseFilesAsync(SqlConnection connection, string databaseName)
        {
            var files = new List<SqlDatabaseFileInfo>();

            await using var command = connection.CreateCommand();
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 0;
            command.CommandText = @"
SELECT name, physical_name, type_desc, file_id
FROM sys.master_files
WHERE database_id = DB_ID(@databaseName)
ORDER BY file_id;";
            command.Parameters.AddWithValue("@databaseName", databaseName);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                files.Add(new SqlDatabaseFileInfo(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetInt32(3)));
            }

            return files;
        }

        private static async Task<List<SqlBackupFileInfo>> GetBackupLogicalFilesAsync(SqlConnection connection, string backupPath)
        {
            var files = new List<SqlBackupFileInfo>();

            await using var command = connection.CreateCommand();
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 0;
            command.CommandText = "RESTORE FILELISTONLY FROM DISK = @backupPath;";
            command.Parameters.AddWithValue("@backupPath", backupPath);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var logicalName = reader["LogicalName"]?.ToString() ?? string.Empty;
                var physicalName = reader["PhysicalName"]?.ToString() ?? string.Empty;
                var type = reader["Type"]?.ToString() ?? string.Empty;
                var fileGroupName = reader["FileGroupName"]?.ToString();

                if (!string.IsNullOrWhiteSpace(logicalName) && !string.IsNullOrWhiteSpace(type))
                {
                    files.Add(new SqlBackupFileInfo(logicalName, physicalName, type, fileGroupName));
                }
            }

            return files;
        }

        private static List<string> BuildRestoreMoveClauses(
            string databaseName,
            IReadOnlyList<SqlDatabaseFileInfo> currentFiles,
            IReadOnlyList<SqlBackupFileInfo> backupFiles)
        {
            var moveClauses = new List<string>();
            var currentDataFiles = currentFiles
                .Where(file => file.TypeDesc.Contains("ROWS", StringComparison.OrdinalIgnoreCase))
                .OrderBy(file => file.FileId)
                .ToList();
            var currentLogFiles = currentFiles
                .Where(file => file.TypeDesc.Contains("LOG", StringComparison.OrdinalIgnoreCase))
                .OrderBy(file => file.FileId)
                .ToList();

            var backupDataFiles = backupFiles
                .Where(file => string.Equals(file.Type, "D", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var backupLogFiles = backupFiles
                .Where(file => string.Equals(file.Type, "L", StringComparison.OrdinalIgnoreCase))
                .ToList();

            moveClauses.AddRange(BuildMoveClausesForType(databaseName, currentDataFiles, backupDataFiles, isLog: false));
            moveClauses.AddRange(BuildMoveClausesForType(databaseName, currentLogFiles, backupLogFiles, isLog: true));

            if (moveClauses.Count == 0)
            {
                throw new InvalidOperationException("تعذر بناء مسارات الاستعادة لملفات قاعدة البيانات.");
            }

            return moveClauses;
        }

        private static IEnumerable<string> BuildMoveClausesForType(
            string databaseName,
            IReadOnlyList<SqlDatabaseFileInfo> currentFiles,
            IReadOnlyList<SqlBackupFileInfo> backupFiles,
            bool isLog)
        {
            var clauses = new List<string>();
            var fallbackDirectory = Path.GetDirectoryName(currentFiles.FirstOrDefault()?.PhysicalName ?? string.Empty) ?? string.Empty;
            var fallbackExtension = isLog ? ".ldf" : ".mdf";

            for (var i = 0; i < backupFiles.Count; i++)
            {
                var backupFile = backupFiles[i];
                var targetPhysicalPath = i < currentFiles.Count
                    ? currentFiles[i].PhysicalName
                    : BuildFallbackPhysicalPath(databaseName, fallbackDirectory, backupFile, i, fallbackExtension);

                clauses.Add($"MOVE {QuoteSqlString(backupFile.LogicalName)} TO {QuoteSqlString(targetPhysicalPath)}");
            }

            return clauses;
        }

        private static string BuildFallbackPhysicalPath(
            string databaseName,
            string directory,
            SqlBackupFileInfo backupFile,
            int index,
            string fallbackExtension)
        {
            var sourceExtension = Path.GetExtension(backupFile.PhysicalName);
            var extension = string.IsNullOrWhiteSpace(sourceExtension) ? fallbackExtension : sourceExtension;
            var suffix = index == 0 ? string.Empty : $"_{index}";
            var fileName = $"{databaseName}{suffix}{extension}";
            return Path.Combine(directory, fileName);
        }

        private static string QuoteSqlIdentifier(string identifier)
        {
            return $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";
        }

        private static string QuoteSqlString(string value)
        {
            return $"N'{value.Replace("'", "''", StringComparison.Ordinal)}'";
        }

        private void TryDeleteLocalFile(string path)
        {
            try
            {
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete local temporary restore file at path {BackupPath}", path);
            }
        }

        // ========== عرض جميع المستخدمين مع المسؤوليات الإدارية ==========
        public async Task<IActionResult> Users(
            bool administrativeOnly = false,
            string viewName = "Users",
            int page = 1,
            string? search = null,
            string? role = null,
            string? residenceGovernorate = null,
            string? workGovernorate = null,
            string? gender = null,
            string? status = null,
            string? managerLevel = null,
            string? education = null,
            string? profileStage = null,
            int pageSize = 10)
        {
            pageSize = NormalizeUserManagementPageSize(pageSize);
            page = Math.Max(1, page);

            return await UsersPagedFromDatabaseAsync(administrativeOnly, viewName, page, search, role, residenceGovernorate, workGovernorate, gender, status, managerLevel, education, profileStage, pageSize);

        }

        public async Task<IActionResult> OnlineUsers(
            int page = 1,
            string? search = null,
            string? role = null,
            string? residenceGovernorate = null,
            string? workGovernorate = null,
            string? gender = null,
            string? status = null,
            string? managerLevel = null,
            string? education = null,
            string? profileStage = null,
            int pageSize = 10)
        {
            pageSize = NormalizeUserManagementPageSize(pageSize);
            page = Math.Max(1, page);

            return await UsersPagedFromDatabaseAsync(
                administrativeOnly: false,
                viewName: "Users",
                page: page,
                search: search,
                role: role,
                residenceGovernorate: residenceGovernorate,
                workGovernorate: workGovernorate,
                gender: gender,
                status: status,
                managerLevel: managerLevel,
                education: education,
                profileStage: profileStage,
                pageSize: pageSize,
                onlineOnly: true,
                pageTitleOverride: "المستخدمون المتصلون",
                listActionOverride: "OnlineUsers");
        }

        private async Task<IActionResult> UsersPagedFromDatabaseAsync(
            bool administrativeOnly,
            string viewName,
            int page,
            string? search,
            string? role,
            string? residenceGovernorate,
            string? workGovernorate,
            string? gender,
            string? status,
            string? managerLevel,
            string? education,
            string? profileStage,
            int pageSize,
            bool onlineOnly = false,
            string? pageTitleOverride = null,
            string? listActionOverride = null)
        {
            var canManageSystemManager = CanCurrentUserManageSystemManager();
            var roleMembershipQuery = _context.UserRoles
                .AsNoTracking()
                .Join(
                    _context.Roles.AsNoTracking(),
                    userRole => userRole.RoleId,
                    roleRow => roleRow.Id,
                    (userRole, roleRow) => new
                    {
                        userRole.UserId,
                        RoleName = roleRow.Name ?? string.Empty
                    });

            var systemManagerUserIdsQuery = roleMembershipQuery
                .Where(x => x.RoleName == clsRoles.SystemManager)
                .Select(x => x.UserId)
                .Distinct();

            var superAdminUserIdsQuery = roleMembershipQuery
                .Where(x => x.RoleName == clsRoles.SuperAdmin)
                .Select(x => x.UserId)
                .Distinct();

            var adminUserIdsQuery = roleMembershipQuery
                .Where(x => x.RoleName == clsRoles.Admin)
                .Select(x => x.UserId)
                .Distinct();

            var districtAdminUserIdsQuery = roleMembershipQuery
                .Where(x => x.RoleName == clsRoles.DistrictAdmin)
                .Select(x => x.UserId)
                .Distinct();

            var managerRoleUserIdsQuery = roleMembershipQuery
                .Where(x => x.RoleName == clsRoles.Manager || x.RoleName == clsRoles.AssistantManager)
                .Select(x => x.UserId)
                .Distinct();

            var memberRoleUserIdsQuery = roleMembershipQuery
                .Where(x => x.RoleName == "فرد")
                .Select(x => x.UserId)
                .Distinct();

            var assignedManagerUserIdsQuery = _context.ManagementAssignments
                .AsNoTracking()
                .Select(a => a.UserId)
                .Distinct();

            var completeProfileUserIdsQuery = _context.Identifies
                .AsNoTracking()
                .Where(i =>
                    !string.IsNullOrWhiteSpace(i.FullName) &&
                    !string.IsNullOrWhiteSpace(i.MotherName) &&
                    i.Date != DateTime.MinValue &&
                    !string.IsNullOrWhiteSpace(i.Gender) &&
                    !string.IsNullOrWhiteSpace(i.PhoneNumber) &&
                    !string.IsNullOrWhiteSpace(i.IdentityCardN) &&
                    i.IdentityCardN.Length == 12 &&
                    (
                        !string.IsNullOrWhiteSpace(i.WorkGovernorate) ||
                        _context.WorkLocations.Any(w =>
                            w.IdentifyId == i.Id &&
                            !string.IsNullOrWhiteSpace(w.Governorate) &&
                            (w.Governorate != "بغداد" || !string.IsNullOrWhiteSpace(w.District)))
                    ))
                .Select(i => i.UserId)
                .Distinct();

            var query = _context.Users.AsNoTracking().AsQueryable();

            if (onlineOnly)
            {
                var onlineUserIds = _onlineUsersTracker.GetOnlineUserIds();
                query = onlineUserIds.Count == 0
                    ? query.Where(_ => false)
                    : query.Where(u => onlineUserIds.Contains(u.Id));
            }

            if (!canManageSystemManager)
            {
                query = query.Where(u => !systemManagerUserIdsQuery.Contains(u.Id));
            }

            if (administrativeOnly)
            {
                query = query.Where(u =>
                    assignedManagerUserIdsQuery.Contains(u.Id) ||
                    managerRoleUserIdsQuery.Contains(u.Id));
            }

            var unfilteredUsersCount = await query.CountAsync();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                var digitsOnlyTerm = new string(term.Where(char.IsDigit).ToArray());
                var isNumericSearch = !string.IsNullOrWhiteSpace(digitsOnlyTerm) && digitsOnlyTerm.Length >= Math.Max(3, term.Length - 1);
                var includeExpandedRelationshipSearch = !isNumericSearch && term.Length >= 3;

                var identifyMatchUserIdsQuery = _context.Identifies
                    .AsNoTracking()
                    .Where(i =>
                        (i.FullName != null && i.FullName.Contains(term)) ||
                        (i.PhoneNumber != null && i.PhoneNumber.Contains(term)) ||
                        (i.WhatsAppNumber != null && i.WhatsAppNumber.Contains(term)) ||
                        (i.IdentityCardN != null && i.IdentityCardN.Contains(term)) ||
                        (i.Email != null && i.Email.Contains(term)))
                    .Select(i => i.UserId)
                    .Distinct();

                if (includeExpandedRelationshipSearch)
                {
                    var affiliationEntityMatchUserIdsQuery = _context.AffiliationInfos
                        .AsNoTracking()
                        .Join(_context.AffiliationEntities.AsNoTracking().Where(e => e.Name.Contains(term)),
                            a => a.AffiliationEntityId,
                            e => e.Id,
                            (a, _) => a.UserId);
                    var divisionMatchUserIdsQuery = _context.AffiliationInfos
                        .AsNoTracking()
                        .Join(_context.Divisions.AsNoTracking().Where(d => d.Name.Contains(term)),
                            a => a.DivisionId,
                            d => d.Id,
                            (a, _) => a.UserId);
                    var sectionMatchUserIdsQuery = _context.AffiliationInfos
                        .AsNoTracking()
                        .Join(_context.Sections.AsNoTracking().Where(s => s.Name.Contains(term)),
                            a => a.SectionId,
                            s => s.Id,
                            (a, _) => a.UserId);
                    var groupMatchUserIdsQuery = _context.AffiliationInfos
                        .AsNoTracking()
                        .Join(_context.Groups.AsNoTracking().Where(g => g.Name.Contains(term)),
                            a => a.GroupId,
                            g => g.Id,
                            (a, _) => a.UserId);
                    var affiliationMatchUserIdsQuery = _context.AffiliationInfos
                        .AsNoTracking()
                        .Where(a => a.BadgeNumber != null && a.BadgeNumber.Contains(term))
                        .Select(a => a.UserId)
                        .Union(affiliationEntityMatchUserIdsQuery)
                        .Union(divisionMatchUserIdsQuery)
                        .Union(sectionMatchUserIdsQuery)
                        .Union(groupMatchUserIdsQuery)
                        .Distinct();

                    var managementEntityMatchUserIdsQuery = _context.ManagementAssignments
                        .AsNoTracking()
                        .Join(_context.AffiliationEntities.AsNoTracking().Where(e => e.Name.Contains(term)),
                            a => a.AffiliationEntityId,
                            e => e.Id,
                            (a, _) => a.UserId);
                    var managementDivisionMatchUserIdsQuery = _context.ManagementAssignments
                        .AsNoTracking()
                        .Join(_context.Divisions.AsNoTracking().Where(d => d.Name.Contains(term)),
                            a => a.DivisionId,
                            d => d.Id,
                            (a, _) => a.UserId);
                    var managementSectionMatchUserIdsQuery = _context.ManagementAssignments
                        .AsNoTracking()
                        .Join(_context.Sections.AsNoTracking().Where(s => s.Name.Contains(term)),
                            a => a.SectionId,
                            s => s.Id,
                            (a, _) => a.UserId);
                    var managementGroupMatchUserIdsQuery = _context.ManagementAssignments
                        .AsNoTracking()
                        .Join(_context.Groups.AsNoTracking().Where(g => g.Name.Contains(term)),
                            a => a.GroupId,
                            g => g.Id,
                            (a, _) => a.UserId);
                    var managementMatchUserIdsQuery = managementEntityMatchUserIdsQuery
                        .Union(managementDivisionMatchUserIdsQuery)
                        .Union(managementSectionMatchUserIdsQuery)
                        .Union(managementGroupMatchUserIdsQuery)
                        .Distinct();

                    query = query.Where(u =>
                        (u.Email != null && u.Email.Contains(term)) ||
                        (u.PhoneNumber != null && u.PhoneNumber.Contains(term)) ||
                        identifyMatchUserIdsQuery.Contains(u.Id) ||
                        affiliationMatchUserIdsQuery.Contains(u.Id) ||
                        managementMatchUserIdsQuery.Contains(u.Id));
                }
                else
                {
                    query = query.Where(u =>
                        (u.Email != null && u.Email.Contains(term)) ||
                        (u.PhoneNumber != null && u.PhoneNumber.Contains(term)) ||
                        identifyMatchUserIdsQuery.Contains(u.Id));
                }
            }

            if (!string.IsNullOrWhiteSpace(role))
            {
                var normalizedRole = role.Trim();
                query = normalizedRole.Equals("member", StringComparison.OrdinalIgnoreCase)
                    ? query.Where(u =>
                        _context.Identifies.Any(i => i.UserId == u.Id && i.AccountType == "فرد") ||
                        memberRoleUserIdsQuery.Contains(u.Id))
                    : query.Where(u => roleMembershipQuery
                        .Where(r => r.RoleName == normalizedRole)
                        .Select(r => r.UserId)
                        .Contains(u.Id));
            }

            if (!string.IsNullOrWhiteSpace(residenceGovernorate))
                query = query.Where(u => _context.Addresses.Any(a => a.UserId == u.Id && a.Governorate == residenceGovernorate));

            if (!string.IsNullOrWhiteSpace(workGovernorate))
            {
                var workGovernorateUserIdsQuery = _context.Identifies
                    .AsNoTracking()
                    .Where(i => i.ManagedGovernorate == workGovernorate || i.WorkGovernorate == workGovernorate)
                    .Select(i => i.UserId)
                    .Union(
                        _context.Identifies
                            .AsNoTracking()
                            .Join(_context.WorkLocations.AsNoTracking().Where(w => w.Governorate == workGovernorate),
                                i => i.Id,
                                w => w.IdentifyId,
                                (i, _) => i.UserId))
                    .Distinct();

                query = query.Where(u => workGovernorateUserIdsQuery.Contains(u.Id));
            }

            if (!string.IsNullOrWhiteSpace(gender))
                query = query.Where(u => _context.Identifies.Any(i => i.UserId == u.Id && i.Gender == gender));

            if (status == "active")
                query = query.Where(u => u.EmailConfirmed);
            else if (status == "inactive")
                query = query.Where(u => !u.EmailConfirmed);

            if (!string.IsNullOrWhiteSpace(managerLevel))
            {
                var parts = managerLevel.Split('-', 2);
                if (parts.Length == 2)
                {
                    var assignmentRole = parts[0];
                    var level = parts[1];
                    query = query.Where(u => _context.ManagementAssignments.Any(a =>
                        a.UserId == u.Id &&
                        a.AssignmentRole == assignmentRole &&
                        a.ManagementLevel == level));
                }
            }

            if (!string.IsNullOrWhiteSpace(education))
                query = query.Where(u => _context.Identifies.Any(i => i.UserId == u.Id && i.Education == education));

            if (!string.IsNullOrWhiteSpace(profileStage))
            {
                var normalizedProfileStage = profileStage.Trim().ToLowerInvariant();
                query = normalizedProfileStage switch
                {
                    "incomplete" => query.Where(u => !completeProfileUserIdsQuery.Contains(u.Id)),
                    "basic-pending" => query.Where(u => completeProfileUserIdsQuery.Contains(u.Id) && _context.Identifies.Any(i =>
                        i.UserId == u.Id &&
                        !i.IsBasicInfoApproved)),
                    "needs-additional" => query.Where(u => _context.Identifies.Any(i =>
                        i.UserId == u.Id &&
                        i.IsBasicInfoApproved &&
                        !i.RequestedPromotion &&
                        !i.IsPromoted &&
                        string.IsNullOrEmpty(i.RejectionReason) &&
                        i.AccountType != "فرد") &&
                        !memberRoleUserIdsQuery.Contains(u.Id)),
                    "promotion-pending" => query.Where(u => _context.Identifies.Any(i =>
                        i.UserId == u.Id &&
                        i.RequestedPromotion &&
                        !i.IsPromoted &&
                        i.AccountType != "فرد") &&
                        !memberRoleUserIdsQuery.Contains(u.Id)),
                    "promoted" => query.Where(u =>
                        _context.Identifies.Any(i => i.UserId == u.Id && (i.IsPromoted || i.AccountType == "فرد")) ||
                        memberRoleUserIdsQuery.Contains(u.Id)),
                    _ => query
                };
            }

            var totalUsersCount = await query.CountAsync();
            var totalPages = Math.Max(
                1,
                (int)Math.Ceiling(totalUsersCount / (double)pageSize));

            page = Math.Min(page, totalPages);

            // جلب المسؤولين مرة واحدة بدل تنفيذ Join متكرر داخل ORDER BY
            var priorityRoleNames = new[]
            {
                clsRoles.SystemManager,
                clsRoles.SuperAdmin,
                clsRoles.Admin,
                clsRoles.DistrictAdmin,
                clsRoles.Manager,
                clsRoles.AssistantManager
            };

            var priorityRoleMemberships = await roleMembershipQuery
                .Where(x => priorityRoleNames.Contains(x.RoleName))
                .Select(x => new
                {
                    x.UserId,
                    x.RoleName
                })
                .ToListAsync();

            var systemManagerUserIds = priorityRoleMemberships
                .Where(x => x.RoleName == clsRoles.SystemManager)
                .Select(x => x.UserId)
                .Distinct()
                .ToList();

            var superAdminUserIds = priorityRoleMemberships
                .Where(x => x.RoleName == clsRoles.SuperAdmin)
                .Select(x => x.UserId)
                .Distinct()
                .ToList();

            var adminUserIds = priorityRoleMemberships
                .Where(x => x.RoleName == clsRoles.Admin)
                .Select(x => x.UserId)
                .Distinct()
                .ToList();

            var districtAdminUserIds = priorityRoleMemberships
                .Where(x => x.RoleName == clsRoles.DistrictAdmin)
                .Select(x => x.UserId)
                .Distinct()
                .ToList();

            var managerRoleUserIds = priorityRoleMemberships
                .Where(x =>
                    x.RoleName == clsRoles.Manager ||
                    x.RoleName == clsRoles.AssistantManager)
                .Select(x => x.UserId)
                .Distinct()
                .ToList();

            var assignedManagerUserIds = await _context.ManagementAssignments
                .AsNoTracking()
                .Select(x => x.UserId)
                .Distinct()
                .ToListAsync();

            var users = await query
                .OrderByDescending(u =>
                    canManageSystemManager &&
                    systemManagerUserIds.Contains(u.Id))
                .ThenByDescending(u =>
                    superAdminUserIds.Contains(u.Id))
                .ThenByDescending(u =>
                    adminUserIds.Contains(u.Id))
                .ThenByDescending(u =>
                    districtAdminUserIds.Contains(u.Id))
                .ThenByDescending(u =>
                    assignedManagerUserIds.Contains(u.Id) ||
                    managerRoleUserIds.Contains(u.Id))
                .ThenBy(u => u.Email)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.EmailConfirmed
                })
                .ToListAsync();

            var userIds = users.Select(u => u.Id).ToHashSet();
            var profilesByUserId = await _context.Identifies.AsNoTracking().Where(i => userIds.Contains(i.UserId)).ToDictionaryAsync(i => i.UserId);
            var profileIds = profilesByUserId.Values.Select(p => p.Id).ToHashSet();
            var addressesByUserId = await _context.Addresses.AsNoTracking().Where(a => userIds.Contains(a.UserId)).GroupBy(a => a.UserId).ToDictionaryAsync(g => g.Key, g => g.First());
            var workLocationsByProfileId = await _context.WorkLocations.AsNoTracking().Where(w => profileIds.Contains(w.IdentifyId)).GroupBy(w => w.IdentifyId).ToDictionaryAsync(g => g.Key, g => g.First());
            var assignmentsByUserId = await _context.ManagementAssignments.AsNoTracking().Where(x => userIds.Contains(x.UserId)).GroupBy(x => x.UserId).ToDictionaryAsync(g => g.Key, g => g.ToList());
            var affiliationInfosByUserId = await _context.AffiliationInfos.AsNoTracking().Where(x => userIds.Contains(x.UserId)).GroupBy(x => x.UserId).ToDictionaryAsync(g => g.Key, g => g.First());
            var rolesByUserId = await _context.UserRoles
                .Where(ur => userIds.Contains(ur.UserId))
                .Join(_context.Roles, userRole => userRole.RoleId, roleRow => roleRow.Id,
                    (userRole, roleRow) => new { userRole.UserId, RoleName = roleRow.Name ?? string.Empty })
                .GroupBy(x => x.UserId)
                .ToDictionaryAsync(g => g.Key, g => (IList<string>)g.Select(x => x.RoleName).ToList());

            var assignmentEntityIds = assignmentsByUserId.Values
                .SelectMany(x => x)
                .Where(x => x.AffiliationEntityId.HasValue)
                .Select(x => x.AffiliationEntityId!.Value)
                .Distinct()
                .ToList();
            var assignmentDivisionIds = assignmentsByUserId.Values
                .SelectMany(x => x)
                .Where(x => x.DivisionId.HasValue)
                .Select(x => x.DivisionId!.Value)
                .Distinct()
                .ToList();
            var assignmentSectionIds = assignmentsByUserId.Values
                .SelectMany(x => x)
                .Where(x => x.SectionId.HasValue)
                .Select(x => x.SectionId!.Value)
                .Distinct()
                .ToList();
            var assignmentGroupIds = assignmentsByUserId.Values
                .SelectMany(x => x)
                .Where(x => x.GroupId.HasValue)
                .Select(x => x.GroupId!.Value)
                .Distinct()
                .ToList();

            var affiliationEntityNames = await _context.AffiliationEntities.AsNoTracking()
                .Where(e => assignmentEntityIds.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, e => e.Name);
            var divisionNames = await _context.Divisions.AsNoTracking()
                .Where(d => assignmentDivisionIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, d => d.Name);
            var sectionNames = await _context.Sections.AsNoTracking()
                .Where(s => assignmentSectionIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.Name);
            var groupNames = await _context.Groups.AsNoTracking()
                .Where(g => assignmentGroupIds.Contains(g.Id))
                .ToDictionaryAsync(g => g.Id, g => g.Name);

            string GetEffectiveGovernorateFast(Identify? profile)
            {
                var workLocation = profile != null && workLocationsByProfileId.TryGetValue(profile.Id, out var wl) ? wl : profile?.WorkLocation;
                return !string.IsNullOrWhiteSpace(workLocation?.Governorate) ? workLocation.Governorate :
                    !string.IsNullOrWhiteSpace(profile?.WorkGovernorate) ? profile.WorkGovernorate : string.Empty;
            }

            string GetEffectiveDistrictFast(Identify? profile)
            {
                var workLocation = profile != null && workLocationsByProfileId.TryGetValue(profile.Id, out var wl) ? wl : profile?.WorkLocation;
                if (!string.IsNullOrWhiteSpace(workLocation?.Governorate) && workLocation.Governorate == "بغداد") return workLocation.District ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(profile?.WorkGovernorate) && profile.WorkGovernorate == "بغداد") return profile.WorkDistrict ?? string.Empty;
                return string.Empty;
            }

            string GetManagedEntityDisplayName(ManagementAssignment assignment)
            {
                return assignment.ManagementLevel switch
                {
                    "Entity" when assignment.AffiliationEntityId.HasValue => affiliationEntityNames.TryGetValue(assignment.AffiliationEntityId.Value, out var name) ? name : string.Empty,
                    "Division" when assignment.DivisionId.HasValue => divisionNames.TryGetValue(assignment.DivisionId.Value, out var name) ? name : string.Empty,
                    "Section" when assignment.SectionId.HasValue => sectionNames.TryGetValue(assignment.SectionId.Value, out var name) ? name : string.Empty,
                    "Group" when assignment.GroupId.HasValue => groupNames.TryGetValue(assignment.GroupId.Value, out var name) ? name : string.Empty,
                    _ => string.Empty
                };
            }

            var list = new List<SuperAdminUserVM>();
            foreach (var user in users)
            {
                var roles = rolesByUserId.TryGetValue(user.Id, out var userRoles) ? userRoles : new List<string>();
                profilesByUserId.TryGetValue(user.Id, out var userProfile);
                addressesByUserId.TryGetValue(user.Id, out var userAddress);
                affiliationInfosByUserId.TryGetValue(user.Id, out var affiliationInfo);
                var managementAssignments = assignmentsByUserId.TryGetValue(user.Id, out var userAssignments) ? userAssignments : new List<ManagementAssignment>();
                var primaryAssignment = managementAssignments.FirstOrDefault();
                var roleDisplay = string.Join(", ", roles);
                if (userProfile?.AccountType == "فرد" && !roles.Contains("فرد"))
                    roleDisplay = string.IsNullOrEmpty(roleDisplay) ? "فرد" : roleDisplay + ", فرد";

                var governorateDisplay = roles.Contains(clsRoles.SystemManager)
                    ? "🛡️ مدير النظام - يدير الكل"
                    : roles.Contains(clsRoles.SuperAdmin)
                    ? "👑 السوبر أدمن - يدير الكل"
                    : roles.Contains(clsRoles.Admin)
                        ? (!string.IsNullOrWhiteSpace(userProfile?.ManagedGovernorate) ? $"🔷 مدير محافظة: {userProfile.ManagedGovernorate}" : "🔷 أدمن (لم تحدد المحافظة)")
                        : GetEffectiveGovernorateFast(userProfile) ?? "غير محدد";

                list.Add(new SuperAdminUserVM
                {
                    Id = user.Id,
                    Email = user.Email ?? "",
                    Roles = roleDisplay,
                    ResidenceGovernorate = userAddress?.Governorate ?? "غير محدد",
                    WorkGovernorate = !string.IsNullOrWhiteSpace(userProfile?.ManagedGovernorate) ? userProfile.ManagedGovernorate : GetEffectiveGovernorateFast(userProfile),
                    WorkDistrict = !string.IsNullOrWhiteSpace(userProfile?.ManagedDistrict) ? userProfile.ManagedDistrict : GetEffectiveDistrictFast(userProfile),
                    Gender = userProfile?.Gender ?? "غير محدد",
                    Governorate = governorateDisplay,
                    ManagedGovernorate = userProfile?.ManagedGovernorate,
                    ManagedDistrict = userProfile?.ManagedDistrict,
                    IsActive = user.EmailConfirmed,
                    FullName = userProfile?.FullName ?? "غير مكتمل",
                    CoverImage = userProfile?.CoverImage,
                    PromotionStatus = userProfile?.RequestedPromotion == true ? "⏳ قيد المراجعة" : userProfile?.RejectionReason != null ? "❌ مرفوض" : "",
                    IsBasicInfoApproved = userProfile?.IsBasicInfoApproved ?? false,
                    RequestedPromotion = userProfile?.RequestedPromotion ?? false,
                    RejectionReason = userProfile?.RejectionReason,
                    HasCompleteProfile = IsProfileComplete(userProfile, userAddress, null, null),
                    CompletionPercentage = CalculateCompletionPercentage(userProfile, userAddress, null),
                    AccountType = userProfile?.AccountType ?? "عادي",
                    ProfileId = userProfile?.Id,
                    IsManager = primaryAssignment != null,
                    ManagementLevel = primaryAssignment?.ManagementLevel ?? "",
                    ManagementLevelArabic = primaryAssignment != null ? GetArabicLevelName(primaryAssignment.ManagementLevel, primaryAssignment.AssignmentRole) : "",
                    AssignmentRole = primaryAssignment?.AssignmentRole ?? "",
                    AdministrativeResponsibilityDisplay = primaryAssignment != null ? GetArabicLevelName(primaryAssignment.ManagementLevel, primaryAssignment.AssignmentRole) : "",
                    ManagedEntityName = primaryAssignment != null ? GetManagedEntityDisplayName(primaryAssignment) : "",
                    SearchText = string.Empty,
                    IsPromoted = userProfile?.IsPromoted ?? false,
                    BadgeNumber = affiliationInfo?.BadgeNumber ?? "",
                    Education = userProfile?.Education ?? "---",
                    StudyStage = userProfile?.StudyStage ?? "---"
                });
            }

            ViewBag.AdministrativeOnly = administrativeOnly;
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalUsers = unfilteredUsersCount;
            ViewBag.FilteredUsers = totalUsersCount;
            ViewBag.Search = search;
            ViewBag.RoleFilter = role;
            ViewBag.ResidenceGovernorateFilter = residenceGovernorate;
            ViewBag.WorkGovernorateFilter = workGovernorate;
            ViewBag.GenderFilter = gender;
            ViewBag.StatusFilter = status;
            ViewBag.ManagerLevelFilter = managerLevel;
            ViewBag.EducationFilter = education;
            ViewBag.ProfileStageFilter = profileStage;
            ViewBag.ActiveUsers = await _context.Users.CountAsync(u => u.EmailConfirmed);
            ViewBag.Admins = await adminUserIdsQuery.CountAsync();
            ViewBag.SuperAdmins = await superAdminUserIdsQuery.CountAsync();
            ViewBag.CanManageSystemManager = canManageSystemManager;
            ViewBag.DistrictAdmins = await districtAdminUserIdsQuery.CountAsync();
            ViewBag.AdministrativeManagers = await _context.ManagementAssignments.AsNoTracking().CountAsync(x => x.AssignmentRole == "Manager");
            ViewBag.AdministrativeAssistants = await _context.ManagementAssignments.AsNoTracking().CountAsync(x => x.AssignmentRole == "Assistant");
            ViewBag.TotalIndividuals = await _context.Identifies.AsNoTracking().CountAsync(i => i.Education != null && i.Education != "" && (i.AccountType == "فرد" || i.IsPromoted));
            ViewBag.CanResetUserPasswords = await IsCurrentUserPasswordResetAllowedAsync();
            ViewBag.PageTitle = pageTitleOverride;
            ViewBag.ListAction = listActionOverride;
            ViewBag.IsOnlineUsersPage = onlineOnly;

            viewName = viewName == "AdministrativeManagers" ? viewName : "Users";
            return View(viewName, list);
        }

        private static int NormalizeUserManagementPageSize(int pageSize)
        {
            int[] allowedPageSizes = [10, 25, 50, 100];
            return allowedPageSizes.Contains(pageSize) ? pageSize : 10;
        }

        public async Task<IActionResult> AdministrativeManagers(
            int page = 1,
            string? search = null,
            string? role = null,
            string? residenceGovernorate = null,
            string? workGovernorate = null,
            string? gender = null,
            string? status = null,
            string? managerLevel = null,
            string? education = null,
            int pageSize = 10)
        {
            return await Users(
                administrativeOnly: true,
                viewName: "AdministrativeManagers",
                page: page,
                search: search,
                role: role,
                residenceGovernorate: residenceGovernorate,
                workGovernorate: workGovernorate,
                gender: gender,
                status: status,
                managerLevel: managerLevel,
                education: education,
                pageSize: pageSize);
        }

        // ========== عرض طلبات الترقية ==========
        [HttpGet]
        public async Task<IActionResult> PromotionRequests(int page = 1)
        {
            const int pageSize = 10;
            var requestsQuery = _context.Identifies
                .AsNoTracking()
                .Where(i => i.RequestedPromotion &&
                            i.AccountType == "مكتمل" &&
                            string.IsNullOrEmpty(i.RejectionReason));

            var governorateOptions = await GetAllWorkGovernorateFilterValuesAsync();

            var totalRequests = await requestsQuery.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalRequests / (double)pageSize));
            page = Math.Max(1, Math.Min(page, totalPages));

            var pagedRequests = await requestsQuery
                .OrderByDescending(i => i.RequestedPromotionDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var viewModel = new List<PromotionRequestViewModel>();
            foreach (var p in pagedRequests)
            {
                var user = await _userManager.FindByIdAsync(p.UserId);
                var userAddress = await GetUserAddressAsync(p.UserId);
                var userVoterCard = await GetUserVoterCardAsync(p.UserId);
                var affiliationInfo = await GetUserAffiliationInfoAsync(p.UserId);

                viewModel.Add(new PromotionRequestViewModel
                {
                    Id = p.Id,
                    UserId = p.UserId,
                    UserEmail = user?.Email ?? "",
                    FullName = p.FullName ?? "",
                    PhoneNumber = p.PhoneNumber ?? "",
                    Governorate = GetEffectiveGovernorate(p, userAddress),
                    District = GetEffectiveDistrict(p, userAddress),
                    IdentityCardN = p.IdentityCardN,
                    AffiliationEntity = affiliationInfo?.AffiliationEntity?.Name,
                    Division = affiliationInfo?.Division?.Name,
                    Section = affiliationInfo?.Section?.Name,
                    Group = affiliationInfo?.Group?.Name,
                    RequestDate = p.RequestedPromotionDate ?? p.CreatedAt,
                    AccountType = p.AccountType ?? "عادي",
                    CoverImage = p.CoverImage,
                    HasCompleteProfile = IsProfileComplete(p, userAddress, userVoterCard, null),
                    CompletionPercentage = CalculateCompletionPercentage(p, userAddress, userVoterCard),
                    RejectionReason = p.RejectionReason
                });
            }

            ViewBag.TotalRequests = totalRequests;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.GovernorateOptions = governorateOptions;

            return View(viewModel);
        }

        // ========== عرض طلبات المراجعة ==========
        [HttpGet]
        public async Task<IActionResult> PendingBasicInfo(int page = 1)
        {
            const int pageSize = 10;
            var requestsQuery = _context.Identifies
                .AsNoTracking()
                .Where(i => i.IsBasicInfoApproved == false &&
                            string.IsNullOrEmpty(i.BasicInfoRejectionReason) &&
                            !string.IsNullOrEmpty(i.FullName) &&
                            !string.IsNullOrEmpty(i.IdentityCardN) && i.IdentityCardN.Length >= 12);

            var governorateOptions = await GetAllWorkGovernorateFilterValuesAsync();

            var totalRequests = await requestsQuery.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalRequests / (double)pageSize));
            page = Math.Max(1, Math.Min(page, totalPages));

            var pagedRequests = await requestsQuery
                .OrderByDescending(i => i.BasicInfoRequestedAt ?? i.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var viewModel = new List<PromotionRequestViewModel>();
            foreach (var p in pagedRequests)
            {
                var user = await _userManager.FindByIdAsync(p.UserId);
                var userAddress = await GetUserAddressAsync(p.UserId);

                viewModel.Add(new PromotionRequestViewModel
                {
                    Id = p.Id,
                    UserId = p.UserId,
                    UserEmail = user?.Email ?? "",
                    FullName = p.FullName ?? "",
                    PhoneNumber = p.PhoneNumber ?? "",
                    Governorate = GetEffectiveGovernorate(p, userAddress),
                    District = GetEffectiveDistrict(p, userAddress),
                    IdentityCardN = p.IdentityCardN,
                    RequestDate = p.BasicInfoRequestedAt ?? p.CreatedAt,
                    AccountType = p.AccountType ?? "عادي",
                    CoverImage = p.CoverImage,
                    HasCompleteProfile = IsProfileComplete(p, userAddress, null, null),
                    CompletionPercentage = CalculateCompletionPercentage(p, userAddress, null),
                    RejectionReason = p.BasicInfoRejectionReason
                });
            }

            ViewBag.TotalRequests = totalRequests;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.GovernorateOptions = governorateOptions;

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> RequestHistory(
            string type = "promotion",
            string? searchName = null,
            string? searchGovernorate = null,
            string? searchPhone = null,
            int page = 1,
            int pageSize = 10)
        {
            var normalizedType = string.Equals(type, "basic", StringComparison.OrdinalIgnoreCase)
                ? "basic"
                : "promotion";

            var query = _context.Identifies.AsNoTracking();
            query = normalizedType == "basic"
                ? query.Where(i => i.IsBasicInfoApproved || !string.IsNullOrEmpty(i.BasicInfoRejectionReason))
                : query.Where(i => i.IsPromoted || !string.IsNullOrEmpty(i.RejectionReason));

            var governorateOptions = await GetRequestHistoryGovernorateOptionsAsync(query);
            pageSize = NormalizeRequestHistoryPageSize(pageSize);
            query = ApplyRequestHistoryQueryFilters(query, searchName, searchGovernorate, searchPhone);

            var totalItems = await query.CountAsync();
            var approvedCount = normalizedType == "basic"
                ? await query.CountAsync(i => i.IsBasicInfoApproved && string.IsNullOrEmpty(i.BasicInfoRejectionReason))
                : await query.CountAsync(i => i.IsPromoted && string.IsNullOrEmpty(i.RejectionReason));
            var rejectedCount = normalizedType == "basic"
                ? await query.CountAsync(i => !string.IsNullOrEmpty(i.BasicInfoRejectionReason))
                : await query.CountAsync(i => !string.IsNullOrEmpty(i.RejectionReason));
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
            page = Math.Max(1, Math.Min(page, totalPages));

            var identifies = await query
                .OrderByDescending(i => normalizedType == "basic"
                    ? (i.BasicInfoRequestedAt ?? i.CreatedAt)
                    : (i.RequestedPromotionDate ?? i.CreatedAt))
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = new List<RequestHistoryItemViewModel>();

            foreach (var identify in identifies)
            {
                var user = await _userManager.FindByIdAsync(identify.UserId);
                var address = await GetUserAddressAsync(identify.UserId);
                var isRejected = normalizedType == "basic"
                    ? !string.IsNullOrWhiteSpace(identify.BasicInfoRejectionReason)
                    : !string.IsNullOrWhiteSpace(identify.RejectionReason);

                items.Add(new RequestHistoryItemViewModel
                {
                    Id = identify.Id,
                    UserId = identify.UserId,
                    FullName = identify.FullName ?? "",
                    UserEmail = user?.Email ?? "",
                    PhoneNumber = identify.PhoneNumber ?? "",
                    Governorate = GetEffectiveGovernorate(identify, address),
                    District = GetEffectiveDistrict(identify, address),
                    RequestDate = normalizedType == "basic"
                        ? identify.BasicInfoRequestedAt ?? identify.CreatedAt
                        : identify.RequestedPromotionDate ?? identify.CreatedAt,
                    ProcessedAt = normalizedType == "basic"
                        ? identify.BasicInfoApprovalDate
                        : identify.PromotionDate,
                    ProcessedBy = normalizedType == "basic"
                        ? await ResolveActorDisplayNameAsync(identify.BasicInfoApprovedBy)
                        : await ResolveActorDisplayNameAsync(identify.PromotedBy),
                    Status = isRejected ? "مرفوض" : "تمت الموافقة",
                    StatusClass = isRejected ? "danger" : "success",
                    Reason = normalizedType == "basic"
                        ? identify.BasicInfoRejectionReason
                        : identify.RejectionReason,
                    CoverImage = identify.CoverImage
                });
            }

            var model = new RequestHistoryViewModel
            {
                RequestType = normalizedType,
                Title = normalizedType == "basic" ? "سجل طلبات المراجعة" : "سجل طلبات الترقية",
                Subtitle = "الطلبات المعالجة مرتبة من الأحدث إلى الأقدم",
                BackAction = normalizedType == "basic" ? nameof(PendingBasicInfo) : nameof(PromotionRequests),
                BackController = "SuperAdmin",
                DetailsAction = nameof(UserDetails),
                DetailsController = "SuperAdmin",
                SearchName = searchName,
                SearchGovernorate = searchGovernorate,
                SearchPhone = searchPhone,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                TotalPages = totalPages,
                GovernorateOptions = governorateOptions,
                Items = items
            };

            ViewBag.ApprovedCount = approvedCount;
            ViewBag.RejectedCount = rejectedCount;
            return View("~/Views/Admin/RequestHistory.cshtml", model);
        }

        private async Task<List<string>> GetRequestHistoryGovernorateOptionsAsync(IQueryable<Identify> query)
        {
            var profiles = await query
                .Select(i => new { i.Id, i.UserId, i.WorkGovernorate })
                .ToListAsync();

            var profileIds = profiles.Select(i => i.Id).ToList();

            var workLocations = await _context.WorkLocations
                .AsNoTracking()
                .Where(w => profileIds.Contains(w.IdentifyId) && !string.IsNullOrEmpty(w.Governorate))
                .Select(w => new { w.IdentifyId, w.Governorate })
                .ToListAsync();

            return profiles
                .Select(profile =>
                    workLocations.FirstOrDefault(w => w.IdentifyId == profile.Id)?.Governorate
                    ?? profile.WorkGovernorate)
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Select(g => g!)
                .Distinct()
                .OrderBy(g => g)
                .ToList();
        }

        private async Task<List<string>> GetAllWorkGovernorateFilterValuesAsync()
        {
            var profiles = await _context.Identifies
                .AsNoTracking()
                .Select(i => new { i.Id, i.WorkGovernorate })
                .ToListAsync();

            var profileIds = profiles.Select(i => i.Id).ToList();

            var workLocations = await _context.WorkLocations
                .AsNoTracking()
                .Where(w => profileIds.Contains(w.IdentifyId) && !string.IsNullOrEmpty(w.Governorate))
                .Select(w => new { w.IdentifyId, w.Governorate })
                .ToListAsync();

            return profiles
                .Select(profile =>
                    workLocations.FirstOrDefault(w => w.IdentifyId == profile.Id)?.Governorate
                    ?? profile.WorkGovernorate)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value)
                .ToList();
        }

        private static int NormalizeRequestHistoryPageSize(int pageSize)
        {
            int[] allowedPageSizes = [10, 25, 50, 100];
            return allowedPageSizes.Contains(pageSize) ? pageSize : 10;
        }

        private IQueryable<Identify> ApplyRequestHistoryQueryFilters(
            IQueryable<Identify> query,
            string? searchName,
            string? searchGovernorate,
            string? searchPhone)
        {
            if (!string.IsNullOrWhiteSpace(searchName))
            {
                var name = searchName.Trim();
                query = query.Where(i => i.FullName.Contains(name));
            }

            if (!string.IsNullOrWhiteSpace(searchGovernorate))
            {
                var governorate = searchGovernorate.Trim();
                query = query.Where(i =>
                    i.WorkGovernorate == governorate ||
                    _context.WorkLocations.Any(w => w.IdentifyId == i.Id && w.Governorate == governorate));
            }

            if (!string.IsNullOrWhiteSpace(searchPhone))
            {
                var phone = searchPhone.Trim();
                query = query.Where(i => i.PhoneNumber.Contains(phone));
            }

            return query;
        }

        // ========== الموافقة على طلب الترقية ==========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApprovePromotion(int id)
        {
            try
            {
                var identify = await _context.Identifies.FindAsync(id);
                if (identify == null)
                    return Json(new { success = false, message = "المستخدم غير موجود" });

                identify.AccountType = "فرد";
                identify.IsPromoted = true;
                identify.PromotionDate = DateTime.Now;
                identify.PromotedBy = await GetCurrentActorDisplayNameAsync("System");
                identify.RequestedPromotion = false;
                identify.RejectionReason = null;

                _context.Identifies.Update(identify);

                var user = await _userManager.FindByIdAsync(identify.UserId);
                if (user != null)
                {
                    if (await _userManager.IsInRoleAsync(user, clsRoles.User))
                        await _userManager.RemoveFromRoleAsync(user, clsRoles.User);
                    if (!await _userManager.IsInRoleAsync(user, clsRoles.Member))
                        await _userManager.AddToRoleAsync(user, clsRoles.Member);
                }

                await _context.SaveChangesAsync();

                try
                {
                    await _notificationService.CreateNotificationFromTemplate(
                        NotificationTemplateKeys.PromotionApproved,
                        identify.UserId,
                        icon: "bi-star-fill",
                        clickUrl: "/Register/ProfileDetails"
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خطأ في إرسال إشعار الموافقة");
                }

                return Json(new { success = true, message = "✅ تمت الموافقة على الترقية بنجاح" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"حدث خطأ: {ex.Message}" });
            }
        }

        // ========== رفض طلب الترقية ==========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectPromotion(int id, string reason)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(reason))
                    return Json(new { success = false, message = "الرجاء كتابة سبب الرفض" });

                var identify = await _context.Identifies.FindAsync(id);
                if (identify == null)
                    return Json(new { success = false, message = "المستخدم غير موجود" });

                identify.RequestedPromotion = false;
                identify.RejectionReason = reason;
                identify.PromotionDate = null;
                identify.PromotedBy = await GetCurrentActorDisplayNameAsync("System");

                _context.Identifies.Update(identify);
                await _context.SaveChangesAsync();

                try
                {
                    await _notificationService.CreateNotificationFromTemplate(
                        NotificationTemplateKeys.PromotionRejected,
                        identify.UserId,
                        new Dictionary<string, string?> { ["reason"] = reason },
                        "bi-x-circle-fill",
                        "/Register/ProfileDetails"
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خطأ في إرسال إشعار الرفض");
                }

                return Json(new { success = true, message = "✅ تم رفض الطلب بنجاح" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"❌ حدث خطأ: {ex.Message}" });
            }
        }

        // ========== الموافقة على البيانات الأساسية ==========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveBasicInfo(int id)
        {
            try
            {
                var identify = await _context.Identifies.FindAsync(id);
                if (identify == null)
                    return Json(new { success = false, message = "المستخدم غير موجود" });

                identify.IsBasicInfoApproved = true;
                identify.BasicInfoApprovedBy = await GetCurrentActorDisplayNameAsync("System");
                identify.BasicInfoApprovalDate = DateTime.Now;

                _context.Identifies.Update(identify);
                await _context.SaveChangesAsync();

                try
                {
                    await _notificationService.CreateNotificationFromTemplate(
                        NotificationTemplateKeys.BasicInfoApproved,
                        identify.UserId,
                        icon: "bi-check-circle",
                        clickUrl: "/Register/AdditionalInfo"
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خطأ في إرسال إشعار الموافقة");
                }

                return Json(new { success = true, message = "✅ تمت الموافقة على البيانات الأساسية" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"❌ حدث خطأ: {ex.Message}" });
            }
        }

        // ========== رفض البيانات الأساسية ==========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectBasicInfo(int id, string reason)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(reason))
                    return Json(new { success = false, message = "❌ الرجاء كتابة سبب الرفض" });

                var identify = await _context.Identifies.FindAsync(id);
                if (identify == null)
                    return Json(new { success = false, message = "❌ المستخدم غير موجود" });

                identify.BasicInfoRejectionReason = reason;
                identify.IsBasicInfoApproved = false;
                identify.BasicInfoApprovedBy = await GetCurrentActorDisplayNameAsync("System");
                identify.BasicInfoApprovalDate = null;

                _context.Identifies.Update(identify);
                await _context.SaveChangesAsync();

                try
                {
                    await _notificationService.CreateNotificationFromTemplate(
                        NotificationTemplateKeys.BasicInfoRejected,
                        identify.UserId,
                        new Dictionary<string, string?> { ["reason"] = reason },
                        "bi-x-circle-fill",
                        "/Register/BasicInfo"
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خطأ في إرسال إشعار الرفض");
                }

                return Json(new { success = true, message = "✅ تم رفض الطلب بنجاح" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"❌ حدث خطأ: {ex.Message}" });
            }
        }

        // ========== عرض تفاصيل مستخدم مع المسؤوليات الإدارية ==========
        public async Task<IActionResult> UserDetails(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    TempData["ErrorMessage"] = "معرف المستخدم مطلوب";
                    return RedirectToAction(nameof(Users));
                }

                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "المستخدم غير موجود";
                    return RedirectToAction(nameof(Users));
                }

                var userProfile = await _context.Identifies
                    .FirstOrDefaultAsync(i => i.UserId == id);
                var roles = await _userManager.GetRolesAsync(user);
                var rolesList = roles.ToList();

                if (userProfile == null)
                    _logger.LogWarning($"⚠️ لا يوجد ملف شخصي للمستخدم {id}");

                var address = await GetUserAddressAsync(id);
                var voterCard = await GetUserVoterCardAsync(id);
                var union = await GetUserUnionAsync(id);
                var federation = await GetUserFederationAsync(id);
                var association = await GetUserAssociationAsync(id);
                var ngo = await GetUserNgoAsync(id);
                var affiliationInfo = await GetUserAffiliationInfoAsync(id);

                if (address == null)
                    _logger.LogWarning($"⚠️ [UserDetails] لا يوجد عنوان للمستخدم {id}");

                if (userProfile?.AccountType == "فرد" && !rolesList.Contains("فرد"))
                    rolesList.Add("فرد");

                var affiliationEntityName = await GetAffiliationEntityNameAsync(affiliationInfo?.AffiliationEntityId);
                var divisionName = await GetDivisionNameAsync(affiliationInfo?.DivisionId);
                var sectionName = await GetSectionNameAsync(affiliationInfo?.SectionId);
                var groupName = await GetGroupNameAsync(affiliationInfo?.GroupId);

                var managementAssignments = await _context.ManagementAssignments
                    .Where(x => x.UserId == id)
                    .ToListAsync();
                var managementDisplayList = new List<ManagementAssignmentDisplayVM>();

                foreach (var assignment in managementAssignments)
                {
                    string? entityName = null;
                    string? divisionNameAssign = null;
                    string? sectionNameAssign = null;
                    string? groupNameAssign = null;

                    if (assignment.ManagementLevel == "Entity" && assignment.AffiliationEntityId.HasValue)
                    {
                        var entity = await _context.AffiliationEntities
                            .FirstOrDefaultAsync(e => e.Id == assignment.AffiliationEntityId.Value);
                        entityName = entity?.Name;
                    }
                    else if (assignment.ManagementLevel == "Division" && assignment.DivisionId.HasValue)
                    {
                        var division = await _context.Divisions
                            .FirstOrDefaultAsync(d => d.Id == assignment.DivisionId.Value);
                        divisionNameAssign = division?.Name;
                    }
                    else if (assignment.ManagementLevel == "Section" && assignment.SectionId.HasValue)
                    {
                        var section = await _context.Sections
                            .FirstOrDefaultAsync(s => s.Id == assignment.SectionId.Value);
                        sectionNameAssign = section?.Name;
                    }
                    else if (assignment.ManagementLevel == "Group" && assignment.GroupId.HasValue)
                    {
                        var group = await _context.Groups
                            .FirstOrDefaultAsync(g => g.Id == assignment.GroupId.Value);
                        groupNameAssign = group?.Name; 
                    }

                    managementDisplayList.Add(new ManagementAssignmentDisplayVM
                    {
                        Id = assignment.Id,
                        Level = assignment.ManagementLevel,
                        AssignmentRole = assignment.AssignmentRole,
                        LevelArabic = GetArabicLevelName(
          assignment.ManagementLevel,
          assignment.AssignmentRole
      ),
                        Governorate = assignment.Governorate,
                        EntityName = entityName,
                        DivisionName = divisionNameAssign,
                        SectionName = sectionNameAssign,
                        GroupName = groupNameAssign,
                        CreatedAt = assignment.CreatedAt
                    });
                }

                var viewModel = new SuperAdminUserDetailsVM
                {
                    UserId = user.Id,
                    Email = user.Email ?? "",
                    PhoneNumber = userProfile?.PhoneNumber ?? "",
                    WhatsAppNumber = !string.IsNullOrWhiteSpace(userProfile?.WhatsAppNumber)
                        ? userProfile.WhatsAppNumber
                        : userProfile?.PhoneNumber ?? "",
                    IsWhatsAppVerified = userProfile?.IsWhatsAppVerified ?? false,
                    WhatsAppVerifiedAt = userProfile?.WhatsAppVerifiedAt,
                    IsActive = user.EmailConfirmed,
                    Roles = string.Join(", ", rolesList),
                    FullName = userProfile?.FullName ?? "غير مكتمل",
                    LastName = userProfile?.LastName ?? "",
                    MotherName = userProfile?.MotherName ?? "",
                    DateOfBirth = userProfile?.Date ?? DateTime.MinValue,
                    Gender = userProfile?.Gender ?? "غير محدد",
                    MaritalStatus = userProfile?.MaritalStatus ?? "",
                    Education = userProfile?.Education ?? "",
                    Specialization = userProfile?.Specialization ?? "",
                    UniversityType = userProfile?.UniversityType ?? "",
                    InstitutionType = userProfile?.InstitutionType ?? "",
                    InstitutionName = userProfile?.InstitutionName ?? "",
                    FacultyDepartment = userProfile?.FacultyDepartment ?? "",
                    StudyType = userProfile?.StudyType ?? "",
                    StudyStage = userProfile?.StudyStage ?? "",
                    IdentityCardN = userProfile?.IdentityCardN ?? "",
                    IdentityDate = userProfile?.identityDate ?? DateTime.MinValue,
                    JobTitle = userProfile?.JobTitle ?? "",
                    JobGrade = userProfile?.JobGrade ?? "",
                    WorkGovernorate = GetEffectiveGovernorate(userProfile, address),
                    WorkDistrict = GetEffectiveDistrict(userProfile, address),
                    Address = address,
                    ManagedGovernorate = userProfile?.ManagedGovernorate,
                    ManagedDistrict = userProfile?.ManagedDistrict,
                    CoverImage = userProfile?.CoverImage,
                    CreatedAt = userProfile?.CreatedAt ?? DateTime.MinValue,
                    AffiliationDate = userProfile?.AffiliationDate,
                    RegistrationDate = userProfile?.CreatedAt,
                    EmploymentStatus = userProfile?.EmploymentStatus,
                    Work = userProfile?.Work,
                    Ministry = userProfile?.Ministry,
                    Department = userProfile?.Department,
                    Position = userProfile?.Position,
                    RequestedPromotion = userProfile?.RequestedPromotion ?? false,
                    RequestedPromotionDate = userProfile?.RequestedPromotionDate,
                    RejectionReason = userProfile?.RejectionReason,
                    AccountType = userProfile?.AccountType ?? "عادي",
                    IsPromoted = userProfile?.IsPromoted ?? false,
                    PromotionDate = userProfile?.PromotionDate,
                    PromotedBy = await ResolveActorDisplayNameAsync(userProfile?.PromotedBy),
                    AffiliationEntity = affiliationEntityName,
                    Division = divisionName,
                    Section = sectionName,
                    Group = groupName,
                    AffiliationMozakeName = affiliationInfo?.MozakeName,
                    MozakePhoneNumber = affiliationInfo?.MozakePhoneNumber,
                    BadgeNumber = affiliationInfo?.BadgeNumber,
                    AffiliationEntryDate = affiliationInfo?.AffiliationDate,
                    UnionName = union?.UnionName,
                    UnionPosition = union?.Position,
                    UnionIdNumber = union?.IdNumber,
                    UnionAffiliationDate = union?.AffiliationDate,
                    FederationName = GetFederationFullName(federation),
                    FederationDivisionName = federation?.FederationDivision?.Name,
                    FederationSectionName = federation?.FederationSection?.Name,
                    FederationGroupName = federation?.FederationGroup?.Name,
                    FederationPosition = federation?.Position,
                    FederationIdNumber = federation?.IdNumber,
                    FederationAffiliationDate = federation?.AffiliationDate,
                    AssociationName = association?.AssociationName,
                    AssociationPosition = association?.Position,
                    AssociationIdNumber = association?.IdNumber,
                    AssociationAffiliationDate = association?.AffiliationDate,
                    NgoName = ngo?.NgoName,
                    NgoPosition = ngo?.Position,
                    NgoIdNumber = ngo?.IdNumber,
                    NgoAffiliationDate = ngo?.AffiliationDate,
                    VoterCardNumber = voterCard?.VoterCardNumber,
                    PollingCenterNumber = voterCard?.PollingCenterNumber,
                    ManagementAssignments = managementDisplayList
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في عرض تفاصيل المستخدم");
                TempData["ErrorMessage"] = "حدث خطأ في تحميل البيانات";
                return RedirectToAction(nameof(Users));
            }
        }

        // ========== إرسال الإشعارات ==========
        [HttpGet]
        public async Task<IActionResult> SendNotification(string? userId = null, string? audience = null)
        {
            ViewBag.Users = new List<object>();
            var model = new SendNotificationViewModel();

            var reportAudienceFilters = string.Equals(audience, "reports", StringComparison.OrdinalIgnoreCase)
                ? GetSavedReportNotificationFilters()
                : null;

            if (!string.IsNullOrWhiteSpace(reportAudienceFilters))
            {
                var filters = DeserializeReportFilters(reportAudienceFilters);
                var reportAudienceUserIds = await GetReportUserIdsAsync(filters);
                var selectedUsers = await _userManager.Users
                    .AsNoTracking()
                    .Where(u => reportAudienceUserIds.Contains(u.Id))
                    .OrderBy(u => u.Email)
                    .Select(u => new { u.Id, u.Email, u.UserName })
                    .ToListAsync();

                var selectedProfiles = await _context.Identifies
                    .AsNoTracking()
                    .Where(i => reportAudienceUserIds.Contains(i.UserId))
                    .ToDictionaryAsync(i => i.UserId, i => i.FullName);

                model.ReportFiltersJson = reportAudienceFilters;

                ViewBag.IsReportAudience = true;
                ViewBag.ReportAudienceCount = reportAudienceUserIds.Count;
                ViewBag.ReportAudiencePreview = selectedUsers
                    .Take(10)
                    .Select(u => selectedProfiles.TryGetValue(u.Id, out var fullName) && !string.IsNullOrWhiteSpace(fullName)
                        ? fullName!
                        : (u.Email ?? u.UserName ?? u.Id))
                    .ToList();

                return View(model);
            }

            var recipientDirectory = await BuildNotificationRecipientDirectoryAsync();
            ViewBag.Users = recipientDirectory
                .Select(user => new { user.Id, user.Email, user.FullName })
                .Cast<object>()
                .ToList();
            ViewBag.NotificationUsersJson = JsonSerializer.Serialize(recipientDirectory);
            ViewBag.NotificationGovernorates = recipientDirectory
                .Select(user => user.Governorate)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.Create(new System.Globalization.CultureInfo("ar-IQ"), false))
                .ToList();
            ViewBag.NotificationAudienceGroups = new List<object>
            {
                new { Value = "members", Label = "الأفراد" },
                new { Value = "users", Label = "المستخدمون" },
                new { Value = "admins", Label = "المسؤولون والإداريون" }
            };
            var notificationRoles = new List<object>
            {
                new { Value = clsRoles.SystemManager, Label = "مدير النظام" },
                new { Value = clsRoles.SuperAdmin, Label = "سوبر أدمن" },
                new { Value = clsRoles.Admin, Label = "أدمن" },
                new { Value = clsRoles.DistrictAdmin, Label = "أدمن محافظة" },
                new { Value = clsRoles.Manager, Label = "مسؤول" },
                new { Value = clsRoles.AssistantManager, Label = "معاون مسؤول" },
                new { Value = clsRoles.NewsEditor, Label = "محرر أخبار" },
                new { Value = clsRoles.MapViewer, Label = "مشاهد خريطة" },
                new { Value = clsRoles.Member, Label = "فرد" },
                new { Value = clsRoles.User, Label = "مستخدم" }
            };

            if (!CanCurrentUserManageSystemManager())
            {
                notificationRoles = notificationRoles
                    .Where(item => !string.Equals((string?)item.GetType().GetProperty("Value")?.GetValue(item), clsRoles.SystemManager, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            ViewBag.NotificationRoles = notificationRoles;

            if (!string.IsNullOrEmpty(userId))
            {
                var targetUser = await _userManager.FindByIdAsync(userId);
                if (targetUser != null)
                {
                    var targetProfile = await _context.Identifies
                        .FirstOrDefaultAsync(i => i.UserId == userId);
                    ViewBag.TargetUserId = userId;
                    ViewBag.TargetUserEmail = targetUser.Email;
                    ViewBag.TargetUserName = targetProfile?.FullName ?? targetUser.Email;
                    model.TargetUserId = userId;
                    model.SelectedUserIdsCsv = userId;
                }
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendNotification(SendNotificationViewModel model)
        {
            try
            {
                if (string.IsNullOrEmpty(model.Title) || string.IsNullOrEmpty(model.Message))
                {
                    TempData["ErrorMessage"] = "❌ العنوان والرسالة مطلوبان";
                    return RedirectToAction("SendNotification");
                }

                var resolution = await ResolveNotificationRecipientsAsync(model);

                if (!resolution.IsBroadcast && !resolution.UserIds.Any())
                {
                    TempData["WarningMessage"] = "⚠️ لم يتم العثور على مستلمين مطابقين للخيارات المحددة.";
                    return RedirectToAction(nameof(SendNotification));
                }

                if (!resolution.IsBroadcast)
                {
                    foreach (var targetUserId in resolution.UserIds)
                    {
                        await _notificationService.CreateNotification(
                            model.Title,
                            model.Message,
                            targetUserId,
                            model.Icon ?? "bi-bell",
                            model.ClickUrl);
                    }

                    TempData["SuccessMessage"] = $"✅ تم إرسال الإشعار إلى {resolution.UserIds.Count} مستخدم.";
                    ClearSavedReportNotificationRecipients();
                    return resolution.Source == "reports"
                        ? RedirectToAction(nameof(Reports))
                        : RedirectToAction(nameof(SendNotification));
                }

                await _notificationService.CreateNotification(
                    model.Title,
                    model.Message,
                    null,
                    model.Icon ?? "bi-bell",
                    model.ClickUrl);

                TempData["SuccessMessage"] = "✅ تم إرسال الإشعار العام بنجاح.";
                return RedirectToAction(nameof(SendNotification));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ عام في إرسال الإشعار");
                TempData["ErrorMessage"] = $"❌ حدث خطأ: {ex.Message}";
                return RedirectToAction("SendNotification");
            }
        }

        private async Task<List<NotificationRecipientItem>> BuildNotificationRecipientDirectoryAsync()
        {
            var users = await _userManager.Users
                .AsNoTracking()
                .OrderBy(u => u.Email)
                .Select(u => new NotificationRecipientUserSnapshot
                {
                    Id = u.Id,
                    Email = u.Email,
                    UserName = u.UserName,
                    PhoneNumber = u.PhoneNumber
                })
                .ToListAsync();

            var userIds = users
                .Select(u => u.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToList();

            var profiles = await _context.Identifies
                .AsNoTracking()
                .Where(i => userIds.Contains(i.UserId))
                .Select(i => new NotificationRecipientProfileSnapshot
                {
                    UserId = i.UserId,
                    FullName = i.FullName,
                    PhoneNumber = i.PhoneNumber,
                    WorkGovernorate = i.WorkGovernorate,
                    ManagedGovernorate = i.ManagedGovernorate,
                    WorkLocationGovernorate = i.WorkLocation != null ? i.WorkLocation.Governorate : null,
                    IsPromoted = i.IsPromoted,
                    AccountType = i.AccountType
                })
                .ToDictionaryAsync(i => i.UserId, i => i);

            var roleRows = await (from userRole in _context.UserRoles
                                  join role in _context.Roles on userRole.RoleId equals role.Id
                                  where userIds.Contains(userRole.UserId)
                                  select new { userRole.UserId, role.Name })
                .ToListAsync();

            var rolesLookup = roleRows
                .Where(row => !string.IsNullOrWhiteSpace(row.Name))
                .GroupBy(row => row.UserId)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(row => row.Name!)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList());

            return users.Select(user =>
            {
                profiles.TryGetValue(user.Id, out var profile);
                rolesLookup.TryGetValue(user.Id, out var roles);

                var fullName = !string.IsNullOrWhiteSpace(profile?.FullName)
                    ? profile!.FullName
                    : (user.Email ?? user.UserName ?? user.Id);

                var phoneNumber = profile?.PhoneNumber ?? user.PhoneNumber ?? string.Empty;
                var governorate = ResolveRecipientGovernorate(profile);
                var userRoles = roles ?? new List<string>();

                return new NotificationRecipientItem
                {
                    Id = user.Id,
                    Email = user.Email ?? string.Empty,
                    FullName = fullName,
                    PhoneNumber = phoneNumber,
                    Governorate = governorate,
                    AccountType = DetermineAudienceGroup(profile, userRoles),
                    Roles = userRoles
                };
            }).ToList();
        }

        private async Task<NotificationRecipientResolution> ResolveNotificationRecipientsAsync(SendNotificationViewModel model)
        {
            if (!string.IsNullOrWhiteSpace(model.ReportFiltersJson))
            {
                var filters = DeserializeReportFilters(model.ReportFiltersJson);
                var reportUserIds = await GetReportUserIdsAsync(filters);

                return new NotificationRecipientResolution
                {
                    Source = "reports",
                    UserIds = reportUserIds.ToList()
                };
            }

            if (model.TargetUserIds.Any())
            {
                return new NotificationRecipientResolution
                {
                    Source = "reports",
                    UserIds = model.TargetUserIds
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList()
                };
            }

            if (model.SendToAll)
            {
                return new NotificationRecipientResolution
                {
                    Source = "all",
                    IsBroadcast = true
                };
            }

            var recipients = await BuildNotificationRecipientDirectoryAsync();
            var selectedGovernorates = ParseCsvSet(model.SelectedGovernoratesCsv);
            var selectedAudienceGroups = ParseCsvSet(model.SelectedAudienceGroupsCsv);
            var selectedRoles = ParseCsvSet(model.SelectedRolesCsv);
            var selectedUserIds = ParseCsvSet(model.SelectedUserIdsCsv);

            if (!string.IsNullOrWhiteSpace(model.TargetUserId))
                selectedUserIds.Add(model.TargetUserId.Trim());

            var resolvedUserIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var recipient in recipients)
            {
                var matchesGovernorate = selectedGovernorates.Contains(recipient.Governorate);
                var matchesAudience = selectedAudienceGroups.Any(group =>
                    NotificationAudienceGroupMatches(recipient, group));
                var matchesRole = recipient.Roles.Any(role => selectedRoles.Contains(role));
                var matchesUser = selectedUserIds.Contains(recipient.Id);

                if (matchesGovernorate || matchesAudience || matchesRole || matchesUser)
                    resolvedUserIds.Add(recipient.Id);
            }

            return new NotificationRecipientResolution
            {
                Source = "filtered",
                UserIds = resolvedUserIds.ToList()
            };
        }

        private static HashSet<string> ParseCsvSet(string? csv)
        {
            return (csv ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static string ResolveRecipientGovernorate(NotificationRecipientProfileSnapshot? profile)
        {
            if (!string.IsNullOrWhiteSpace(profile?.WorkLocationGovernorate))
                return profile.WorkLocationGovernorate.Trim();

            if (!string.IsNullOrWhiteSpace(profile?.WorkGovernorate))
                return profile.WorkGovernorate.Trim();

            if (!string.IsNullOrWhiteSpace(profile?.ManagedGovernorate))
                return profile.ManagedGovernorate.Trim();

            return string.Empty;
        }

        private static string DetermineAudienceGroup(NotificationRecipientProfileSnapshot? profile, IReadOnlyCollection<string> roles)
        {
            if (roles.Any(IsAdminLikeRole))
                return "admins";

            if ((profile?.IsPromoted ?? false) ||
                string.Equals(profile?.AccountType, clsRoles.Member, StringComparison.OrdinalIgnoreCase) ||
                roles.Any(role => string.Equals(role, clsRoles.Member, StringComparison.OrdinalIgnoreCase)))
            {
                return "members";
            }

            return "users";
        }

        private static bool NotificationAudienceGroupMatches(NotificationRecipientItem user, string? group)
        {
            if (string.IsNullOrWhiteSpace(group))
                return false;

            return string.Equals(user.AccountType, group.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAdminLikeRole(string? role)
        {
            if (string.IsNullOrWhiteSpace(role))
                return false;

            return string.Equals(role, clsRoles.SystemManager, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(role, clsRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(role, clsRoles.Admin, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(role, clsRoles.DistrictAdmin, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(role, clsRoles.Manager, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(role, clsRoles.AssistantManager, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(role, clsRoles.NewsEditor, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(role, clsRoles.MapViewer, StringComparison.OrdinalIgnoreCase);
        }

        // ========== تحديث أدوار المستخدم ==========
        [HttpGet]
        public async Task<IActionResult> GetUserRoles(string userId)
        {
            try
            {
                var canManageSystemManager = CanCurrentUserManageSystemManager();
                if (string.IsNullOrEmpty(userId))
                    return Json(new { success = false, message = "معرف المستخدم مطلوب" });

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                    return Json(new { success = false, message = "المستخدم غير موجود" });

                var userRoles = await _userManager.GetRolesAsync(user);
                if (!canManageSystemManager && userRoles.Contains(clsRoles.SystemManager))
                    return Json(new { success = false, message = "المستخدم غير موجود" });

                var allRoles = new List<string>
                {
                    clsRoles.User,
                    clsRoles.Member,
                    clsRoles.Admin,
                    clsRoles.SuperAdmin,
                    clsRoles.SystemManager,
                    clsRoles.NewsEditor,
                    clsRoles.MapViewer
                };

                if (!canManageSystemManager)
                {
                    allRoles.Remove(clsRoles.SystemManager);
                }

                return Json(new
                {
                    success = true,
                    data = new { UserId = user.Id, UserEmail = user.Email, CurrentRoles = userRoles, AllRoles = allRoles }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateUserRoles([FromBody] UpdateRolesRequest request)
        {
            try
            {
                var canManageSystemManager = CanCurrentUserManageSystemManager();
                if (request == null || string.IsNullOrEmpty(request.UserId))
                    return Json(new { success = false, message = "بيانات غير صالحة" });

                var user = await _userManager.FindByIdAsync(request.UserId);
                if (user == null)
                    return Json(new { success = false, message = "المستخدم غير موجود" });

                var currentRoles = await _userManager.GetRolesAsync(user);
                if (!canManageSystemManager && currentRoles.Contains(clsRoles.SystemManager))
                    return Json(new { success = false, message = "لا يمكنك تعديل مدير النظام" });

                var allowedRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    clsRoles.User,
                    clsRoles.Member,
                    clsRoles.Admin,
                    clsRoles.SuperAdmin,
                    clsRoles.SystemManager,
                    clsRoles.NewsEditor,
                    clsRoles.MapViewer
                };

                if (!canManageSystemManager)
                {
                    allowedRoles.Remove(clsRoles.SystemManager);
                }

                if (request.SelectedRoles != null)
                {
                    request.SelectedRoles = request.SelectedRoles
                        .Select(role => role == clsRoles.DistrictAdmin ? clsRoles.Admin : role)
                        .Where(role => allowedRoles.Contains(role))
                        .Distinct()
                        .ToList();
                }

                request.SelectedRoles ??= new List<string>();

                if (request.SelectedRoles.Contains(clsRoles.Member))
                    request.SelectedRoles.RemoveAll(role => string.Equals(role, clsRoles.User, StringComparison.OrdinalIgnoreCase));

                if (!request.SelectedRoles.Any())
                    return Json(new { success = false, message = "الرجاء اختيار دور واحد على الأقل" });

                var rolesToRemove = currentRoles
                    .Where(role => role == clsRoles.User || allowedRoles.Contains(role))
                    .ToList();

                if (rolesToRemove.Any())
                {
                    var removeResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
                    if (!removeResult.Succeeded)
                        return Json(new { success = false, message = "فشل في إزالة الأدوار الحالية" });
                }

                if (request.SelectedRoles.Any())
                {
                    var addResult = await _userManager.AddToRolesAsync(user, request.SelectedRoles);
                    if (!addResult.Succeeded)
                        return Json(new { success = false, message = "فشل في إضافة الأدوار الجديدة" });
                }

                var isAdminNow = request.SelectedRoles != null &&
                    request.SelectedRoles.Contains(clsRoles.Admin);
                var isMapViewerNow = request.SelectedRoles != null &&
                    request.SelectedRoles.Contains(clsRoles.MapViewer);
                var isMemberNow = request.SelectedRoles?.Contains(clsRoles.Member) == true;

                var userProfile = await _context.Identifies
                    .FirstOrDefaultAsync(i => i.UserId == request.UserId);

                if (isMemberNow && userProfile == null)
                {
                    userProfile = new Identify
                    {
                        UserId = request.UserId,
                        FullName = user.Email ?? user.UserName ?? "فرد",
                        MotherName = "",
                        Email = user.Email ?? "",
                        Date = DateTime.Now,
                        Gender = "ذكر",
                        PhoneNumber = user.PhoneNumber ?? "",
                        IdentityCardN = "",
                        identityDate = DateTime.Now,
                        IsBasicInfoApproved = true,
                        BasicInfoApprovalDate = DateTime.Now,
                        BasicInfoApprovedBy = await GetCurrentActorDisplayNameAsync("System")
                    };
                    _context.Identifies.Add(userProfile);
                    await _context.SaveChangesAsync();
                }

                if (isAdminNow || isMapViewerNow)
                {
                    if (userProfile != null)
                    {
                        if (isMapViewerNow && string.IsNullOrEmpty(request.ManagedGovernorate))
                        {
                            userProfile.ManagedGovernorate = null;
                            userProfile.ManagedDistrict = null;
                        }
                        else if (!string.IsNullOrEmpty(request.ManagedGovernorate))
                        {
                            userProfile.ManagedGovernorate = request.ManagedGovernorate;
                        }
                        else
                        {
                            var userAddress = await GetUserAddressAsync(request.UserId);
                            userProfile.ManagedGovernorate = GetEffectiveGovernorate(userProfile, userAddress) ?? "بغداد";
                        }

                        if (!string.IsNullOrEmpty(request.ManagedDistrict))
                        {
                            userProfile.ManagedDistrict = request.ManagedDistrict;
                        }
                        else
                        {
                            userProfile.ManagedDistrict = null;
                        }

                        _context.Identifies.Update(userProfile);
                    }
                    else
                    {
                        userProfile = new Identify
                        {
                            UserId = request.UserId,
                            FullName = "مدير نظام",
                            ManagedGovernorate = isMapViewerNow ? request.ManagedGovernorate : request.ManagedGovernorate ?? "بغداد",
                            ManagedDistrict = request.ManagedDistrict,
                            Date = DateTime.Now,
                            Gender = "ذكر",
                            PhoneNumber = "",
                            IdentityCardN = "",
                            identityDate = DateTime.Now
                        };
                        _context.Identifies.Add(userProfile);
                    }

                    await _context.SaveChangesAsync();
                }
                else if (userProfile != null && request.SelectedRoles != null &&
                         !request.SelectedRoles.Contains(clsRoles.Admin) &&
                         !request.SelectedRoles.Contains(clsRoles.MapViewer))
                {
                    userProfile.ManagedGovernorate = null;
                    userProfile.ManagedDistrict = null;
                    _context.Identifies.Update(userProfile);
                    await _context.SaveChangesAsync();
                }


                if (userProfile != null && request.SelectedRoles != null)
                {
                    if (request.SelectedRoles.Contains(clsRoles.SystemManager))
                        userProfile.AccountType = "مدير النظام";
                    else if (request.SelectedRoles.Contains("SuperAdmin"))
                        userProfile.AccountType = "سوبر أدمن";
                    else if (request.SelectedRoles.Contains("Admin"))
                        userProfile.AccountType = "أدمن";
                    else if (request.SelectedRoles.Contains("فرد"))
                    {
                        userProfile.AccountType = "فرد";
                        userProfile.IsPromoted = true;
                        userProfile.PromotionDate ??= DateTime.Now;
                        userProfile.PromotedBy ??= await GetCurrentActorDisplayNameAsync("System");
                        userProfile.RequestedPromotion = false;
                        userProfile.RequestedPromotionDate = null;
                        userProfile.RejectionReason = null;
                        userProfile.IsBasicInfoApproved = true;
                        userProfile.BasicInfoApprovalDate ??= DateTime.Now;
                        userProfile.BasicInfoApprovedBy ??= await GetCurrentActorDisplayNameAsync("System");
                        userProfile.BasicInfoRejectionReason = null;
                    }
                    else
                        userProfile.AccountType = "عادي";
                    _context.Identifies.Update(userProfile);
                    await _context.SaveChangesAsync();
                }

                if (request.SelectedRoles != null)
                {
                    try
                    {
                        if (request.SelectedRoles.Contains("SuperAdmin"))
                            await _notificationService.CreateNotificationFromTemplate(NotificationTemplateKeys.SuperAdminAssigned, request.UserId, icon: "bi-crown-fill", clickUrl: "/SuperAdmin/Users");
                        else if (request.SelectedRoles.Contains("Admin"))
                            await _notificationService.CreateNotificationFromTemplate(NotificationTemplateKeys.AdminAssigned, request.UserId, icon: "bi-shield-fill", clickUrl: "/Admin/Users");
                        else if (request.SelectedRoles.Contains("NewsEditor"))
                            await _notificationService.CreateNotificationFromTemplate(NotificationTemplateKeys.NewsEditorAssigned, request.UserId, icon: "bi-newspaper", clickUrl: "/News/Index");
                        else if (request.SelectedRoles.Contains("MapViewer"))
                            await _notificationService.CreateNotificationFromTemplate(NotificationTemplateKeys.MapViewerAssigned, request.UserId, icon: "bi-map", clickUrl: "/MapDashboard/Index");
                        else if (request.SelectedRoles.Contains("فرد"))
                            await _notificationService.CreateNotificationFromTemplate(NotificationTemplateKeys.MemberAssigned, request.UserId, icon: "bi-star-fill", clickUrl: "/Register/ProfileDetails");
                    }
                    catch (Exception ex) { _logger.LogError(ex, "خطأ في إرسال الإشعار"); }
                }

                var finalRoles = await _userManager.GetRolesAsync(user);
                return Json(new { success = true, message = "✅ تم تحديث أدوار المستخدم بنجاح", newRoles = finalRoles.ToList(), userEmail = user.Email });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"❌ حدث خطأ: {ex.Message}" });
            }
        }

        private async Task RemoveUserRelatedDataAsync(string userId)
        {
            var managementAssignments = await _context.ManagementAssignments
                .Where(a => a.UserId == userId)
                .ToListAsync();
            if (managementAssignments.Any()) _context.ManagementAssignments.RemoveRange(managementAssignments);

            var managementRequests = await _context.ManagementAssignmentRequests
                .Where(r => r.UserId == userId || r.RequestedByUserId == userId)
                .ToListAsync();
            if (managementRequests.Any()) _context.ManagementAssignmentRequests.RemoveRange(managementRequests);

            var address = await GetUserAddressAsync(userId);
            if (address != null) _context.Addresses.Remove(address);

            var affiliation = await GetUserAffiliationInfoAsync(userId);
            if (affiliation != null) _context.AffiliationInfos.Remove(affiliation);

            var voterCard = await GetUserVoterCardAsync(userId);
            if (voterCard != null) _context.VoterCards.Remove(voterCard);

            var union = await GetUserUnionAsync(userId);
            if (union != null) _context.UnionMemberships.Remove(union);

            var federation = await GetUserFederationAsync(userId);
            if (federation != null) _context.FederationMemberships.Remove(federation);

            var association = await GetUserAssociationAsync(userId);
            if (association != null) _context.AssociationMemberships.Remove(association);

            var ngo = await GetUserNgoAsync(userId);
            if (ngo != null) _context.NgoMemberships.Remove(ngo);

            var notifications = await _context.Notifications.Where(n => n.TargetUserId == userId).ToListAsync();
            if (notifications.Any()) _context.Notifications.RemoveRange(notifications);

            var devices = await _context.UserDevices.Where(d => d.UserId == userId).ToListAsync();
            if (devices.Any()) _context.UserDevices.RemoveRange(devices);

            var userProfile = await _context.Identifies.FirstOrDefaultAsync(i => i.UserId == userId);
            if (userProfile != null) _context.Identifies.Remove(userProfile);
        }

        // ========== حذف مستخدم واحد ==========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser([FromBody] DeleteUserRequest request)
        {
            try
            {
                var canManageSystemManager = CanCurrentUserManageSystemManager();
                if (string.IsNullOrEmpty(request.UserId))
                    return Json(new { success = false, message = "معرف المستخدم مطلوب" });

                var user = await _userManager.FindByIdAsync(request.UserId);
                if (user == null)
                    return Json(new { success = false, message = "المستخدم غير موجود" });

                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains(clsRoles.SystemManager) && !canManageSystemManager)
                    return Json(new { success = false, message = "لا يمكن حذف مدير النظام" });
                if (roles.Contains(clsRoles.SuperAdmin))
                    return Json(new { success = false, message = "لا يمكن حذف حساب سوبر أدمن" });

                if (roles.Any())
                {
                    var removeRolesResult = await _userManager.RemoveFromRolesAsync(user, roles);
                    if (!removeRolesResult.Succeeded)
                        return Json(new { success = false, message = "❌ فشل في إزالة أدوار المستخدم قبل الحذف" });
                }

                await RemoveUserRelatedDataAsync(request.UserId);
                await _context.SaveChangesAsync();

                var result = await _userManager.DeleteAsync(user);
                if (result.Succeeded)
                    return Json(new { success = true, message = "✅ تم حذف المستخدم وجميع بياناته بنجاح" });

                return Json(new { success = false, message = $"❌ فشل في حذف المستخدم" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"❌ حدث خطأ: {ex.Message}" });
            }
        }

        // ========== الحذف المتعدد ==========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkDeleteUsers([FromBody] BulkDeleteRequest request)
        {
            try
            {
                var canManageSystemManager = CanCurrentUserManageSystemManager();
                if (request?.UserIds == null || !request.UserIds.Any())
                    return Json(new { success = false, message = "الرجاء تحديد مستخدمين للحذف" });

                int successCount = 0, failCount = 0;
                foreach (var userId in request.UserIds)
                {
                    try
                    {
                        var user = await _userManager.FindByIdAsync(userId);
                        if (user == null) { failCount++; continue; }

                        var roles = await _userManager.GetRolesAsync(user);
                        if (roles.Contains(clsRoles.SystemManager) && !canManageSystemManager) { failCount++; continue; }
                        if (roles.Contains(clsRoles.SuperAdmin)) { failCount++; continue; }

                        if (roles.Any())
                        {
                            var removeRolesResult = await _userManager.RemoveFromRolesAsync(user, roles);
                            if (!removeRolesResult.Succeeded) { failCount++; continue; }
                        }

                        await RemoveUserRelatedDataAsync(userId);
                        await _context.SaveChangesAsync();

                        var result = await _userManager.DeleteAsync(user);
                        if (result.Succeeded) successCount++;
                        else failCount++;
                    }
                    catch { failCount++; }
                }

                return Json(new { success = true, message = $"✅ تم حذف {successCount} مستخدمين، فشل {failCount}" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"❌ حدث خطأ: {ex.Message}" });
            }
        }

        // ========== تغيير كلمة مرور مستخدم ==========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetUserPassword([FromBody] ResetUserPasswordRequest request)
        {
            try
            {
                if (!await IsCurrentUserPasswordResetAllowedAsync())
                    return Json(new { success = false, message = "ليست لديك صلاحية تغيير كلمات المرور" });

                if (request == null || string.IsNullOrWhiteSpace(request.UserId))
                    return Json(new { success = false, message = "معرف المستخدم مطلوب" });

                if (string.IsNullOrWhiteSpace(request.NewPassword))
                    return Json(new { success = false, message = "كلمة المرور الجديدة مطلوبة" });

                var user = await _userManager.FindByIdAsync(request.UserId);
                if (user == null)
                    return Json(new { success = false, message = "المستخدم غير موجود" });

                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var result = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);

                if (result.Succeeded)
                    return Json(new { success = true, message = "✅ تم تغيير كلمة المرور بنجاح" });

                var errors = string.Join("، ", result.Errors.Select(e => e.Description));
                return Json(new { success = false, message = $"❌ فشل تغيير كلمة المرور: {errors}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في تغيير كلمة مرور المستخدم");
                return Json(new { success = false, message = $"❌ حدث خطأ: {ex.Message}" });
            }
        }

        // ========== تفعيل/تعطيل المستخدم ==========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUserStatus([FromBody] ToggleStatusRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.UserId))
                    return Json(new { success = false, message = "معرف المستخدم مطلوب" });

                var user = await _userManager.FindByIdAsync(request.UserId);
                if (user == null)
                    return Json(new { success = false, message = "المستخدم غير موجود" });

                user.EmailConfirmed = !user.EmailConfirmed;
                var result = await _userManager.UpdateAsync(user);

                if (result.Succeeded)
                    return Json(new { success = true, message = $"✅ تم {(user.EmailConfirmed ? "تفعيل" : "تعطيل")} حساب المستخدم", isActive = user.EmailConfirmed });

                return Json(new { success = false, message = "❌ فشل في تغيير حالة المستخدم" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"❌ حدث خطأ: {ex.Message}" });
            }
        }

        // ========== تعديل بيانات المستخدم بواسطة السوبر أدمن ==========
        [HttpGet]
        public async Task<IActionResult> EditUser(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    TempData["ErrorMessage"] = "معرف المستخدم مطلوب";
                    return RedirectToAction(nameof(Users));
                }

                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "المستخدم غير موجود";
                    return RedirectToAction(nameof(Users));
                }

                var profile = await GetUserProfileAsync(id);
                var address = await GetUserAddressAsync(id);
                var voterCard = await GetUserVoterCardAsync(id);
                var union = await GetUserUnionAsync(id);
                var federation = await GetUserFederationAsync(id);
                var association = await GetUserAssociationAsync(id);
                var ngo = await GetUserNgoAsync(id);
                var affiliationInfo = await GetUserAffiliationInfoAsync(id);
                var workLocation = profile?.WorkLocation ?? (profile != null
                    ? await _context.WorkLocations.FirstOrDefaultAsync(w => w.IdentifyId == profile.Id)
                    : null);
                var roles = await _userManager.GetRolesAsync(user);

                var viewModel = new CompleteProfileViewModel
                {
                    UserId = user.Id,
                    Email = user.Email ?? "",
                    UserRole = string.Join(", ", roles),
                    IsEmailConfirmed = user.EmailConfirmed,
                    CreatedAt = profile?.CreatedAt ?? DateTime.UtcNow,
                    PersonalInfo = new PersonalInfoViewModel
                    {
                        FullName = profile?.FullName ?? "",
                        LastName = profile?.LastName ?? "",
                        MotherName = profile?.MotherName ?? "",
                        DateOfBirth = profile?.Date ?? DateTime.Now,
                        Gender = profile?.Gender ?? "",
                        MaritalStatus = profile?.MaritalStatus ?? "",
                        PhoneNumber = profile?.PhoneNumber ?? "",
                        Education = profile?.Education ?? "",
                        Specialization = profile?.Specialization ?? "",
                        CoverImage = profile?.CoverImage ?? "",
                        UniversityType = profile?.UniversityType ?? "",
                        InstitutionType = profile?.InstitutionType ?? "",
                        InstitutionName = profile?.InstitutionName ?? "",
                        FacultyDepartment = profile?.FacultyDepartment ?? "",
                        StudyType = profile?.StudyType ?? "",
                        StudyStage = profile?.StudyStage ?? "",
                        StudyStagesList = GetStudyStages()   // <-- هذا هو الحل


                    },
                    Address = new AddressViewModel
                    {
                        Governorate = address?.Governorate ?? "",
                        District = address?.District ?? "",
                        Area = address?.Area ?? "",
                        Alley = address?.Alley ?? "",
                        Street = address?.Street ?? "",
                        House = address?.House ?? "",
                        NearestPoint = address?.NearestPoint ?? ""
                    },
                    Documents = new DocumentsViewModel
                    {
                        VoterCardNumber = voterCard?.VoterCardNumber ?? "",
                        PollingCenterNumber = voterCard?.PollingCenterNumber ?? ""
                    },
                    Employment = new EmploymentViewModel
                    {
                        EmploymentStatus = profile?.EmploymentStatus ?? "",
                        Work = profile?.Work ?? "",
                        Ministry = profile?.Ministry ?? "",
                        Department = profile?.Department ?? "",
                        Position = profile?.Position ?? "",
                        JobTitle = profile?.JobTitle ?? "",
                        JobGrade = profile?.JobGrade ?? ""
                    },
                    WorkLocation = new WorkLocationViewModel
                    {
                        Governorate = workLocation?.Governorate ?? profile?.WorkGovernorate ?? "",
                        District = workLocation?.District ?? profile?.WorkDistrict ?? ""
                    },
                    Affiliation = new AffiliationViewModel
                    {
                        AffiliationEntity = await GetAffiliationEntityNameAsync(affiliationInfo?.AffiliationEntityId),
                        Division = await GetDivisionNameAsync(affiliationInfo?.DivisionId),
                        Section = await GetSectionNameAsync(affiliationInfo?.SectionId),
                        Group = await GetGroupNameAsync(affiliationInfo?.GroupId),
                        MozakeName = affiliationInfo?.MozakeName ?? "",
                        MozakePhoneNumber = affiliationInfo?.MozakePhoneNumber ?? "",
                        BadgeNumber = affiliationInfo?.BadgeNumber ?? "",
                        AffiliationDate = affiliationInfo?.AffiliationDate
                    },
                    Memberships = new MembershipViewModel
                    {
                        UnionName = union?.UnionName ?? "",
                        UnionPosition = union?.Position ?? "",
                        UnionIdNumber = union?.IdNumber ?? "",
                        UnionAffiliationDate = union?.AffiliationDate,
                        FederationName = GetFederationFullName(federation),
                        FederationPosition = federation?.Position ?? "",
                        FederationIdNumber = federation?.IdNumber ?? "",
                        FederationAffiliationDate = federation?.AffiliationDate,
                        AssociationName = association?.AssociationName ?? "",
                        AssociationPosition = association?.Position ?? "",
                        AssociationIdNumber = association?.IdNumber ?? "",
                        AssociationAffiliationDate = association?.AffiliationDate,
                        NgoName = ngo?.NgoName ?? "",
                        NgoPosition = ngo?.Position ?? "",
                        NgoIdNumber = ngo?.IdNumber ?? "",
                        NgoAffiliationDate = ngo?.AffiliationDate
                    },
                    // ✅ أضف هذه الأسطر الخمسة الجديدة هنا (بعد Memberships وقبل IdentityCardN)
                    AffiliationEntityId = affiliationInfo?.AffiliationEntityId,
                    DivisionId = affiliationInfo?.DivisionId,
                    SectionId = affiliationInfo?.SectionId,
                    GroupId = affiliationInfo?.GroupId,

                    IdentityCardN = profile?.IdentityCardN ?? "",
                    IdentityDate = profile?.identityDate ?? DateTime.Now,
                    AccountType = profile?.AccountType ?? "عادي",
                    IsPromoted = profile?.IsPromoted ?? false,
                    PromotionDate = profile?.PromotionDate,
                    PromotedBy = await ResolveActorDisplayNameAsync(profile?.PromotedBy)
                };

                await LoadAdminDropdownLists(viewModel);
                ViewBag.TargetUserName = profile?.FullName ?? user.Email;
                ViewBag.IsEditing = true;

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في تحميل صفحة تعديل المستخدم");
                TempData["ErrorMessage"] = $"حدث خطأ: {ex.Message}";
                return RedirectToAction(nameof(Users));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(CompleteProfileViewModel model)
        {
            try
            {
                await LoadAdminDropdownLists(model);

                if (model.Address?.Governorate == "بغداد" && string.IsNullOrWhiteSpace(model.Address.District))
                {
                    ModelState.AddModelError("Address.District", "القضاء مطلوب عند اختيار محافظة السكن بغداد");
                }

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("❌ النموذج غير صالح للتعديل");
                    return View(model);
                }

                var user = await _userManager.FindByIdAsync(model.UserId);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "المستخدم غير موجود";
                    return RedirectToAction(nameof(Users));
                }

                string? coverImagePath = null;
                if (model.CoverImageFile != null && model.CoverImageFile.Length > 0)
                    coverImagePath = await SaveAdminCoverImage(model.CoverImageFile);

                var profile = await _context.Identifies
                    .FirstOrDefaultAsync(i => i.UserId == model.UserId);

                if (profile == null)
                {
                    profile = new Identify { UserId = model.UserId };
                    _context.Identifies.Add(profile);
                }

                profile.FullName = model.PersonalInfo.FullName ?? "";
                profile.LastName = model.PersonalInfo.LastName ?? "";
                profile.MotherName = model.PersonalInfo.MotherName ?? "";
                profile.Date = model.PersonalInfo.DateOfBirth;
                profile.Gender = model.PersonalInfo.Gender ?? "";
                profile.MaritalStatus = model.PersonalInfo.MaritalStatus ?? "";
                profile.PhoneNumber = model.PersonalInfo.PhoneNumber ?? "";
                profile.Education = model.PersonalInfo.Education ?? "";
                profile.Specialization = model.PersonalInfo.Specialization ?? "";
                profile.UniversityType = model.PersonalInfo.UniversityType;
                profile.InstitutionType = model.PersonalInfo.InstitutionType;
                profile.InstitutionName = model.PersonalInfo.InstitutionName;
                profile.FacultyDepartment = model.PersonalInfo.FacultyDepartment;
                profile.StudyType = model.PersonalInfo.StudyType;
                profile.StudyStage = model.PersonalInfo.StudyStage;
                profile.IdentityCardN = model.IdentityCardN;
                profile.identityDate = model.IdentityDate;
                profile.EmploymentStatus = model.Employment.EmploymentStatus;
                profile.Work = model.Employment.Work;
                profile.Ministry = model.Employment.Ministry;
                profile.Department = model.Employment.Department;
                profile.Position = model.Employment.Position;
                profile.JobTitle = model.Employment.JobTitle;
                profile.JobGrade = model.Employment.JobGrade;
                NormalizeWorkLocation(model.WorkLocation);
                var normalizedWorkGovernorate = string.Equals(model.WorkLocation.Governorate, "كل المحافظات", StringComparison.OrdinalIgnoreCase)
                    ? "مركزي"
                    : string.Equals(model.WorkLocation.Governorate, "بغداد عامة", StringComparison.OrdinalIgnoreCase)
                    ? "بغداد عامة"
                    : model.WorkLocation.Governorate;

                profile.WorkGovernorate = normalizedWorkGovernorate;
                profile.WorkDistrict = null;

                if (coverImagePath != null)
                {
                    if (!string.IsNullOrEmpty(profile.CoverImage))
                    {
                        var oldImagePath = Path.Combine("C:\\Users", "Public", "MyApp_Uploads", "Profiles", Path.GetFileName(profile.CoverImage));
                        if (System.IO.File.Exists(oldImagePath))
                            System.IO.File.Delete(oldImagePath);
                    }
                    profile.CoverImage = coverImagePath;
                }

                _context.Identifies.Update(profile);
                await _context.SaveChangesAsync();

                await AdminUpdateOrCreateAddress(model.UserId, model.Address);
                await AdminUpdateOrCreateVoterCard(model.UserId, model.Documents);
                model.WorkLocation.Governorate = normalizedWorkGovernorate;
                model.WorkLocation.District = null;

                await AdminUpdateOrCreateWorkLocation(profile.Id, model.WorkLocation);

                await UpdateOrCreateAffiliationInfoWithIds(model.UserId, model.Affiliation,
    model.AffiliationEntityId, model.DivisionId, model.SectionId, model.GroupId);
                await AdminUpdateOrCreateUnion(model.UserId, model.Memberships);
                await AdminUpdateOrCreateFederationWithIds(model.UserId, model.Memberships);
                await AdminUpdateOrCreateAssociation(model.UserId, model.Memberships);
                await AdminUpdateOrCreateNgo(model.UserId, model.Memberships);
                await _context.SaveChangesAsync();

                try
                {
                    await _notificationService.CreateNotificationFromTemplate(NotificationTemplateKeys.ProfileUpdated, model.UserId, icon: "bi-pencil-square", clickUrl: "/Register/ProfileDetails");
                }
                catch (Exception ex) { _logger.LogError(ex, "خطأ في إرسال الإشعار"); }

                TempData["SuccessMessage"] = "✅ تم تحديث بيانات المستخدم بنجاح!";
                return RedirectToAction(nameof(UserDetails), new { id = model.UserId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ في تعديل بيانات المستخدم");
                TempData["ErrorMessage"] = $"حدث خطأ: {ex.Message}";
                return View(model);
            }
        }

        // ========== دوال مساعدة لتعديل السوبر أدمن ==========
        private async Task LoadAdminDropdownLists(CompleteProfileViewModel model)
        {
            model.Governorates = GetGovernorates();
            model.Genders = GetGenders();
            model.Educations = GetEducations();
            model.Ministries = GetMinistries();
            model.EmploymentStatuses = GetEmploymentStatuses();
            model.StudyStagesList = GetStudyStages();
            model.JobGradesList = GetJobGrades();  // ✅ أضف هذا السطر الجديد
            model.AffiliationEntities = await GetDistinctAffiliationEntitiesAsync();
            model.DivisionsList = await GetDistinctDivisionsAsync();
            model.SectionsList = await GetDistinctSectionsAsync();
            model.GroupsList = await GetDistinctGroupsAsync();
            model.UnionsList = await GetDistinctUnionsAsync();
            model.FederationsList = await GetDistinctFederationsAsync();
            model.FederationDivisionsList = await GetDistinctFederationDivisionsAsync();
            model.FederationSectionsList = await GetDistinctFederationSectionsAsync();
            model.FederationGroupsList = await GetDistinctFederationGroupsAsync();
            model.AssociationsList = await GetDistinctAssociationsAsync();
            model.NgosList = await GetDistinctNgosAsync();
        }

        private async Task<string?> SaveAdminCoverImage(IFormFile coverImageFile)
        {
            if (coverImageFile == null) return null;
            var uploadsFolder = Path.Combine("C:\\Users", "Public", "MyApp_Uploads", "Profiles");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);
            var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(coverImageFile.FileName);
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);
            using (var fileStream = new FileStream(filePath, FileMode.Create))
                await coverImageFile.CopyToAsync(fileStream);
            return "/MyApp_Uploads/Profiles/" + uniqueFileName;
        }

        private async Task AdminUpdateOrCreateAddress(string userId, AddressViewModel model)
        {
            if (string.IsNullOrEmpty(model.Governorate)) return;
            var existingAddress = await GetUserAddressAsync(userId);
            if (existingAddress != null)
            {
                existingAddress.Governorate = model.Governorate ?? "";
                existingAddress.District = model.District ?? "";
                existingAddress.Area = model.Area ?? "";
                existingAddress.Alley = model.Alley ?? "";
                existingAddress.Street = model.Street ?? "";
                existingAddress.House = model.House ?? "";
                existingAddress.NearestPoint = model.NearestPoint ?? "";
                _context.Addresses.Update(existingAddress);
            }
            else
            {
                var address = new Address
                {
                    UserId = userId,
                    Governorate = model.Governorate ?? "",
                    District = model.District ?? "",
                    Area = model.Area ?? "",
                    Alley = model.Alley ?? "",
                    Street = model.Street ?? "",
                    House = model.House ?? "",
                    NearestPoint = model.NearestPoint ?? ""
                };
                _context.Addresses.Add(address);
            }
            await _context.SaveChangesAsync();
        }

        private async Task AdminUpdateOrCreateVoterCard(string userId, DocumentsViewModel model)
        {
            if (string.IsNullOrEmpty(model.VoterCardNumber) && string.IsNullOrEmpty(model.PollingCenterNumber)) return;
            var existing = await GetUserVoterCardAsync(userId);
            if (existing != null)
            {
                existing.VoterCardNumber = model.VoterCardNumber;
                existing.PollingCenterNumber = model.PollingCenterNumber;
                _context.VoterCards.Update(existing);
            }
            else if (!string.IsNullOrEmpty(model.VoterCardNumber))
            {
                var voterCard = new VoterCard { UserId = userId, VoterCardNumber = model.VoterCardNumber, PollingCenterNumber = model.PollingCenterNumber };
                _context.VoterCards.Add(voterCard);
            }
            await _context.SaveChangesAsync();
        }

        private async Task AdminUpdateOrCreateWorkLocation(int identifyId, WorkLocationViewModel model)
        {
            NormalizeWorkLocation(model);
            var existingWorkLocation = await _context.WorkLocations
                .FirstOrDefaultAsync(w => w.IdentifyId == identifyId);

            var governorate = model.Governorate ?? string.Empty;
            var district = governorate == "بغداد" ? model.District : null;

            if (string.IsNullOrWhiteSpace(governorate))
            {
                if (existingWorkLocation != null)
                {
                    _context.WorkLocations.Remove(existingWorkLocation);
                    await _context.SaveChangesAsync();
                }

                return;
            }

            if (existingWorkLocation == null)
            {
                existingWorkLocation = new WorkLocation
                {
                    IdentifyId = identifyId
                };

                _context.WorkLocations.Add(existingWorkLocation);
            }

            existingWorkLocation.Governorate = governorate;
            existingWorkLocation.District = district;
            await _context.SaveChangesAsync();
        }

        private static void NormalizeWorkLocation(WorkLocationViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Governorate))
                return;

            model.Governorate = model.Governorate.Trim();
            model.District = null;
        }

        private async Task AdminUpdateOrCreateAffiliationInfo(string userId, AffiliationViewModel model)
        {
            // ✅ لا تقم بأي شيء إذا كانت جميع الحقول فارغة (لن يتم حفظها)
            if (string.IsNullOrEmpty(model.AffiliationEntity) && string.IsNullOrEmpty(model.Division) &&
                string.IsNullOrEmpty(model.Section) && string.IsNullOrEmpty(model.Group))
                return;

            var existing = await GetUserAffiliationInfoAsync(userId);

            int? affiliationEntityId = null;
            int? divisionId = null;
            int? sectionId = null;
            int? groupId = null;

            if (!string.IsNullOrEmpty(model.AffiliationEntity))
            {
                var entities = await _context.AffiliationEntities.ToListAsync();
                var foundEntity = entities.FirstOrDefault(e => e.Name == model.AffiliationEntity);
                affiliationEntityId = foundEntity?.Id;
            }

            if (!string.IsNullOrEmpty(model.Division) && affiliationEntityId.HasValue)
            {
                var divisions = await _context.Divisions
                    .Where(d => d.AffiliationEntityId == affiliationEntityId.Value)
                    .ToListAsync();
                var foundDivision = divisions.FirstOrDefault(d => d.Name == model.Division);
                divisionId = foundDivision?.Id;
            }

            if (!string.IsNullOrEmpty(model.Section) && divisionId.HasValue)
            {
                var sections = await _context.Sections
                    .Where(s => s.DivisionId == divisionId.Value)
                    .ToListAsync();
                var foundSection = sections.FirstOrDefault(s => s.Name == model.Section);
                sectionId = foundSection?.Id;
            }

            if (!string.IsNullOrEmpty(model.Group) && sectionId.HasValue)
            {
                var groups = await _context.Groups
                    .Where(g => g.SectionId == sectionId.Value)
                    .ToListAsync();
                var foundGroup = groups.FirstOrDefault(g => g.Name == model.Group);
                groupId = foundGroup?.Id;
            }

            if (existing != null)
            {
                // ✅ ✅ ✅ التعديل المهم: فقط قم بتحديث الحقول التي لها قيمة جديدة، ولا تقم بتعيينها إلى null
                if (affiliationEntityId.HasValue)
                    existing.AffiliationEntityId = affiliationEntityId;

                if (divisionId.HasValue)
                    existing.DivisionId = divisionId;

                if (sectionId.HasValue)
                    existing.SectionId = sectionId;

                if (groupId.HasValue)
                    existing.GroupId = groupId;

                // ✅ تحديث الحقول الأخرى فقط إذا كانت القيم الجديدة غير فارغة
                if (!string.IsNullOrEmpty(model.MozakeName))
                    existing.MozakeName = model.MozakeName;

                if (!string.IsNullOrEmpty(model.MozakePhoneNumber))
                    existing.MozakePhoneNumber = model.MozakePhoneNumber;

                existing.BadgeNumber = model.BadgeNumber ?? "";

                if (model.AffiliationDate.HasValue)
                    existing.AffiliationDate = model.AffiliationDate;

                _context.AffiliationInfos.Update(existing);
            }
            else if (affiliationEntityId.HasValue || !string.IsNullOrEmpty(model.MozakeName))
            {
                var affiliationInfo = new AffiliationInfo
                {
                    UserId = userId,
                    AffiliationEntityId = affiliationEntityId,
                    DivisionId = divisionId,
                    SectionId = sectionId,
                    GroupId = groupId,
                    MozakeName = model.MozakeName,
                    MozakePhoneNumber = model.MozakePhoneNumber,
                    BadgeNumber = model.BadgeNumber,
                    AffiliationDate = model.AffiliationDate
                };
                _context.AffiliationInfos.Add(affiliationInfo);
            }

            await _context.SaveChangesAsync();
        }

        private async Task AdminUpdateOrCreateUnion(string userId, MembershipViewModel model)
        {
            if (string.IsNullOrEmpty(model.UnionName)) return;
            var existing = await GetUserUnionAsync(userId);
            if (existing != null)
            {
                existing.UnionName = model.UnionName;
                existing.Position = model.UnionPosition;
                existing.IdNumber = model.UnionIdNumber;
                existing.AffiliationDate = model.UnionAffiliationDate;
                _context.UnionMemberships.Update(existing);
            }
            else
            {
                var union = new UnionMembership { UserId = userId, UnionName = model.UnionName, Position = model.UnionPosition, IdNumber = model.UnionIdNumber, AffiliationDate = model.UnionAffiliationDate };
                _context.UnionMemberships.Add(union);
            }
            await _context.SaveChangesAsync();
        }

        private async Task AdminUpdateOrCreateFederation(string userId, MembershipViewModel model)
        {
            if (string.IsNullOrEmpty(model.FederationName)) return;

            var existing = await GetUserFederationAsync(userId);

            string federationName = model.FederationName;
            string? divisionName = null;
            string? sectionName = null;
            string? groupName = null;

            var parts = model.FederationName.Split(new[] { " - " }, StringSplitOptions.None);
            if (parts.Length >= 1) federationName = parts[0];
            if (parts.Length >= 2) divisionName = parts[1];
            if (parts.Length >= 3) sectionName = parts[2];
            if (parts.Length >= 4) groupName = parts[3];

            int? federationId = null;
            int? divisionId = null;
            int? sectionId = null;
            int? groupId = null;

            if (!string.IsNullOrEmpty(federationName))
            {
                var federationMaster = await _context.Federations
                    .FirstOrDefaultAsync(f => f.Name == federationName);
                federationId = federationMaster?.Id;
            }

            if (federationId.HasValue && !string.IsNullOrEmpty(divisionName))
            {
                var divisions = await _context.FederationDivisions
                    .Where(d => d.FederationId == federationId.Value)
                    .ToListAsync();
                var division = divisions.FirstOrDefault(d => d.Name == divisionName);
                divisionId = division?.Id;
            }

            if (divisionId.HasValue && !string.IsNullOrEmpty(sectionName))
            {
                var sections = await _context.FederationSections
                    .Where(s => s.FederationDivisionId == divisionId.Value)
                    .ToListAsync();
                var section = sections.FirstOrDefault(s => s.Name == sectionName);
                sectionId = section?.Id;
            }

            if (sectionId.HasValue && !string.IsNullOrEmpty(groupName))
            {
                var groups = await _context.FederationGroups
                    .Where(g => g.FederationSectionId == sectionId.Value)
                    .ToListAsync();
                var group = groups.FirstOrDefault(g => g.Name == groupName);
                groupId = group?.Id;
            }

            if (existing != null)
            {
                existing.FederationId = federationId;
                existing.FederationDivisionId = divisionId;
                existing.FederationSectionId = sectionId;
                existing.FederationGroupId = groupId;
                existing.Position = model.FederationPosition;
                existing.IdNumber = model.FederationIdNumber;
                existing.AffiliationDate = model.FederationAffiliationDate;
                _context.FederationMemberships.Update(existing);
            }
            else if (federationId.HasValue)
            {
                var federation = new FederationMembership
                {
                    UserId = userId,
                    FederationId = federationId,
                    FederationDivisionId = divisionId,
                    FederationSectionId = sectionId,
                    FederationGroupId = groupId,
                    Position = model.FederationPosition,
                    IdNumber = model.FederationIdNumber,
                    AffiliationDate = model.FederationAffiliationDate
                };
                _context.FederationMemberships.Add(federation);
            }
            await _context.SaveChangesAsync();
        }
        /// <summary>
        /// تحديث بيانات اتحاد المستخدم باستخدام المعرفات المحفوظة (للسوبر أدمن)
        /// </summary>
        private async Task AdminUpdateOrCreateFederationWithIds(string userId, MembershipViewModel model)
        {
            // إذا كان الاسم فارغاً، لا تفعل شيئاً
            if (string.IsNullOrEmpty(model.FederationName))
                return;

            var existing = await GetUserFederationAsync(userId);

            // تفكيك الاسم الكامل للاتحاد إلى أجزاء
            string federationName = model.FederationName;
            string? divisionName = null;
            string? sectionName = null;
            string? groupName = null;

            var parts = model.FederationName.Split(new[] { " - " }, StringSplitOptions.None);
            if (parts.Length >= 1) federationName = parts[0];
            if (parts.Length >= 2) divisionName = parts[1];
            if (parts.Length >= 3) sectionName = parts[2];
            if (parts.Length >= 4) groupName = parts[3];

            int? federationId = null;
            int? divisionId = null;
            int? sectionId = null;
            int? groupId = null;

            // البحث عن الاتحاد الرئيسي
            if (!string.IsNullOrEmpty(federationName))
            {
                var federationMaster = await _context.Federations
                    .FirstOrDefaultAsync(f => f.Name == federationName);
                federationId = federationMaster?.Id;
            }

            // البحث عن قسم الاتحاد
            if (federationId.HasValue && !string.IsNullOrEmpty(divisionName))
            {
                var divisions = await _context.FederationDivisions
                    .Where(d => d.FederationId == federationId.Value)
                    .ToListAsync();
                var division = divisions.FirstOrDefault(d => d.Name == divisionName);
                divisionId = division?.Id;
            }

            // البحث عن شعبة الاتحاد
            if (divisionId.HasValue && !string.IsNullOrEmpty(sectionName))
            {
                var sections = await _context.FederationSections
                    .Where(s => s.FederationDivisionId == divisionId.Value)
                    .ToListAsync();
                var section = sections.FirstOrDefault(s => s.Name == sectionName);
                sectionId = section?.Id;
            }

            // البحث عن تجمع الاتحاد
            if (sectionId.HasValue && !string.IsNullOrEmpty(groupName))
            {
                var groups = await _context.FederationGroups
                    .Where(g => g.FederationSectionId == sectionId.Value)
                    .ToListAsync();
                var group = groups.FirstOrDefault(g => g.Name == groupName);
                groupId = group?.Id;
            }

            // حفظ أو تحديث البيانات
            if (existing != null)
            {
                existing.FederationId = federationId;
                existing.FederationDivisionId = divisionId;
                existing.FederationSectionId = sectionId;
                existing.FederationGroupId = groupId;
                existing.Position = model.FederationPosition;
                existing.IdNumber = model.FederationIdNumber;
                existing.AffiliationDate = model.FederationAffiliationDate;
                _context.FederationMemberships.Update(existing);
            }
            else if (federationId.HasValue)
            {
                var federation = new FederationMembership
                {
                    UserId = userId,
                    FederationId = federationId,
                    FederationDivisionId = divisionId,
                    FederationSectionId = sectionId,
                    FederationGroupId = groupId,
                    Position = model.FederationPosition,
                    IdNumber = model.FederationIdNumber,
                    AffiliationDate = model.FederationAffiliationDate
                };
                _context.FederationMemberships.Add(federation);
            }

            await _context.SaveChangesAsync();
        }

        private async Task AdminUpdateOrCreateAssociation(string userId, MembershipViewModel model)
        {
            if (string.IsNullOrEmpty(model.AssociationName)) return;
            var existing = await GetUserAssociationAsync(userId);
            if (existing != null)
            {
                existing.AssociationName = model.AssociationName;
                existing.Position = model.AssociationPosition;
                existing.IdNumber = model.AssociationIdNumber;
                existing.AffiliationDate = model.AssociationAffiliationDate;
                _context.AssociationMemberships.Update(existing);
            }
            else
            {
                var association = new AssociationMembership { UserId = userId, AssociationName = model.AssociationName, Position = model.AssociationPosition, IdNumber = model.AssociationIdNumber, AffiliationDate = model.AssociationAffiliationDate };
                _context.AssociationMemberships.Add(association);
            }
            await _context.SaveChangesAsync();
        }

        private async Task AdminUpdateOrCreateNgo(string userId, MembershipViewModel model)
        {
            if (string.IsNullOrEmpty(model.NgoName)) return;
            var existing = await GetUserNgoAsync(userId);
            if (existing != null)
            {
                existing.NgoName = model.NgoName;
                existing.Position = model.NgoPosition;
                existing.IdNumber = model.NgoIdNumber;
                existing.AffiliationDate = model.NgoAffiliationDate;
                _context.NgoMemberships.Update(existing);
            }
            else
            {
                var ngo = new NgoMembership { UserId = userId, NgoName = model.NgoName, Position = model.NgoPosition, IdNumber = model.NgoIdNumber, AffiliationDate = model.NgoAffiliationDate };
                _context.NgoMemberships.Add(ngo);
            }
            await _context.SaveChangesAsync();
        }
        private async Task UpdateOrCreateAffiliationInfoWithIds(string userId, AffiliationViewModel model,
     int? entityId, int? divisionId, int? sectionId, int? groupId)
        {
            // ✅ نفس المنطق المستخدم في RegisterController
            if (string.IsNullOrEmpty(model.AffiliationEntity) && string.IsNullOrEmpty(model.Division) &&
                string.IsNullOrEmpty(model.Section) && string.IsNullOrEmpty(model.Group))
                return;

            var existing = await GetUserAffiliationInfoAsync(userId);

            int? affiliationEntityId = null;
            int? finalDivisionId = null;
            int? finalSectionId = null;
            int? finalGroupId = null;

            // ✅ البحث عن المعرفات باستخدام الأسماء
            if (!string.IsNullOrEmpty(model.AffiliationEntity))
            {
                var entities = await _context.AffiliationEntities.ToListAsync();
                var foundEntity = entities.FirstOrDefault(e => e.Name == model.AffiliationEntity);
                affiliationEntityId = foundEntity?.Id;
            }

            if (!string.IsNullOrEmpty(model.Division) && affiliationEntityId.HasValue)
            {
                var divisions = await _context.Divisions
                    .Where(d => d.AffiliationEntityId == affiliationEntityId.Value)
                    .ToListAsync();
                var foundDivision = divisions.FirstOrDefault(d => d.Name == model.Division);
                finalDivisionId = foundDivision?.Id;
            }

            if (!string.IsNullOrEmpty(model.Section) && finalDivisionId.HasValue)
            {
                var sections = await _context.Sections
                    .Where(s => s.DivisionId == finalDivisionId.Value)
                    .ToListAsync();
                var foundSection = sections.FirstOrDefault(s => s.Name == model.Section);
                finalSectionId = foundSection?.Id;
            }

            // ✅ جعل التجمع اختيارياً (قد لا يكون موجوداً لبعض جهات الانتساب)
            if (!string.IsNullOrEmpty(model.Group) && finalSectionId.HasValue)
            {
                var groups = await _context.Groups
                    .Where(g => g.SectionId == finalSectionId.Value)
                    .ToListAsync();
                var foundGroup = groups.FirstOrDefault(g => g.Name == model.Group);
                finalGroupId = foundGroup?.Id;
            }

            if (existing != null)
            {
                existing.AffiliationEntityId = affiliationEntityId;
                existing.DivisionId = finalDivisionId;
                existing.SectionId = finalSectionId;
                existing.GroupId = finalGroupId;
                existing.MozakeName = model.MozakeName;
                existing.MozakePhoneNumber = model.MozakePhoneNumber;
                existing.BadgeNumber = model.BadgeNumber;
                existing.AffiliationDate = model.AffiliationDate;
                _context.AffiliationInfos.Update(existing);
            }
            else
            {
                var affiliationInfo = new AffiliationInfo
                {
                    UserId = userId,
                    AffiliationEntityId = affiliationEntityId,
                    DivisionId = finalDivisionId,
                    SectionId = finalSectionId,
                    GroupId = finalGroupId,
                    MozakeName = model.MozakeName,
                    MozakePhoneNumber = model.MozakePhoneNumber,
                    BadgeNumber = model.BadgeNumber,
                    AffiliationDate = model.AffiliationDate
                };
                _context.AffiliationInfos.Add(affiliationInfo);
            }

            await _context.SaveChangesAsync();
        }

        // ========== قوائم البيانات ==========
        private List<string> GetGovernorates()
        {
            return new List<string> { "بغداد", "الأنبار", "بابل", "البصرة", "ذي قار", "القادسية", "ديالى", "دهوك", "أربيل", "كربلاء", "كركوك", "ميسان", "المثنى", "النجف", "نينوى", "صلاح الدين", "السليمانية", "واسط" };
        }

        private List<string> GetGenders() => new List<string> { "ذكر", "أنثى" };

        private List<string> GetEducations() => new List<string> { "آمي", "ابتدائي", "متوسط", "إعدادي", "معهد", "طالب جامعي", "دبلوم", "بكالوريوس", "ماجستير", "دكتوراه" };

        private List<string> GetMinistries() => IraqiGovernmentEntities.GetMinistries();

        private List<string> GetEmploymentStatuses() => new List<string> { "موظف", "كاسب", "متقاعد", "طالب", "قطاع خاص" };

        private List<string> GetStudyStages()
        {
            return new List<string>
            {
                "المرحلة الأولى",
                "المرحلة الثانية",
                "المرحلة الثالثة",
                "المرحلة الرابعة",
                "المرحلة الخامسة",
                "المرحلة السادسة"
            };
        }
        private List<string> GetJobGrades()
        {
            return new List<string>
    {
        "الدرجة العاشرة",
        "الدرجة التاسعة",
        "الدرجة الثامنة",
        "الدرجة السابعة",
        "الدرجة السادسة",
        "الدرجة الخامسة",
        "الدرجة الرابعة",
        "الدرجة الثالثة",
        "الدرجة الثانية",
        "الدرجة الأولى"
    };
        }
        private async Task<List<string>> GetDistinctAffiliationEntitiesAsync()
        {
            var entities = await _context.AffiliationEntities.ToListAsync();
            return entities.Select(e => e.Name).OrderBy(x => x).ToList();
        }

        private async Task<List<string>> GetDistinctDivisionsAsync()
        {
            var divisions = await _context.Divisions.ToListAsync();
            return divisions.Select(d => d.Name).Distinct().OrderBy(x => x).ToList();
        }

        private async Task<List<string>> GetDistinctSectionsAsync()
        {
            var sections = await _context.Sections.ToListAsync();
            return sections.Select(s => s.Name).Distinct().OrderBy(x => x).ToList();
        }

        private async Task<List<string>> GetDistinctGroupsAsync()
        {
            var groups = await _context.Groups.ToListAsync();
            return groups.Select(g => g.Name).Distinct().OrderBy(x => x).ToList();
        }

        private async Task<List<string>> GetDistinctUnionsAsync()
        {
            var unions = await _context.Unions.ToListAsync();
            return unions.Select(x => x.Name).OrderBy(x => x).ToList();
        }

        private async Task<List<string>> GetDistinctFederationsAsync()
        {
            var federations = await _context.Federations.ToListAsync();
            return federations.Select(x => x.Name).OrderBy(x => x).ToList();
        }

        private async Task<List<string>> GetDistinctAssociationsAsync()
        {
            var associations = await _context.Associations.ToListAsync();
            return associations.Select(x => x.Name).OrderBy(x => x).ToList();
        }

        private async Task<List<string>> GetDistinctNgosAsync()
        {
            var ngos = await _context.Ngos.ToListAsync();
            return ngos.Select(x => x.Name).OrderBy(x => x).ToList();
        }
        private async Task<List<string>> GetDistinctFederationDivisionsAsync()
        {
            var divisions = await _context.FederationDivisions.ToListAsync();
            return divisions.Select(d => d.Name).Distinct().OrderBy(x => x).ToList();
        }

        private async Task<List<string>> GetDistinctFederationSectionsAsync()
        {
            var sections = await _context.FederationSections.ToListAsync();
            return sections.Select(s => s.Name).Distinct().OrderBy(x => x).ToList();
        }

        private async Task<List<string>> GetDistinctFederationGroupsAsync()
        {
            var groups = await _context.FederationGroups.ToListAsync();
            return groups.Select(g => g.Name).Distinct().OrderBy(x => x).ToList();
        }

        // ========== دوال AJAX للـ Cascading Dropdown ==========
        [HttpGet]
        public async Task<IActionResult> GetDivisionsByEntityName(string entityName)
        {
            try
            {
                if (string.IsNullOrEmpty(entityName))
                    return Json(new { success = false, data = new List<object>() });

                var entities = await _context.AffiliationEntities.ToListAsync();
                var entity = entities.FirstOrDefault(e => e.Name == entityName);
                if (entity == null)
                    return Json(new { success = false, data = new List<object>() });

                var divisions = await _context.Divisions
                    .Where(d => d.AffiliationEntityId == entity.Id)
                    .Select(d => new { id = d.Id, name = d.Name })
                    .ToListAsync();
                return Json(new { success = true, data = divisions });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, data = new List<object>(), message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetSectionsByDivisionName(string divisionName)
        {
            try
            {
                if (string.IsNullOrEmpty(divisionName))
                    return Json(new { success = false, data = new List<object>() });

                var divisions = await _context.Divisions.ToListAsync();
                var division = divisions.FirstOrDefault(d => d.Name == divisionName);
                if (division == null)
                    return Json(new { success = false, data = new List<object>() });

                var sections = await _context.Sections
                    .Where(s => s.DivisionId == division.Id)
                    .Select(s => new { id = s.Id, name = s.Name })
                    .ToListAsync();
                return Json(new { success = true, data = sections });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, data = new List<object>(), message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetGroupsBySectionName(string sectionName)
        {
            try
            {
                if (string.IsNullOrEmpty(sectionName))
                    return Json(new { success = false, data = new List<object>() });

                var sections = await _context.Sections.ToListAsync();
                var section = sections.FirstOrDefault(s => s.Name == sectionName);
                if (section == null)
                    return Json(new { success = false, data = new List<object>() });

                var groups = await _context.Groups
                    .Where(g => g.SectionId == section.Id)
                    .Select(g => new { id = g.Id, name = g.Name })
                    .ToListAsync();
                return Json(new { success = true, data = groups });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, data = new List<object>(), message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Reports()
        {
            var cards = await BuildReportCardsAsync();
            var reportUserIds = await GetReportEligibleUserIdsAsync();

            return View(new SuperAdminReportsViewModel
            {
                Cards = cards,
                TotalUsers = reportUserIds.Count
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GetReportsExportCount([FromBody] ReportSelectionRequest request)
        {
            var userIds = await GetReportUserIdsAsync(request.Filters);
            return Json(new { success = true, count = userIds.Count });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PrepareSendNotificationFromReports(string selectedFilters)
        {
            try
            {
                var filters = DeserializeReportFilters(selectedFilters);

                var userIds = await GetReportUserIdsAsync(filters);
                if (!userIds.Any())
                {
                    TempData["WarningMessage"] = "⚠️ لا توجد بيانات مطابقة للفلاتر المحددة لإرسال الإشعار.";
                    return RedirectToAction(nameof(Reports));
                }

                SaveReportNotificationFilters(selectedFilters);
                return RedirectToAction(nameof(SendNotification), new { audience = "reports" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في تجهيز مستلمي الإشعارات من التقارير");
                TempData["ErrorMessage"] = $"❌ حدث خطأ أثناء تجهيز المستلمين: {ex.Message}";
                return RedirectToAction(nameof(Reports));
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetReportFilterOptions(string key)
        {
            var options = await GetReportFilterOptionsAsync(key);
            return Json(new { success = true, data = options });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportReportsToExcel(string selectedFilters)
        {
            try
            {
                var filters = JsonSerializer.Deserialize<List<ReportFilterSelection>>(
                    string.IsNullOrWhiteSpace(selectedFilters) ? "[]" : selectedFilters,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<ReportFilterSelection>();

                var userIds = await GetReportUserIdsAsync(filters);
                if (!userIds.Any())
                {
                    TempData["WarningMessage"] = "⚠️ لا توجد بيانات مطابقة للتقارير المحددة";
                    return RedirectToAction(nameof(Reports));
                }

                var users = await _userManager.Users
                    .Where(u => userIds.Contains(u.Id))
                    .OrderBy(u => u.Email)
                    .ToListAsync();

                var data = await BuildFullUsersExcelData(users);
                byte[] fileContent = GenerateExcelFile(data);
                return File(fileContent, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Reports_Export_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في تصدير تقارير Excel");
                TempData["ErrorMessage"] = $"❌ حدث خطأ: {ex.Message}";
                return RedirectToAction(nameof(Reports));
            }
        }

        private async Task<List<ReportCardOptionVM>> BuildReportCardsAsync()
        {
            var definitions = GetReportCardDefinitions();
            var cards = new List<ReportCardOptionVM>();

            foreach (var definition in definitions)
            {
                cards.Add(new ReportCardOptionVM
                {
                    Key = definition.Key,
                    Title = definition.Title,
                    Description = definition.Description,
                    Icon = definition.Icon,
                    Color = definition.Color
                });
            }

            return cards;
        }

        private static List<ReportCardOptionVM> GetReportCardDefinitions()
        {
            return new List<ReportCardOptionVM>
            {
                new() { Key = "governorate", Title = "محافظة العمل التنظيمي", Description = "محافظة العمل التنظيمي الرئيسية، ومع بغداد يشمل القضاء", Icon = "bi-geo-alt-fill", Color = "primary" },
                new() { Key = "residenceGovernorate", Title = "محافظة السكن", Description = "فلترة المستخدمين حسب محافظة السكن", Icon = "bi-house-door-fill", Color = "success" },
                new() { Key = "gender", Title = "الجنس", Description = "فلترة المستخدمين حسب ذكر أو أنثى", Icon = "bi-gender-ambiguous", Color = "info" },
                new() { Key = "education", Title = "التحصيل الدراسي", Description = "المستخدمون الذين لديهم تحصيل دراسي", Icon = "bi-mortarboard-fill", Color = "success" },
                new() { Key = "studyStage", Title = "المرحلة الدراسية", Description = "فلترة الطلبة حسب المرحلة الدراسية", Icon = "bi-layers-fill", Color = "warning" },
                new() { Key = "work", Title = "الحالة الوظيفية", Description = "فلترة حسب موظف أو متقاعد أو طالب وغيرها", Icon = "bi-briefcase-fill", Color = "info" },
                new() { Key = "ministry", Title = "الوزارة", Description = "فلترة الموظفين حسب الوزارة", Icon = "bi-bank2", Color = "primary" },
                new() { Key = "birthDate", Title = "تاريخ الميلاد", Description = "المستخدمون الذين لديهم تاريخ ميلاد", Icon = "bi-calendar-date-fill", Color = "warning" },
                new() { Key = "affiliationDate", Title = "تاريخ الانتماء للجهة", Description = "فلترة تاريخ الانتماء الخاص بجهة الانتساب", Icon = "bi-calendar-check-fill", Color = "danger" },
                new() { Key = "affiliationEntity", Title = "الجهة", Description = "جهة الانتساب في الاستمارة", Icon = "bi-building-fill", Color = "primary" },
                new() { Key = "division", Title = "القسم", Description = "قسم الانتساب", Icon = "bi-diagram-3-fill", Color = "secondary" },
                new() { Key = "section", Title = "الشعبة", Description = "شعبة الانتساب", Icon = "bi-diagram-2-fill", Color = "success" },
                new() { Key = "group", Title = "الوحدة", Description = "وحدة الانتساب", Icon = "bi-grid-3x3-gap-fill", Color = "danger" },
                new() { Key = "federation", Title = "الاتحاد", Description = "اسم الاتحاد", Icon = "bi-people-fill", Color = "info" },
                new() { Key = "federationDivision", Title = "قسم الاتحاد", Description = "القسم داخل الاتحاد", Icon = "bi-diagram-3", Color = "primary" },
                new() { Key = "federationSection", Title = "شعبة الاتحاد", Description = "الشعبة داخل الاتحاد", Icon = "bi-diagram-2", Color = "success" },
                new() { Key = "federationGroup", Title = "وحدة الاتحاد", Description = "الوحدة داخل الاتحاد", Icon = "bi-grid-fill", Color = "warning" },
                new() { Key = "union", Title = "النقابة", Description = "بيانات النقابة", Icon = "bi-person-vcard-fill", Color = "secondary" },
                new() { Key = "association", Title = "الجمعية", Description = "بيانات الجمعية", Icon = "bi-collection-fill", Color = "danger" },
                new() { Key = "ngo", Title = "المنظمة", Description = "بيانات المنظمة", Icon = "bi-globe2", Color = "info" }
            };
        }

        private async Task<HashSet<string>> GetReportEligibleUserIdsAsync()
        {
            var roleUserIds = await _context.UserRoles
                .Join(
                    _context.Roles,
                    userRole => userRole.RoleId,
                    role => role.Id,
                    (userRole, role) => new { userRole.UserId, RoleName = role.Name ?? string.Empty })
                .Where(x => ReportIncludedRoleNames.Contains(x.RoleName))
                .Select(x => x.UserId)
                .Distinct()
                .ToListAsync();

            var individualUserIds = await _context.Identifies.AsNoTracking()
                .Where(i => i.AccountType == "فرد" || i.IsPromoted)
                .Select(i => i.UserId)
                .Distinct()
                .ToListAsync();

            var assignmentUserIds = await _context.ManagementAssignments.AsNoTracking()
                .Select(x => x.UserId)
                .Distinct()
                .ToListAsync();

            return roleUserIds
                .Concat(individualUserIds)
                .Concat(assignmentUserIds)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private async Task<List<ReportFilterOptionVM>> GetReportFilterOptionsAsync(string key)
        {
            var values = new List<string>();
            var reportUserIds = await GetReportEligibleUserIdsAsync();

            if (!reportUserIds.Any())
                return new List<ReportFilterOptionVM>();

            switch (key)
            {
                case "governorate":
                    var workLocations = await _context.Identifies.AsNoTracking()
                        .Include(i => i.WorkLocation)
                        .Where(i => reportUserIds.Contains(i.UserId))
                        .ToListAsync();

                    values = workLocations
                        .Select(i => FormatWorkGovernorateOption(
                            !string.IsNullOrWhiteSpace(i.WorkLocation?.Governorate) ? i.WorkLocation.Governorate : i.WorkGovernorate,
                            !string.IsNullOrWhiteSpace(i.WorkLocation?.District) ? i.WorkLocation.District : i.WorkDistrict))
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .ToList();

                    if (workLocations.Any(i =>
                        string.Equals(
                            !string.IsNullOrWhiteSpace(i.WorkLocation?.Governorate) ? i.WorkLocation.Governorate : i.WorkGovernorate,
                            "بغداد",
                            StringComparison.OrdinalIgnoreCase)))
                    {
                        values.Add("بغداد عامة");
                    }
                    break;

                case "residenceGovernorate":
                    values = await _context.Addresses.AsNoTracking()
                        .Where(a => reportUserIds.Contains(a.UserId))
                        .Select(a => a.Governorate)
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Select(v => v!)
                        .ToListAsync();
                    break;

                case "gender":
                    values = await _context.Identifies.AsNoTracking()
                        .Where(i => reportUserIds.Contains(i.UserId))
                        .Select(i => i.Gender)
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Select(v => v!)
                        .ToListAsync();
                    break;

                case "education":
                    values = await _context.Identifies.AsNoTracking()
                        .Where(i => reportUserIds.Contains(i.UserId))
                        .Select(i => i.Education)
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Select(v => v!)
                        .ToListAsync();
                    break;

                case "studyStage":
                    values = await _context.Identifies.AsNoTracking()
                        .Where(i => reportUserIds.Contains(i.UserId))
                        .Select(i => i.StudyStage)
                        .Where(v => !string.IsNullOrWhiteSpace(v) && v != "---")
                        .Select(v => v!)
                        .ToListAsync();
                    break;

                case "work":
                    values = await _context.Identifies.AsNoTracking()
                        .Where(i => reportUserIds.Contains(i.UserId))
                        .Select(i => i.EmploymentStatus)
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Select(v => v!)
                        .ToListAsync();
                    break;

                case "ministry":
                    values = await _context.Identifies.AsNoTracking()
                        .Where(i => reportUserIds.Contains(i.UserId) && i.EmploymentStatus == "موظف")
                        .Select(i => i.Ministry)
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Select(v => v!)
                        .ToListAsync();
                    break;

                case "birthDate":
                    values = await _context.Identifies.AsNoTracking()
                        .Where(i => reportUserIds.Contains(i.UserId) && i.Date.Year > 1900)
                        .Select(i => i.Date.Year.ToString())
                        .ToListAsync();
                    break;

                case "affiliationDate":
                    values = await _context.AffiliationInfos.AsNoTracking()
                        .Where(a => reportUserIds.Contains(a.UserId) && a.AffiliationDate.HasValue)
                        .Select(a => a.AffiliationDate!.Value.Year.ToString())
                        .ToListAsync();
                    break;

                case "affiliationEntity":
                    values = await _context.AffiliationInfos.AsNoTracking()
                        .Include(a => a.AffiliationEntity)
                        .Where(a => reportUserIds.Contains(a.UserId))
                        .Select(a => a.AffiliationEntity != null ? a.AffiliationEntity.Name : null)
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Select(v => v!)
                        .ToListAsync();
                    break;

                case "division":
                    values = await _context.AffiliationInfos.AsNoTracking()
                        .Include(a => a.Division)
                        .Where(a => reportUserIds.Contains(a.UserId))
                        .Select(a => a.Division != null ? a.Division.Name : null)
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Select(v => v!)
                        .ToListAsync();
                    break;

                case "section":
                    values = await _context.AffiliationInfos.AsNoTracking()
                        .Include(a => a.Section)
                        .Where(a => reportUserIds.Contains(a.UserId))
                        .Select(a => a.Section != null ? a.Section.Name : null)
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Select(v => v!)
                        .ToListAsync();
                    break;

                case "group":
                    values = await _context.AffiliationInfos.AsNoTracking()
                        .Include(a => a.Group)
                        .Where(a => reportUserIds.Contains(a.UserId))
                        .Select(a => a.Group != null ? a.Group.Name : null)
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Select(v => v!)
                        .ToListAsync();
                    break;

                case "federation":
                    values = await _context.FederationMemberships.AsNoTracking()
                        .Include(f => f.Federation)
                        .Where(f => reportUserIds.Contains(f.UserId))
                        .Select(f => f.Federation != null ? f.Federation.Name : null)
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Select(v => v!)
                        .ToListAsync();
                    break;

                case "federationDivision":
                    values = await _context.FederationMemberships.AsNoTracking()
                        .Include(f => f.FederationDivision)
                        .Where(f => reportUserIds.Contains(f.UserId))
                        .Select(f => f.FederationDivision != null ? f.FederationDivision.Name : null)
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Select(v => v!)
                        .ToListAsync();
                    break;

                case "federationSection":
                    values = await _context.FederationMemberships.AsNoTracking()
                        .Include(f => f.FederationSection)
                        .Where(f => reportUserIds.Contains(f.UserId))
                        .Select(f => f.FederationSection != null ? f.FederationSection.Name : null)
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Select(v => v!)
                        .ToListAsync();
                    break;

                case "federationGroup":
                    values = await _context.FederationMemberships.AsNoTracking()
                        .Include(f => f.FederationGroup)
                        .Where(f => reportUserIds.Contains(f.UserId))
                        .Select(f => f.FederationGroup != null ? f.FederationGroup.Name : null)
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Select(v => v!)
                        .ToListAsync();
                    break;

                case "union":
                    values = await _context.UnionMemberships.AsNoTracking()
                        .Where(u => reportUserIds.Contains(u.UserId))
                        .Select(u => u.UnionName)
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Select(v => v!)
                        .ToListAsync();
                    break;

                case "association":
                    values = await _context.AssociationMemberships.AsNoTracking()
                        .Where(a => reportUserIds.Contains(a.UserId))
                        .Select(a => a.AssociationName)
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Select(v => v!)
                        .ToListAsync();
                    break;

                case "ngo":
                    values = await _context.NgoMemberships.AsNoTracking()
                        .Where(n => reportUserIds.Contains(n.UserId))
                        .Select(n => n.NgoName)
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Select(v => v!)
                        .ToListAsync();
                    break;
            }

            return values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v, StringComparer.Create(new System.Globalization.CultureInfo("ar-IQ"), false))
                .Select(v => new ReportFilterOptionVM { Value = v, Label = v })
                .ToList();
        }

        private async Task<HashSet<string>> GetReportUserIdsAsync(IEnumerable<ReportFilterSelection>? filters)
        {
            var reportUserIds = await GetReportEligibleUserIdsAsync();
            if (!reportUserIds.Any())
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var selectedFilters = (filters ?? Enumerable.Empty<ReportFilterSelection>())
                .Where(f => !string.IsNullOrWhiteSpace(f.Key))
                .Select(f => new ReportFilterSelection
                {
                    Key = f.Key.Trim(),
                    Values = (f.Values ?? new List<string>())
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Select(v => v.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList()
                })
                .ToList();

            var identifies = await _context.Identifies
                .AsNoTracking()
                .Include(i => i.WorkLocation)
                .Where(i => reportUserIds.Contains(i.UserId))
                .ToListAsync();

            if (!selectedFilters.Any())
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var userIds = identifies.Select(i => i.UserId).ToHashSet();

            var addresses = await _context.Addresses.AsNoTracking()
                .Where(a => userIds.Contains(a.UserId))
                .GroupBy(a => a.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.First());

            var affiliations = await _context.AffiliationInfos.AsNoTracking()
                .Include(a => a.AffiliationEntity)
                .Include(a => a.Division)
                .Include(a => a.Section)
                .Include(a => a.Group)
                .Where(a => userIds.Contains(a.UserId))
                .GroupBy(a => a.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.First());

            var federations = await _context.FederationMemberships.AsNoTracking()
                .Include(f => f.Federation)
                .Include(f => f.FederationDivision)
                .Include(f => f.FederationSection)
                .Include(f => f.FederationGroup)
                .Where(f => userIds.Contains(f.UserId))
                .GroupBy(f => f.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.First());

            var unions = await _context.UnionMemberships.AsNoTracking()
                .Where(u => userIds.Contains(u.UserId))
                .GroupBy(u => u.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.First());

            var associations = await _context.AssociationMemberships.AsNoTracking()
                .Where(a => userIds.Contains(a.UserId))
                .GroupBy(a => a.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.First());

            var ngos = await _context.NgoMemberships.AsNoTracking()
                .Where(n => userIds.Contains(n.UserId))
                .GroupBy(n => n.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.First());

            string? GetReportValue(Identify identify, string key)
            {
                affiliations.TryGetValue(identify.UserId, out var affiliation);
                federations.TryGetValue(identify.UserId, out var federation);
                unions.TryGetValue(identify.UserId, out var union);
                associations.TryGetValue(identify.UserId, out var association);
                ngos.TryGetValue(identify.UserId, out var ngo);
                addresses.TryGetValue(identify.UserId, out var address);

                return key switch
                {
                    "governorate" => FormatWorkGovernorateOption(
                        !string.IsNullOrWhiteSpace(identify.WorkLocation?.Governorate) ? identify.WorkLocation.Governorate : identify.WorkGovernorate,
                        !string.IsNullOrWhiteSpace(identify.WorkLocation?.District) ? identify.WorkLocation.District : identify.WorkDistrict),
                    "residenceGovernorate" => address?.Governorate,
                    "gender" => identify.Gender,
                    "education" => identify.Education,
                    "studyStage" => identify.StudyStage,
                    "work" => identify.EmploymentStatus,
                    "ministry" => identify.EmploymentStatus == "موظف" ? identify.Ministry : null,
                    "birthDate" => identify.Date.Year > 1900 ? identify.Date.ToString("yyyy-MM-dd") : null,
                    "affiliationDate" => affiliation?.AffiliationDate?.ToString("yyyy-MM-dd") ?? identify.AffiliationDate?.ToString("yyyy-MM-dd"),
                    "affiliationEntity" => affiliation?.AffiliationEntity?.Name,
                    "division" => affiliation?.Division?.Name,
                    "section" => affiliation?.Section?.Name,
                    "group" => affiliation?.Group?.Name,
                    "federation" => federation?.Federation?.Name,
                    "federationDivision" => federation?.FederationDivision?.Name,
                    "federationSection" => federation?.FederationSection?.Name,
                    "federationGroup" => federation?.FederationGroup?.Name,
                    "union" => union?.UnionName,
                    "association" => association?.AssociationName,
                    "ngo" => ngo?.NgoName,
                    _ => null
                };
            }

            return identifies
                .Where(identify => selectedFilters.All(filter =>
                {
                    var value = GetReportValue(identify, filter.Key);
                    if (string.IsNullOrWhiteSpace(value))
                        return false;

                    return !filter.Values.Any() ||
                           filter.Values.Contains("__all", StringComparer.OrdinalIgnoreCase) ||
                           (IsDateReportKey(filter.Key)
                               ? DateReportValueMatches(value, filter.Values)
                               : filter.Values.Any(selected => ReportValueMatches(filter.Key, value, selected)));
                }))
                .Select(i => i.UserId)
                .ToHashSet();
        }

        private void SaveReportNotificationFilters(string? selectedFilters)
        {
            var normalizedFilters = string.IsNullOrWhiteSpace(selectedFilters)
                ? "[]"
                : selectedFilters.Trim();

            HttpContext.Session.SetString(ReportNotificationFiltersSessionKey, normalizedFilters);
        }

        private string? GetSavedReportNotificationFilters()
        {
            return HttpContext.Session.GetString(ReportNotificationFiltersSessionKey);
        }

        private void ClearSavedReportNotificationRecipients()
        {
            HttpContext.Session.Remove(ReportNotificationFiltersSessionKey);
        }

        private static List<ReportFilterSelection> DeserializeReportFilters(string? selectedFilters)
        {
            return JsonSerializer.Deserialize<List<ReportFilterSelection>>(
                string.IsNullOrWhiteSpace(selectedFilters) ? "[]" : selectedFilters,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<ReportFilterSelection>();
        }

        private static bool IsDateReportKey(string key)
        {
            return key == "birthDate" || key == "affiliationDate";
        }

        private static bool DateReportValueMatches(string value, IEnumerable<string> selectedValues)
        {
            if (!DateTime.TryParse(value, out var date))
                return false;

            var parts = selectedValues
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v.Trim())
                .ToList();

            if (!parts.Any() || parts.Contains("__all", StringComparer.OrdinalIgnoreCase))
                return true;

            foreach (var part in parts)
            {
                var separatorIndex = part.IndexOf(':');
                if (separatorIndex <= 0 || separatorIndex == part.Length - 1)
                    return false;

                var type = part[..separatorIndex];
                var rawValue = part[(separatorIndex + 1)..];

                if (type == "date")
                {
                    if (!DateTime.TryParse(rawValue, out var selectedDate) || date.Date != selectedDate.Date)
                        return false;
                }
                else if (type == "year")
                {
                    if (!int.TryParse(rawValue, out var year) || date.Year != year)
                        return false;
                }
                else if (type == "month")
                {
                    if (!int.TryParse(rawValue, out var month) || date.Month != month)
                        return false;
                }
                else if (type == "day")
                {
                    if (!int.TryParse(rawValue, out var day) || date.Day != day)
                        return false;
                }
            }

            return true;
        }

        private static bool ReportValueMatches(string key, string value, string selectedValue)
        {
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(selectedValue))
                return false;

            var normalizedValue = value.Trim();
            var normalizedSelectedValue = selectedValue.Trim();

            if (key == "governorate" &&
                string.Equals(normalizedSelectedValue, "بغداد عامة", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(normalizedValue, "بغداد عامة", StringComparison.OrdinalIgnoreCase) ||
                       normalizedValue.StartsWith("بغداد - ", StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(normalizedValue, normalizedSelectedValue, StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatWorkGovernorateOption(string? governorate, string? district)
        {
            if (string.IsNullOrWhiteSpace(governorate))
                return string.Empty;

            if (governorate != "بغداد")
                return governorate.Trim();

            return string.IsNullOrWhiteSpace(district)
                ? "بغداد عامة"
                : $"بغداد - {district.Trim()}";
        }

        // ========== 1. تصدير جميع المستخدمين (كامل التفاصيل) ==========
        [HttpGet]
        public async Task<IActionResult> ExportAllUsersToExcel()
        {
            try
            {
                var users = await _userManager.Users.OrderBy(u => u.Email).ToListAsync();
                var data = await BuildFullUsersExcelData(users);
                byte[] fileContent = GenerateExcelFile(data);
                return File(fileContent, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"All_Users_Export_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في تصدير Excel لجميع المستخدمين");
                TempData["ErrorMessage"] = $"❌ حدث خطأ: {ex.Message}";
                return RedirectToAction(nameof(Users));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportSelectedUsersToExcel([FromBody] BulkDeleteRequest request)
        {
            try
            {
                if (request?.UserIds == null || !request.UserIds.Any())
                    return BadRequest(new { success = false, message = "الرجاء تحديد مستخدم واحد على الأقل للتصدير" });

                var userIds = request.UserIds
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct()
                    .ToList();

                var users = await _userManager.Users
                    .Where(u => userIds.Contains(u.Id))
                    .OrderBy(u => u.Email)
                    .ToListAsync();

                if (!users.Any())
                    return BadRequest(new { success = false, message = "لم يتم العثور على مستخدمين صالحين للتصدير" });

                var data = await BuildFullUsersExcelData(users);
                byte[] fileContent = GenerateExcelFile(data);
                return File(fileContent, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Selected_Users_Export_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في تصدير Excel للمستخدمين المحددين");
                return StatusCode(500, new { success = false, message = $"❌ حدث خطأ: {ex.Message}" });
            }
        }

        // ========== 2. تصدير الأفراد فقط (كامل التفاصيل) ==========
        [HttpGet]
        public async Task<IActionResult> ExportMembersOnlyToExcel()
        {
            try
            {
                var allIdentifies = await _context.Identifies.ToListAsync();
                var memberUserIds = allIdentifies
                    .Where(i => !string.IsNullOrWhiteSpace(i.Education) &&
                                (i.AccountType == "فرد" || i.IsPromoted == true))
                    .Select(i => i.UserId)
                    .ToList();

                var users = await _userManager.Users
                    .Where(u => memberUserIds.Contains(u.Id))
                    .OrderBy(u => u.Email)
                    .ToListAsync();

                var data = await BuildFullUsersExcelData(users);
                byte[] fileContent = GenerateExcelFile(data);
                return File(fileContent, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Members_Only_Export_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في تصدير Excel للأفراد");
                TempData["ErrorMessage"] = $"❌ حدث خطأ: {ex.Message}";
                return RedirectToAction(nameof(Members));
            }
        }

        // ========== 3. تصدير المسؤولين (Admin + SuperAdmin) فقط (كامل التفاصيل) ==========
        [HttpGet]
        public async Task<IActionResult> ExportAdminsOnlyToExcel()
        {
            try
            {
                var allowedRoles = new List<string> { clsRoles.Admin, clsRoles.SuperAdmin };
                if (CanCurrentUserManageSystemManager())
                {
                    allowedRoles.Add(clsRoles.SystemManager);
                }

                var adminUserIds = await _context.UserRoles
                    .AsNoTracking()
                    .Join(
                        _context.Roles.AsNoTracking(),
                        userRole => userRole.RoleId,
                        roleRow => roleRow.Id,
                        (userRole, roleRow) => new
                        {
                            userRole.UserId,
                            RoleName = roleRow.Name ?? string.Empty
                        })
                    .Where(x => allowedRoles.Contains(x.RoleName))
                    .Select(x => x.UserId)
                    .Distinct()
                    .ToListAsync();

                var adminUsers = await _userManager.Users
                    .Where(u => adminUserIds.Contains(u.Id))
                    .OrderBy(u => u.Email)
                    .ToListAsync();

                var data = await BuildFullUsersExcelData(adminUsers);
                byte[] fileContent = GenerateExcelFile(data);
                return File(fileContent, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Admins_Only_Export_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في تصدير Excel للمسؤولين");
                TempData["ErrorMessage"] = $"❌ حدث خطأ: {ex.Message}";
                return RedirectToAction(nameof(Users));
            }
        }

        // ========== 4. تصدير الطلاب الجامعيين فقط (كامل التفاصيل) ==========
        [HttpGet]
        public async Task<IActionResult> ExportStudentsOnlyToExcel()
        {
            try
            {
                var allIdentifies = await _context.Identifies.ToListAsync();
                var studentUserIds = allIdentifies
                    .Where(i => i.Education == "طالب جامعي" ||
                               (!string.IsNullOrEmpty(i.StudyStage) && i.StudyStage != "---") ||
                               i.StudyType == "انتظام" ||
                               i.StudyType == "مسائي")
                    .Select(i => i.UserId)
                    .ToList();

                var users = await _userManager.Users
                    .Where(u => studentUserIds.Contains(u.Id))
                    .OrderBy(u => u.Email)
                    .ToListAsync();

                var data = await BuildFullUsersExcelData(users);
                byte[] fileContent = GenerateExcelFile(data);
                return File(fileContent, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Students_Only_Export_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في تصدير Excel للطلاب");
                TempData["ErrorMessage"] = $"❌ حدث خطأ: {ex.Message}";
                return RedirectToAction(nameof(Users));
            }
        }

        // ========== 5. تصدير المسؤولين الإداريين فقط (مسؤولي الجهات والأقسام والشعب والتجمعات) ==========
        [HttpGet]
        public async Task<IActionResult> ExportAdministrativeManagersOnlyToExcel()
        {
            try
            {
                // جلب جميع المستخدمين الذين لديهم مسؤوليات إدارية في ManagementAssignments
                var managersAssignments = await _context.ManagementAssignments
                    .Select(m => m.UserId)
                    .Distinct()
                    .ToListAsync();

                if (!managersAssignments.Any())
                {
                    TempData["WarningMessage"] = "⚠️ لا يوجد مسؤولين إداريين في النظام";
                    return RedirectToAction(nameof(Users));
                }

                var users = await _userManager.Users
                    .Where(u => managersAssignments.Contains(u.Id))
                    .OrderBy(u => u.Email)
                    .ToListAsync();

                var data = await BuildFullUsersExcelData(users);
                byte[] fileContent = GenerateExcelFile(data);
                return File(fileContent, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Administrative_Managers_Export_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في تصدير Excel للمسؤولين الإداريين");
                TempData["ErrorMessage"] = $"❌ حدث خطأ: {ex.Message}";
                return RedirectToAction(nameof(Users));
            }
        }

        // ========== دالة مساعدة لبناء بيانات Excel كاملة (مطابقة لـ ProfileDetails) ==========
        private async Task<List<object[]>> BuildFullUsersExcelData(List<IdentityUser> users)
        {
            var data = new List<object[]>();

            // رأس الجدول - مطابق لجميع حقول ProfileDetails
            data.Add(new object[]
            {
        // ===== الصورة الشخصية =====
        "الصورة الشخصية",
        
        // ===== المعلومات الشخصية =====
        "الاسم الرباعي",
        "اللقب",
        "اسم الأم",
        "تاريخ الميلاد",
        "الجنس",
        "الحالة الاجتماعية",
        "رقم الهاتف",
        "التحصيل الدراسي",
        "الاختصاص",
        
        // ===== حقول الطالب الجامعي =====
        "نوع الجامعة",
        "نوع المؤسسة",
        "اسم الجامعة/المعهد",
        "الكلية/القسم",
        "نوع الدراسة",
        "المرحلة الدراسية",
        
        // ===== العنوان =====
        "محافظة العمل التنظيمي",
        "محافظة السكن",
        "قضاء السكن",
        "المنطقة",
        "المحلة",
        "الزقاق",
        "الدار",
        "أقرب نقطة دالة",
        
        // ===== المستندات الرسمية =====
        "رقم البطاقة الموحدة",
        "تاريخ إصدار البطاقة",
        "رقم بطاقة الناخب",
        "رقم مركز الاقتراع",
        
        // ===== العمل والتوظيف =====
        "الحالة الوظيفية",
        "جهة العمل",
        "الوزارة",
        "الدائرة",
        "المنصب",
        "العنوان الوظيفي",
        "الدرجة الوظيفية",
        
        // ===== معلومات الانتساب =====
        "جهة الانتساب",
        "القسم (الانتساب)",
        "الشعبة (الانتساب)",
        "الوحدة (الانتساب)",
        "رقم الباج الخاص بك",
        "اسم المزكي",
        "رقم هاتف المزكي",
        "تاريخ الانتماء",
        
        // ===== النقابة =====
        "اسم النقابة",
        "المنصب في النقابة",
        "رقم العضوية في النقابة",
        "تاريخ النفاذ/الانتهاء للنقابة",
        
        // ===== الاتحاد (4 مستويات منفصلة) =====
        "اسم الاتحاد",
        "قسم الاتحاد",
        "شعبة الاتحاد",
        "وحدة الاتحاد",
        "المنصب في الاتحاد",
        "رقم العضوية في الاتحاد",
        "تاريخ النفاذ/الانتهاء للاتحاد",
        
        // ===== الجمعية =====
        "اسم الجمعية",
        "المنصب في الجمعية",
        "رقم العضوية في الجمعية",
        "تاريخ النفاذ/الانتهاء للجمعية",
        
        // ===== المنظمة =====
        "اسم المنظمة",
        "المنصب في المنظمة",
        "رقم العضوية في المنظمة",
        "تاريخ النفاذ/الانتهاء للمنظمة",
        
        // ===== معلومات الحساب =====
        "البريد الإلكتروني",
        "الأدوار",
        "نوع الحساب",
        "حالة الترقية",
        "تاريخ التصعيد",
        "مصعد بواسطة",
        "تاريخ التسجيل",
        "حالة الحساب (نشط؟)",
        
        // ===== معلومات إضافية =====
        "طلب ترقية؟",
        "تاريخ الطلب",
        "سبب الرفض",
        "المسؤوليات الإدارية",
        "المحافظة المُدارة",
        "القضاء المُدار"
            });

            var userIds = users.Select(u => u.Id).ToList();
            var profilesByUserId = await _context.Identifies
                .AsNoTracking()
                .Where(i => userIds.Contains(i.UserId))
                .ToDictionaryAsync(i => i.UserId);
            var profileIds = profilesByUserId.Values.Select(p => p.Id).ToList();

            var rolesByUserId = await _context.UserRoles
                .AsNoTracking()
                .Where(ur => userIds.Contains(ur.UserId))
                .Join(
                    _context.Roles.AsNoTracking(),
                    userRole => userRole.RoleId,
                    roleRow => roleRow.Id,
                    (userRole, roleRow) => new
                    {
                        userRole.UserId,
                        RoleName = roleRow.Name ?? string.Empty
                    })
                .GroupBy(x => x.UserId)
                .ToDictionaryAsync(g => g.Key, g => (IList<string>)g.Select(x => x.RoleName).ToList());

            var addressesByUserId = await _context.Addresses
                .AsNoTracking()
                .Where(a => userIds.Contains(a.UserId))
                .GroupBy(a => a.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.First());

            var workLocationsByProfileId = await _context.WorkLocations
                .AsNoTracking()
                .Where(w => profileIds.Contains(w.IdentifyId))
                .GroupBy(w => w.IdentifyId)
                .ToDictionaryAsync(g => g.Key, g => g.First());

            var voterCardsByUserId = await _context.VoterCards
                .AsNoTracking()
                .Where(v => userIds.Contains(v.UserId))
                .GroupBy(v => v.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.First());

            var unionsByUserId = await _context.UnionMemberships
                .AsNoTracking()
                .Where(u => userIds.Contains(u.UserId))
                .GroupBy(u => u.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.First());

            var federationsByUserId = await _context.FederationMemberships
                .AsNoTracking()
                .Include(f => f.Federation)
                .Include(f => f.FederationDivision)
                .Include(f => f.FederationSection)
                .Include(f => f.FederationGroup)
                .Where(f => userIds.Contains(f.UserId))
                .GroupBy(f => f.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.First());

            var associationsByUserId = await _context.AssociationMemberships
                .AsNoTracking()
                .Where(a => userIds.Contains(a.UserId))
                .GroupBy(a => a.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.First());

            var ngosByUserId = await _context.NgoMemberships
                .AsNoTracking()
                .Where(n => userIds.Contains(n.UserId))
                .GroupBy(n => n.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.First());

            var affiliationInfosByUserId = await _context.AffiliationInfos
                .AsNoTracking()
                .Include(a => a.AffiliationEntity)
                .Include(a => a.Division)
                .Include(a => a.Section)
                .Include(a => a.Group)
                .Where(a => userIds.Contains(a.UserId))
                .GroupBy(a => a.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.First());

            var managementAssignmentsByUserId = await _context.ManagementAssignments
                .AsNoTracking()
                .Where(x => userIds.Contains(x.UserId))
                .GroupBy(x => x.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.ToList());

            var affiliationEntityNames = await _context.AffiliationEntities
                .AsNoTracking()
                .ToDictionaryAsync(e => e.Id, e => e.Name);
            var divisionNames = await _context.Divisions
                .AsNoTracking()
                .ToDictionaryAsync(d => d.Id, d => d.Name);
            var sectionNames = await _context.Sections
                .AsNoTracking()
                .ToDictionaryAsync(s => s.Id, s => s.Name);
            var groupNames = await _context.Groups
                .AsNoTracking()
                .ToDictionaryAsync(g => g.Id, g => g.Name);

            int counter = 1;
            foreach (var user in users)
            {
                var roles = rolesByUserId.TryGetValue(user.Id, out var roleList) ? roleList : new List<string>();
                profilesByUserId.TryGetValue(user.Id, out var userProfile);
                addressesByUserId.TryGetValue(user.Id, out var address);
                var workLocation = userProfile != null && workLocationsByProfileId.TryGetValue(userProfile.Id, out var userWorkLocation)
                    ? userWorkLocation
                    : null;
                voterCardsByUserId.TryGetValue(user.Id, out var voterCard);
                unionsByUserId.TryGetValue(user.Id, out var union);
                federationsByUserId.TryGetValue(user.Id, out var federation);
                associationsByUserId.TryGetValue(user.Id, out var association);
                ngosByUserId.TryGetValue(user.Id, out var ngo);
                affiliationInfosByUserId.TryGetValue(user.Id, out var affiliationInfo);

                // أسماء الكيانات للانتساب
                var affiliationEntityName = affiliationInfo?.AffiliationEntityId.HasValue == true &&
                    affiliationEntityNames.TryGetValue(affiliationInfo.AffiliationEntityId.Value, out var affiliationEntity)
                        ? affiliationEntity
                        : null;
                var divisionName = affiliationInfo?.DivisionId.HasValue == true &&
                    divisionNames.TryGetValue(affiliationInfo.DivisionId.Value, out var division)
                        ? division
                        : null;
                var sectionName = affiliationInfo?.SectionId.HasValue == true &&
                    sectionNames.TryGetValue(affiliationInfo.SectionId.Value, out var section)
                        ? section
                        : null;
                var groupName = affiliationInfo?.GroupId.HasValue == true &&
                    groupNames.TryGetValue(affiliationInfo.GroupId.Value, out var group)
                        ? group
                        : null;

                // أسماء الاتحاد بشكل منفصل
                string federationName = "";
                string federationDivisionName = "";
                string federationSectionName = "";
                string federationGroupName = "";

                var residenceGovernorate = address?.Governorate ?? "";
                var residenceDistrict = address?.District ?? "";
                var workGovernorate = FirstNonBlank(workLocation?.Governorate, userProfile?.WorkGovernorate);
                var workDistrict = workGovernorate == "بغداد"
                    ? FirstNonBlank(workLocation?.District, userProfile?.WorkDistrict)
                    : "";

                if (federation != null)
                {
                    federationName = federation.Federation?.Name ?? "";
                    federationDivisionName = federation.FederationDivision?.Name ?? "";
                    federationSectionName = federation.FederationSection?.Name ?? "";
                    federationGroupName = federation.FederationGroup?.Name ?? "";
                }

                // المسؤوليات الإدارية
                var managementAssignments = managementAssignmentsByUserId.TryGetValue(user.Id, out var assignments)
                    ? assignments
                    : new List<ManagementAssignment>();

                string managementDisplay = "";
                string managedGovernorate = "";
                string managedDistrict = "";
                string assignmentBaghdadScope = "";

                foreach (var assignment in managementAssignments)
                {
                    string entityName = "";
                    string levelArabic = GetArabicLevelName(
    assignment.ManagementLevel,
    assignment.AssignmentRole
);

                    if (assignment.ManagementLevel == "Entity" && assignment.AffiliationEntityId.HasValue)
                    {
                        entityName = affiliationEntityNames.TryGetValue(assignment.AffiliationEntityId.Value, out var entityNameValue)
                            ? entityNameValue
                            : "";
                    }
                    else if (assignment.ManagementLevel == "Division" && assignment.DivisionId.HasValue)
                    {
                        entityName = divisionNames.TryGetValue(assignment.DivisionId.Value, out var divisionNameValue)
                            ? divisionNameValue
                            : "";
                    }
                    else if (assignment.ManagementLevel == "Section" && assignment.SectionId.HasValue)
                    {
                        entityName = sectionNames.TryGetValue(assignment.SectionId.Value, out var sectionNameValue)
                            ? sectionNameValue
                            : "";
                    }
                    else if (assignment.ManagementLevel == "Group" && assignment.GroupId.HasValue)
                    {
                        entityName = groupNames.TryGetValue(assignment.GroupId.Value, out var groupNameValue)
                            ? groupNameValue
                            : "";
                    }

                    managementDisplay += $"{levelArabic}: {entityName}, ";

                    if (!string.IsNullOrEmpty(assignment.Governorate) && string.IsNullOrEmpty(managedGovernorate))
                        managedGovernorate = assignment.Governorate;

                    if (assignment.Governorate == "بغداد" &&
                        string.IsNullOrWhiteSpace(assignmentBaghdadScope) &&
                        !string.IsNullOrWhiteSpace(assignment.BaghdadScope))
                    {
                        assignmentBaghdadScope = assignment.BaghdadScope;
                    }
                }

                if (managedGovernorate == "بغداد" && string.IsNullOrWhiteSpace(managedDistrict))
                {
                    managedDistrict = FirstNonBlank(
                        workLocation?.District,
                        userProfile?.WorkDistrict,
                        userProfile?.ManagedDistrict,
                        assignmentBaghdadScope);
                }

                if (string.IsNullOrWhiteSpace(managedGovernorate))
                {
                    managedGovernorate = FirstNonBlank(userProfile?.ManagedGovernorate, workGovernorate);
                }
                managementDisplay = managementDisplay.TrimEnd(',', ' ');

                data.Add(new object[]
                {
            // ===== الصورة الشخصية =====
            userProfile?.CoverImage ?? "",

            // ===== المعلومات الشخصية =====
            userProfile?.FullName ?? "غير مكتمل",
            userProfile?.LastName ?? "",
            userProfile?.MotherName ?? "",
            userProfile?.Date.ToString("yyyy-MM-dd") ?? "",
            userProfile?.Gender ?? "",
            userProfile?.MaritalStatus ?? "",
            userProfile?.PhoneNumber ?? "",
            userProfile?.Education ?? "",
            userProfile?.Specialization ?? "",

            // ===== حقول الطالب الجامعي =====
            userProfile?.UniversityType ?? "",
            userProfile?.InstitutionType ?? "",
            userProfile?.InstitutionName ?? "",
            userProfile?.FacultyDepartment ?? "",
            userProfile?.StudyType ?? "",
            userProfile?.StudyStage ?? "",

            // ===== العنوان =====
            workGovernorate,
            residenceGovernorate,
            residenceDistrict,
            address?.Area ?? "",
            address?.Alley ?? "",
            address?.Street ?? "",
            address?.House ?? "",
            address?.NearestPoint ?? "",

            // ===== المستندات الرسمية =====
            userProfile?.IdentityCardN ?? "",
            userProfile?.identityDate.ToString("yyyy-MM-dd") ?? "",
            voterCard?.VoterCardNumber ?? "",
            voterCard?.PollingCenterNumber ?? "",

            // ===== العمل والتوظيف =====
            userProfile?.EmploymentStatus ?? "",
            userProfile?.Work ?? "",
            userProfile?.Ministry ?? "",
            userProfile?.Department ?? "",
            userProfile?.Position ?? "",
            userProfile?.JobTitle ?? "",
            CleanExcelPlaceholder(userProfile?.JobGrade, "-- اختر الدرجة الوظيفية --"),

            // ===== معلومات الانتساب =====
            affiliationEntityName ?? "",
            divisionName ?? "",
            sectionName ?? "",
            groupName ?? "",
            affiliationInfo?.BadgeNumber ?? "",
            affiliationInfo?.MozakeName ?? "",
            affiliationInfo?.MozakePhoneNumber ?? "",
            affiliationInfo?.AffiliationDate?.ToString("yyyy-MM-dd") ?? "",

            // ===== النقابة =====
            union?.UnionName ?? "",
            union?.Position ?? "",
            union?.IdNumber ?? "",
            union?.AffiliationDate?.ToString("yyyy-MM-dd") ?? "",

            // ===== الاتحاد =====
            federationName,
            federationDivisionName,
            federationSectionName,
            federationGroupName,
            federation?.Position ?? "",
            federation?.IdNumber ?? "",
            federation?.AffiliationDate?.ToString("yyyy-MM-dd") ?? "",

            // ===== الجمعية =====
            association?.AssociationName ?? "",
            association?.Position ?? "",
            association?.IdNumber ?? "",
            association?.AffiliationDate?.ToString("yyyy-MM-dd") ?? "",

            // ===== المنظمة =====
            ngo?.NgoName ?? "",
            ngo?.Position ?? "",
            ngo?.IdNumber ?? "",
            ngo?.AffiliationDate?.ToString("yyyy-MM-dd") ?? "",

            // ===== معلومات الحساب =====
            user.Email ?? "",
            TranslateRolesForExport(roles),
            TranslateAccountTypeForExport(userProfile?.AccountType),
            userProfile?.IsPromoted == true ? "مصعد" : "غير مصعد",
            userProfile?.PromotionDate?.ToString("yyyy-MM-dd") ?? "",
            await ResolveActorDisplayNameAsync(userProfile?.PromotedBy),
            userProfile?.CreatedAt.ToString("yyyy-MM-dd") ?? "",
            user.EmailConfirmed ? "نشط" : "غير نشط",

            // ===== معلومات إضافية =====
            userProfile?.RequestedPromotion == true ? "نعم" : "لا",
            userProfile?.RequestedPromotionDate?.ToString("yyyy-MM-dd") ?? "",
            userProfile?.RejectionReason ?? "",
            TranslateManagementDisplayForExport(managementDisplay),
            managedGovernorate,
            managedDistrict
                });
            }

            return data;
        }

        // ========== دالة GenerateExcelFile (نفس الموجودة لديك) ==========
        private byte[] GenerateExcelFile(List<object[]> data)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Users");
                for (int i = 0; i < data.Count; i++)
                    for (int j = 0; j < data[i].Length; j++)
                        worksheet.Cell(i + 1, j + 1).Value = NormalizeExcelText(data[i][j]?.ToString());

                var range = worksheet.Range(1, 1, data.Count, data[0].Length);
                range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                var headerRange = worksheet.Range(1, 1, 1, data[0].Length);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return stream.ToArray();
                }
            }
        }

        private static string NormalizeExcelText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var text = value.Trim();
            if (!LooksLikeMojibake(text))
                return text;

            try
            {
                return Encoding.UTF8.GetString(Encoding.GetEncoding("Windows-1252").GetBytes(text));
            }
            catch
            {
                return text;
            }
        }

        private static bool LooksLikeMojibake(string value)
        {
            return value.Contains('Ø') || value.Contains('Ù') || value.Contains('â');
        }

        private static string TranslateRolesForExport(IEnumerable<string> roles)
        {
            return string.Join("، ", roles
                .Select(TranslateRoleForExport)
                .Where(role => !string.IsNullOrWhiteSpace(role)));
        }

        private static string TranslateRoleForExport(string? role)
        {
            return role switch
            {
                clsRoles.SystemManager => "مدير النظام",
                clsRoles.SuperAdmin => "سوبر أدمن",
                clsRoles.Admin => "أدمن",
                clsRoles.DistrictAdmin => "أدمن قضاء",
                clsRoles.User => "مستخدم",
                clsRoles.Member => "فرد",
                clsRoles.NewsEditor => "محرر أخبار",
                clsRoles.MapViewer => "مراقب الخريطة",
                clsRoles.Manager => "مسؤول",
                clsRoles.AssistantManager => "معاون مسؤول",
                null or "" => string.Empty,
                _ => NormalizeExcelText(role)
            };
        }

        private static string TranslateAccountTypeForExport(string? accountType)
        {
            return accountType switch
            {
                "User" => "مستخدم",
                "Member" => "فرد",
                null or "" => "عادي",
                _ => NormalizeExcelText(accountType)
            };
        }

        private static string TranslateManagementDisplayForExport(string? managementDisplay)
        {
            return NormalizeExcelText(managementDisplay);
        }

        private static string CleanExcelPlaceholder(string? value, params string[] placeholders)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var trimmed = value.Trim();
            return placeholders.Any(p => string.Equals(trimmed, p, StringComparison.OrdinalIgnoreCase))
                ? string.Empty
                : trimmed;
        }

        private static string FirstNonBlank(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
        }

        private async Task<string> GetCurrentActorDisplayNameAsync(string fallback)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (!string.IsNullOrWhiteSpace(currentUserId))
            {
                var profile = await _context.Identifies
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.UserId == currentUserId);

                if (!string.IsNullOrWhiteSpace(profile?.FullName))
                    return profile.FullName;

                var user = await _userManager.FindByIdAsync(currentUserId);
                if (!string.IsNullOrWhiteSpace(user?.UserName) && !user.UserName.Contains('@'))
                    return user.UserName;

                if (!string.IsNullOrWhiteSpace(user?.Email))
                    return user.Email;
            }

            return User.Identity?.Name ?? fallback;
        }

        private async Task<string> ResolveActorDisplayNameAsync(string? actorValue)
        {
            if (string.IsNullOrWhiteSpace(actorValue))
                return string.Empty;

            var trimmedValue = actorValue.Trim();

            IdentityUser? actorUser = await _userManager.FindByIdAsync(trimmedValue);
            if (actorUser == null)
            {
                var normalizedValue = trimmedValue.ToLower();
                actorUser = await _userManager.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u =>
                        (u.Email != null && u.Email.ToLower() == normalizedValue) ||
                        (u.UserName != null && u.UserName.ToLower() == normalizedValue) ||
                        (u.PhoneNumber != null && u.PhoneNumber == trimmedValue));
            }

            var profile = await _context.Identifies
                .AsNoTracking()
                .FirstOrDefaultAsync(i =>
                    (actorUser != null && i.UserId == actorUser.Id) ||
                    i.PhoneNumber == trimmedValue ||
                    i.WhatsAppNumber == trimmedValue);

            if (!string.IsNullOrWhiteSpace(profile?.FullName))
                return profile.FullName;

            if (actorUser == null)
                return trimmedValue;

            if (!string.IsNullOrWhiteSpace(actorUser.UserName) && !actorUser.UserName.Contains('@'))
                return actorUser.UserName;

            return actorUser.Email ?? trimmedValue;
        }

        // عرض الأعضاء
        public async Task<IActionResult> Members()
        {
            var allIdentifies = await _context.Identifies.ToListAsync();
            var members = allIdentifies.Where(i => i.AccountType == "فرد" || i.IsPromoted).OrderByDescending(i => i.PromotionDate).ToList();
            var memberList = new List<object>();
            foreach (var member in members)
            {
                var user = await _userManager.FindByIdAsync(member.UserId);
                var address = await GetUserAddressAsync(member.UserId);
                var voterCard = await GetUserVoterCardAsync(member.UserId);
                var union = await GetUserUnionAsync(member.UserId);
                var federation = await GetUserFederationAsync(member.UserId);
                var association = await GetUserAssociationAsync(member.UserId);
                var ngo = await GetUserNgoAsync(member.UserId);
                var affiliationInfo = await GetUserAffiliationInfoAsync(member.UserId);

                var affiliationEntityName = await GetAffiliationEntityNameAsync(affiliationInfo?.AffiliationEntityId);
                var divisionName = await GetDivisionNameAsync(affiliationInfo?.DivisionId);
                var sectionName = await GetSectionNameAsync(affiliationInfo?.SectionId);
                var groupName = await GetGroupNameAsync(affiliationInfo?.GroupId);

                memberList.Add(new { member.Id, member.UserId, UserEmail = user?.Email ?? "", member.FullName, member.PhoneNumber, Governorate = GetEffectiveGovernorate(member, address), District = GetEffectiveDistrict(member, address), member.IdentityCardN, member.Date, member.Gender, member.Education, member.EmploymentStatus, member.Work, member.Position, HasUnion = union != null, UnionName = union?.UnionName, HasFederation = federation != null, FederationName = GetFederationFullName(federation), HasAssociation = association != null, AssociationName = association?.AssociationName, HasNgo = ngo != null, NgoName = ngo?.NgoName, AffiliationEntity = affiliationEntityName, Division = divisionName, Section = sectionName, Group = groupName, member.PromotionDate, PromotedBy = await ResolveActorDisplayNameAsync(member.PromotedBy), member.CreatedAt });
            }
            return View(memberList);
        }

        // ========== دوال مساعدة عامة ==========
        private bool IsProfileComplete(Identify? profile, Address? address, VoterCard? voterCard, AffiliationInfo? affiliationInfo)
        {
            if (profile == null) return false;
            if (string.IsNullOrWhiteSpace(profile.FullName)) return false;
            if (string.IsNullOrWhiteSpace(profile.MotherName)) return false;
            if (string.IsNullOrWhiteSpace(profile.Gender)) return false;
            if (string.IsNullOrWhiteSpace(profile.PhoneNumber)) return false;
            if (string.IsNullOrWhiteSpace(profile.IdentityCardN)) return false;
            if (profile.IdentityCardN.Length != 12) return false;
            if (string.IsNullOrWhiteSpace(GetEffectiveGovernorate(profile, address))) return false;
            return true;
        }

        private int CalculateCompletionPercentage(Identify? profile, Address? address, VoterCard? voterCard)
        {
            if (profile == null) return 0;
            int totalFields = 10;
            int filledFields = 0;
            if (!string.IsNullOrEmpty(profile.FullName)) filledFields++;
            if (!string.IsNullOrEmpty(profile.MotherName)) filledFields++;
            if (!string.IsNullOrEmpty(profile.Gender)) filledFields++;
            if (!string.IsNullOrEmpty(profile.PhoneNumber)) filledFields++;
            if (!string.IsNullOrEmpty(profile.IdentityCardN) && profile.IdentityCardN.Length == 12) filledFields++;
            if (!string.IsNullOrEmpty(GetEffectiveGovernorate(profile, address))) filledFields++;
            if (!string.IsNullOrEmpty(GetEffectiveDistrict(profile, address))) filledFields++;
            if (!string.IsNullOrEmpty(profile.Education)) filledFields++;
            if (voterCard != null && !string.IsNullOrEmpty(voterCard.VoterCardNumber)) filledFields++;
            if (!string.IsNullOrEmpty(profile.CoverImage)) filledFields++;
            return (int)((filledFields / (double)totalFields) * 100);
        }



        // ========== Request Models ==========
        public class BulkDeleteRequest { public List<string> UserIds { get; set; } = new(); }
        public class DeleteUserRequest { public string UserId { get; set; } = string.Empty; }
        public class ToggleStatusRequest { public string UserId { get; set; } = string.Empty; }
        public class ResetUserPasswordRequest
        {
            public string UserId { get; set; } = string.Empty;
            public string NewPassword { get; set; } = string.Empty;
        }
        public class UpdateRolesRequest
        {
            public string UserId { get; set; } = string.Empty;
            public List<string> SelectedRoles { get; set; } = new();
            public string? ManagedGovernorate { get; set; }  // ✅ أضف هذا
            public string? ManagedDistrict { get; set; }     // ✅ أضف هذا
        }
        public class SendNotificationViewModel
        {
            public string Title { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public string? TargetUserId { get; set; }
            public List<string> TargetUserIds { get; set; } = new();
            public string? Icon { get; set; }
            public string? ClickUrl { get; set; }
            public bool SendToAll { get; set; }
            public string? SelectedGovernoratesCsv { get; set; }
            public string? SelectedAudienceGroupsCsv { get; set; }
            public string? SelectedRolesCsv { get; set; }
            public string? SearchTerm { get; set; }
            public string? SelectedUserIdsCsv { get; set; }
            public string? ReportFiltersJson { get; set; }
        }
        private sealed class NotificationRecipientUserSnapshot
        {
            public string Id { get; set; } = string.Empty;
            public string? Email { get; set; }
            public string? UserName { get; set; }
            public string? PhoneNumber { get; set; }
        }
        private sealed class NotificationRecipientProfileSnapshot
        {
            public string UserId { get; set; } = string.Empty;
            public string? FullName { get; set; }
            public string? PhoneNumber { get; set; }
            public string? WorkGovernorate { get; set; }
            public string? ManagedGovernorate { get; set; }
            public string? WorkLocationGovernorate { get; set; }
            public bool IsPromoted { get; set; }
            public string? AccountType { get; set; }
        }
        private sealed class NotificationRecipientItem
        {
            public string Id { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public string PhoneNumber { get; set; } = string.Empty;
            public string Governorate { get; set; } = string.Empty;
            public string AccountType { get; set; } = string.Empty;
            public List<string> Roles { get; set; } = new();
        }
        private sealed class NotificationRecipientResolution
        {
            public string Source { get; set; } = string.Empty;
            public bool IsBroadcast { get; set; }
            public List<string> UserIds { get; set; } = new();
        }
        private sealed record SqlDatabaseFileInfo(string LogicalName, string PhysicalName, string TypeDesc, int FileId);
        private sealed record SqlBackupFileInfo(string LogicalName, string PhysicalName, string Type, string? FileGroupName);
    }
}
