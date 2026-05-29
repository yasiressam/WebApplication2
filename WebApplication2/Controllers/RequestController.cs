using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using WebApplication2.Data;

using WebApplication2.Models;
using WebApplication2.Models.Request;

namespace WebApplication2.Controllers
{
    [Authorize]
    public class RequestController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<RequestController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly string _requestUploadPath;
        private const long MaxRequestAttachmentSize = 10 * 1024 * 1024;

        public RequestController(
            UserManager<IdentityUser> userManager,
            ApplicationDbContext context,
            ILogger<RequestController> logger)
        {
            _userManager = userManager;
            _context = context;
            _logger = logger;
            _requestUploadPath = Path.Combine("C:\\Users", "Public", "MyApp_Uploads", "Requests");
            Directory.CreateDirectory(_requestUploadPath);
        }

        private readonly string[] _allowedRoles = new[] { "SuperAdmin", "Admin", "DistrictAdmin", "Manager", "AssistantManager", "فرد" };
        private readonly string[] _targetRoles = new[] { "SuperAdmin", "Admin", "DistrictAdmin", "Manager", "AssistantManager", "فرد" };
        private readonly string[] _adminRoles = new[] { "Admin", "DistrictAdmin" };
        private readonly string[] _managerRoles = new[] { "Manager", "AssistantManager" };
        private readonly string[] _requestTitles = new[]
        {
            "طلب تعديل بيانات",
            "طلب دعم فني",
            "طلب موافقة إدارية",
            "طلب استفسار",
            "طلب شكوى",
            "طلب متابعة طلب سابق",
            "طلب إضافة معلومات",
            "طلب تحديث معلومات",
            "أخرى"
        };

        private sealed record RequestLocationScope(string Governorate, string District);

        private async Task<bool> IsAllowedUser()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var userRoles = await _userManager.GetRolesAsync(currentUser);
            return userRoles.Any(r => _allowedRoles.Contains(r));
        }

        public override async Task OnActionExecutionAsync(
            Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context,
            Microsoft.AspNetCore.Mvc.Filters.ActionExecutionDelegate next)
        {
            if (!await IsAllowedUser())
            {
                context.Result = RedirectToAction("AccessDenied", "Account");
                return;
            }
            await next();
        }

        // ===== دوال مساعدة محسنة =====

        private async Task<Dictionary<string, string>> GetUsersFullNamesAsync(List<string> userIds)
        {
            if (userIds == null || !userIds.Any())
                return new Dictionary<string, string>();

            var identities = await _context.Identifies
                .Where(i => userIds.Contains(i.UserId) && !string.IsNullOrEmpty(i.FullName))
                .ToDictionaryAsync(i => i.UserId, i => i.FullName);

            var missingUserIds = userIds.Where(id => !identities.ContainsKey(id)).ToList();
            if (missingUserIds.Any())
            {
                var users = await _userManager.Users
                    .Where(u => missingUserIds.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id, u => u.UserName?.Split('@')[0] ?? u.Id);
                foreach (var user in users)
                    identities[user.Key] = user.Value;
            }

            foreach (var id in userIds)
                if (!identities.ContainsKey(id))
                    identities[id] = id;

            return identities;
        }

        private async Task<Dictionary<string, string>> GetUsersRolesAsync(List<string> userIds)
        {
            var result = new Dictionary<string, string>();
            foreach (var userId in userIds)
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user != null)
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    result[userId] = GetRolesDisplayName(roles);
                }
                else
                {
                    result[userId] = "غير معروف";
                }
            }
            return result;
        }

        private string GetRoleDisplayName(string? role)
        {
            return role switch
            {
                "SuperAdmin" => "سوبر أدمن",
                "Admin" => "أدمن محافظة",
                "DistrictAdmin" => "أدمن محافظة",
                "Manager" => "مسؤول",
                "AssistantManager" => "معاون مسؤول",
                "NewsEditor" => "محرر أخبار",
                "MapViewer" => "مشاهد خريطة",
                "User" => "مستخدم",
                "فرد" => "فرد",
                null or "" => "مستخدم",
                _ => role
            };
        }

        private string GetRolesDisplayName(IEnumerable<string> roles)
        {
            var roleList = roles?.ToList() ?? new List<string>();
            return roleList.Any()
                ? string.Join(", ", roleList.Select(GetRoleDisplayName))
                : "مستخدم";
        }

        private async Task<bool> CanSendTo(IdentityUser sender, IList<string> senderRoles, IdentityUser recipient)
        {
            if (sender.Id == recipient.Id)
                return false;

            var recipientRoles = await _userManager.GetRolesAsync(recipient);

            if (senderRoles.Contains("SuperAdmin"))
                return recipientRoles.Any(r => _targetRoles.Contains(r));

            var senderScopes = await GetUserRequestLocationScopesAsync(sender.Id, senderRoles);
            var recipientScopes = await GetUserRequestLocationScopesAsync(recipient.Id, recipientRoles);
            var isSameLocationScope = HasSharedLocationScope(senderScopes, recipientScopes);

            if (senderRoles.Any(r => _adminRoles.Contains(r)))
            {
                return recipientRoles.Contains("SuperAdmin") ||
                       (isSameLocationScope && recipientRoles.Any(r => _managerRoles.Contains(r) || r == "فرد"));
            }
            else if (senderRoles.Any(r => _managerRoles.Contains(r)))
            {
                return isSameLocationScope &&
                       recipientRoles.Any(r => _adminRoles.Contains(r) || r == "فرد");
            }
            else if (senderRoles.Contains("فرد"))
                return recipientRoles.Any(r => r is "SuperAdmin" or "Admin" or "DistrictAdmin" or "Manager" or "AssistantManager");

            return false;
        }

        // ========== عرض الطلبات ==========
        [HttpGet]
        public async Task<IActionResult> Index(int page = 1)
        {
            const int pageSize = 10;
            var currentUser = await _userManager.GetUserAsync(User);
            var userRoles = await _userManager.GetRolesAsync(currentUser);
            if (userRoles.Contains("فرد") &&
                !userRoles.Any(r => r is "SuperAdmin" or "Admin" or "DistrictAdmin" or "Manager" or "AssistantManager"))
            {
                TempData["WarningMessage"] = "يمكنك متابعة الطلبات من خلال روابط الإشعارات فقط.";
                return RedirectToAction("Index", "Home");
            }

            ViewBag.CurrentUserRole = userRoles.FirstOrDefault() ?? "فرد";
            ViewBag.IsStaffRequestUser = userRoles.Any(r => r is "SuperAdmin" or "Admin" or "DistrictAdmin" or "Manager" or "AssistantManager");
            ViewBag.CurrentUserFullName = await GetUserFullNameAsync(currentUser.Id);

            var query = _context.Requests
                .Include(r => r.Recipients)
                .AsQueryable();

            if (userRoles.Contains("SuperAdmin"))
            {
                // يرى كل شيء
            }
            else if (userRoles.Contains("Admin"))
            {
                var usersInRoleFard = await _userManager.GetUsersInRoleAsync("فرد");
                var fardUserIds = usersInRoleFard.Select(u => u.Id).ToList();

                query = query.Where(r =>
                    r.SenderId == currentUser.Id ||
                    r.Recipients.Any(rec => rec.RecipientId == currentUser.Id) ||
                    fardUserIds.Contains(r.SenderId));
            }
            else if (userRoles.Contains("DistrictAdmin") || userRoles.Contains("Manager") || userRoles.Contains("AssistantManager"))
            {
                query = query.Where(r =>
                    r.SenderId == currentUser.Id ||
                    r.Recipients.Any(rec => rec.RecipientId == currentUser.Id));
            }
            else if (userRoles.Contains("فرد"))
            {
                query = query.Where(r =>
                    r.SenderId == currentUser.Id ||
                    r.Recipients.Any(rec => rec.RecipientId == currentUser.Id));
            }

            var totalRequests = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalRequests / (double)pageSize));
            page = Math.Max(1, Math.Min(page, totalPages));

            var requests = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var allUserIds = new List<string>();
            foreach (var r in requests)
            {
                allUserIds.Add(r.SenderId);
                allUserIds.AddRange(r.Recipients.Select(rec => rec.RecipientId));
            }
            allUserIds = allUserIds.Distinct().ToList();

            var usersData = await GetUsersFullNamesAsync(allUserIds);
            var rolesData = await GetUsersRolesAsync(allUserIds);

            var viewModel = new List<RequestListItemViewModel>();
            foreach (var r in requests)
            {
                var senderName = usersData.GetValueOrDefault(r.SenderId) ?? r.SenderId;
                var senderRole = rolesData.GetValueOrDefault(r.SenderId) ?? "مستخدم";

                var canSeeRequestRecipients = userRoles.Contains("SuperAdmin") || r.SenderId == currentUser.Id;
                var isCurrentUserRecipient = r.Recipients.Any(rec => rec.RecipientId == currentUser.Id);
                var recipientNames = canSeeRequestRecipients
                    ? r.Recipients
                        .Select(rec => usersData.GetValueOrDefault(rec.RecipientId) ?? rec.RecipientId)
                        .ToList()
                    : isCurrentUserRecipient
                        ? new List<string> { usersData.GetValueOrDefault(currentUser.Id) ?? "أنت" }
                        : new List<string> { "مخفي" };

                var isRead = r.Recipients.Any(rec => rec.RecipientId == currentUser.Id && rec.IsRead);

                viewModel.Add(new RequestListItemViewModel
                {
                    Id = r.Id,
                    Title = r.Title,
                    StatusName = GetEnumDisplayName(r.Status),
                    TypeName = GetEnumDisplayName(r.Type),
                    PriorityName = GetEnumDisplayName(r.Priority),
                    Priority = r.Priority,
                    CreatedAt = r.CreatedAt,
                    SenderName = senderName,
                    SenderRole = senderRole,
                    RecipientsNames = string.Join(", ", recipientNames),
                    IsRead = isRead
                });
            }

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalRequests = totalRequests;
            ViewBag.PageSize = pageSize;
            return View(viewModel);
        }

        // ========== إنشاء طلب جديد ==========
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var userRoles = await _userManager.GetRolesAsync(currentUser);

            ViewBag.CurrentUserFullName = await GetUserFullNameAsync(currentUser.Id);
            ViewBag.UserRoles = userRoles;
            ViewBag.TargetUsers = await GetTargetUsersWithDetails(currentUser, userRoles);
            ViewBag.RequestTitles = GetRequestTitleOptions();

            return View(new RequestCreateViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RequestCreateViewModel model)
        {
            model.Title = model.Title?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(model.Title) && !_requestTitles.Contains(model.Title))
                ModelState.AddModelError(nameof(model.Title), "اختر عنواناً من القائمة فقط");

            ValidateRequestAttachment(model.AttachmentFile);

            if (!ModelState.IsValid || model.RecipientIds == null || !model.RecipientIds.Any())
            {
                var currentUser = await _userManager.GetUserAsync(User);
                var userRoles = await _userManager.GetRolesAsync(currentUser);

                ViewBag.CurrentUserFullName = await GetUserFullNameAsync(currentUser.Id);
                ViewBag.UserRoles = userRoles;
                ViewBag.TargetUsers = await GetTargetUsersWithDetails(currentUser, userRoles);
                ViewBag.RequestTitles = GetRequestTitleOptions();

                if (model.RecipientIds == null || !model.RecipientIds.Any())
                    ModelState.AddModelError("RecipientIds", "يجب اختيار مستلم واحد على الأقل");

                return View(model);
            }

            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                var userRoles = await _userManager.GetRolesAsync(currentUser);

                var validRecipients = new List<IdentityUser>();
                foreach (var recipientId in model.RecipientIds)
                {
                    var recipient = await _userManager.FindByIdAsync(recipientId);
                    if (recipient != null && await CanSendTo(currentUser, userRoles, recipient))
                    {
                        validRecipients.Add(recipient);
                    }
                }

                if (!validRecipients.Any())
                {
                    ModelState.AddModelError("RecipientIds", "لا يوجد مستلمين صالحين للإرسال إليهم");
                    ViewBag.CurrentUserFullName = await GetUserFullNameAsync(currentUser.Id);
                    ViewBag.UserRoles = userRoles;
                    ViewBag.TargetUsers = await GetTargetUsersWithDetails(currentUser, userRoles);
                    ViewBag.RequestTitles = GetRequestTitleOptions();
                    return View(model);
                }

                var attachmentInfo = await SaveRequestAttachmentAsync(model.AttachmentFile);

                var request = new Request
                {
                    Title = model.Title,
                    Content = model.Content,
                    Type = model.Type,
                    Priority = model.Priority,
                    SenderId = currentUser.Id,
                    CreatedAt = DateTime.UtcNow,
                    Status = RequestStatus.Pending,
                    AttachmentPath = attachmentInfo?.Path,
                    AttachmentFileName = attachmentInfo?.FileName,
                    AttachmentContentType = attachmentInfo?.ContentType,
                    AttachmentSize = attachmentInfo?.Size,
                    Recipients = new List<RequestRecipient>()
                };

                foreach (var recipient in validRecipients)
                {
                    request.Recipients.Add(new RequestRecipient
                    {
                        RecipientId = recipient.Id,
                        IsRead = false,
                        HasResponded = false
                    });
                }

                await using var transaction = await _context.Database.BeginTransactionAsync();

                _context.Requests.Add(request);
                await _context.SaveChangesAsync();

                foreach (var recipient in validRecipients)
                {
                    var notification = new Notification
                    {
                        Title = "📬 طلب جديد",
                        Message = $"لديك طلب جديد من {await GetUserFullNameAsync(currentUser.Id)}: {model.Title}",
                        ClickUrl = $"/Request/Details/{request.Id}",
                        TargetUserId = recipient.Id,
                        IsForAll = false,
                        Icon = "bi-envelope-fill",
                        SentAt = DateTime.Now,
                        IsRead = false
                    };
                    _context.Notifications.Add(notification);
                }

                var confirmNotification = new Notification
                {
                    Title = "✅ تم إرسال طلبك",
                    Message = $"تم إرسال طلبك '{model.Title}' بنجاح إلى {request.Recipients.Count} مستلم(ين)",
                    ClickUrl = $"/Request/Details/{request.Id}",
                    TargetUserId = currentUser.Id,
                    IsForAll = false,
                    Icon = "bi-check-circle-fill",
                    SentAt = DateTime.Now,
                    IsRead = false
                };
                _context.Notifications.Add(confirmNotification);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = "✅ تم إرسال الطلب بنجاح";
                if (userRoles.Contains("فرد") &&
                    !userRoles.Any(r => r is "SuperAdmin" or "Admin" or "DistrictAdmin" or "Manager" or "AssistantManager"))
                {
                    return RedirectToAction(nameof(Details), new { id = request.Id });
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في إنشاء طلب");
                ModelState.AddModelError("", "حدث خطأ أثناء إرسال الطلب");

                var currentUser = await _userManager.GetUserAsync(User);
                var userRoles = await _userManager.GetRolesAsync(currentUser);
                ViewBag.CurrentUserFullName = await GetUserFullNameAsync(currentUser.Id);
                ViewBag.UserRoles = userRoles;
                ViewBag.TargetUsers = await GetTargetUsersWithDetails(currentUser, userRoles);
                ViewBag.RequestTitles = GetRequestTitleOptions();

                return View(model);
            }
        }

        // ========== عرض تفاصيل طلب (مع الردود حسب الصلاحية) ==========
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var userRoles = await _userManager.GetRolesAsync(currentUser);

            ViewBag.CurrentUserId = currentUser.Id;

            var request = await _context.Requests
                .Include(r => r.Recipients)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null)
                return NotFound();

            // التحقق من الصلاحية للعرض
            bool canView = false;
            var isRecipient = request.Recipients.Any(rec => rec.RecipientId == currentUser.Id);
            var isSender = request.SenderId == currentUser.Id;

            if (userRoles.Contains("SuperAdmin"))
                canView = true;
            else if (userRoles.Contains("Admin") || userRoles.Contains("DistrictAdmin") ||
                     userRoles.Contains("Manager") || userRoles.Contains("AssistantManager"))
                canView = isSender || isRecipient;
            else if (userRoles.Contains("فرد"))
                canView = isSender || isRecipient;

            if (!canView)
                return Forbid();

            // تحديث حالة القراءة
            if (isRecipient)
            {
                var recipient = request.Recipients.FirstOrDefault(rec => rec.RecipientId == currentUser.Id);
                if (recipient != null && !recipient.IsRead)
                {
                    recipient.IsRead = true;
                    _context.RequestRecipients.Update(recipient);
                    await _context.SaveChangesAsync();
                }
            }

            // ✅ تحديد ما إذا كان المستخدم يرى كل شيء أم لا
            bool canSeeAll = userRoles.Contains("SuperAdmin") || userRoles.Contains("Admin") ||
                             userRoles.Contains("DistrictAdmin") || userRoles.Contains("Manager") ||
                             userRoles.Contains("AssistantManager") || isSender;
            bool canSeeRecipients = userRoles.Contains("SuperAdmin") || isSender;

            // ✅ جلب الردود حسب الصلاحية
            List<ReplyInfoViewModel> repliesList = new List<ReplyInfoViewModel>();

            if (canSeeAll)
            {
                // سوبر أدمن، أدمن، أو المرسل: يرى جميع الردود
                var replies = await _context.RequestReplies
                    .Where(r => r.RequestId == id)
                    .ToListAsync();

                foreach (var reply in replies.OrderBy(r => r.RepliedAt))
                {
                    var replyUser = await _userManager.FindByIdAsync(reply.UserId);
                    var replyUserRoles = replyUser != null ? await _userManager.GetRolesAsync(replyUser) : new List<string>();
                    repliesList.Add(new ReplyInfoViewModel
                    {
                        Id = reply.Id,
                        UserName = await GetUserFullNameAsync(reply.UserId),
                        UserRole = GetRolesDisplayName(replyUserRoles),
                        ReplyContent = reply.Reply,
                        RepliedAt = reply.RepliedAt,
                        Notes = reply.Notes
                    });
                }
            }
            else
            {
                // ✅ الفرد المستلم: يرى ردوده فقط (لا يرى ردود الإدارة)
                var replies = await _context.RequestReplies
                    .Where(r => r.RequestId == id && r.UserId == currentUser.Id)
                    .ToListAsync();

                foreach (var reply in replies.OrderBy(r => r.RepliedAt))
                {
                    repliesList.Add(new ReplyInfoViewModel
                    {
                        Id = reply.Id,
                        UserName = await GetUserFullNameAsync(reply.UserId),
                        UserRole = "فرد",
                        ReplyContent = reply.Reply,
                        RepliedAt = reply.RepliedAt,
                        Notes = reply.Notes
                    });
                }
            }

            // جلب بيانات جميع المستخدمين
            var allUserIds = new List<string> { request.SenderId };
            allUserIds.AddRange(request.Recipients.Select(r => r.RecipientId));
            if (request.ProcessedById != null)
                allUserIds.Add(request.ProcessedById);
            allUserIds = allUserIds.Distinct().ToList();

            var usersData = await GetUsersFullNamesAsync(allUserIds);
            var rolesData = await GetUsersRolesAsync(allUserIds);

            var senderName = usersData.GetValueOrDefault(request.SenderId) ?? request.SenderId;
            var senderRole = rolesData.GetValueOrDefault(request.SenderId) ?? "مستخدم";

            // ✅ بناء قائمة المستلمين حسب الصلاحية
            var recipientsInfo = new List<RecipientInfoViewModel>();

            if (canSeeRecipients)
            {
                // السوبر أدمن أو المرسل فقط: يرى جميع المستلمين
                foreach (var rec in request.Recipients)
                {
                    recipientsInfo.Add(new RecipientInfoViewModel
                    {
                        Id = rec.RecipientId,
                        Name = usersData.GetValueOrDefault(rec.RecipientId) ?? rec.RecipientId,
                        Role = rolesData.GetValueOrDefault(rec.RecipientId) ?? "مستخدم",
                        IsRead = rec.IsRead,
                        HasResponded = rec.HasResponded,
                        RespondedAt = rec.RespondedAt
                    });
                }
            }
            else if (isRecipient)
            {
                // ✅ الفرد المستلم: يرى نفسه فقط
                var currentRecipient = request.Recipients.FirstOrDefault(rec => rec.RecipientId == currentUser.Id);
                if (currentRecipient != null)
                {
                    recipientsInfo.Add(new RecipientInfoViewModel
                    {
                        Id = currentRecipient.RecipientId,
                        Name = usersData.GetValueOrDefault(currentRecipient.RecipientId) ?? currentRecipient.RecipientId,
                        Role = rolesData.GetValueOrDefault(currentRecipient.RecipientId) ?? "مستخدم",
                        IsRead = currentRecipient.IsRead,
                        HasResponded = currentRecipient.HasResponded,
                        RespondedAt = currentRecipient.RespondedAt
                    });
                }
            }

            var processorName = !string.IsNullOrEmpty(request.ProcessedById)
                ? usersData.GetValueOrDefault(request.ProcessedById) : null;
            var processorRole = !string.IsNullOrEmpty(request.ProcessedById)
                ? rolesData.GetValueOrDefault(request.ProcessedById) : null;

            var viewModel = new RequestDetailsViewModel
            {
                Id = request.Id,
                Title = request.Title,
                Content = request.Content,
                StatusName = GetEnumDisplayName(request.Status),
                TypeName = GetEnumDisplayName(request.Type),
                PriorityName = GetEnumDisplayName(request.Priority),
                Priority = request.Priority,
                CreatedAt = request.CreatedAt,
                UpdatedAt = request.UpdatedAt,
                ProcessedAt = request.ProcessedAt,
                SenderId = request.SenderId,
                SenderName = senderName,
                SenderRole = senderRole,
                Recipients = recipientsInfo,
                AdminResponse = request.AdminResponse,
                ProcessorName = processorName,
                ProcessorRole = processorRole,
                Notes = request.Notes,
                AttachmentPath = request.AttachmentPath,
                AttachmentFileName = request.AttachmentFileName,
                AttachmentContentType = request.AttachmentContentType,
                AttachmentSize = request.AttachmentSize,
                Replies = repliesList
            };

            // التحقق من صلاحية الرد
            bool canRespond = false;
            bool hasResponded = false;
            var recipientRecord = request.Recipients.FirstOrDefault(rec => rec.RecipientId == currentUser.Id);

            if (userRoles.Contains("SuperAdmin") || userRoles.Contains("Admin"))
            {
                canRespond = true;
            }
            else if ((userRoles.Contains("DistrictAdmin") || userRoles.Contains("Manager") || userRoles.Contains("AssistantManager")) && isRecipient)
            {
                canRespond = true;
            }
            else if (userRoles.Contains("فرد") && isRecipient)
            {
                canRespond = true;
            }

            if (request.SenderId == currentUser.Id)
                canRespond = false;

            ViewBag.CanRespond = canRespond;
            ViewBag.HasResponded = hasResponded;
            ViewBag.CanForward = userRoles.Contains("Admin") || userRoles.Contains("SuperAdmin") ||
                                 userRoles.Contains("DistrictAdmin") || userRoles.Contains("Manager") ||
                                 userRoles.Contains("AssistantManager");
            ViewBag.IsStaffRequestUser = userRoles.Any(r => r is "SuperAdmin" or "Admin" or "DistrictAdmin" or "Manager" or "AssistantManager");
            ViewBag.IsRecipient = isRecipient;
            ViewBag.IsSender = isSender;
            ViewBag.HasMultipleRecipients = canSeeRecipients && request.Recipients.Count > 1;
            ViewBag.CanSeeAll = canSeeAll;
            ViewBag.CanSeeRecipients = canSeeRecipients;

            return View(viewModel);
        }

        // ========== الرد على طلب (مع حفظ الرد في جدول الردود) ==========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Respond(int id, RequestResponseViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            if (!ModelState.IsValid)
                return RedirectToAction(nameof(Details), new { id });

            var currentUser = await _userManager.GetUserAsync(User);
            var userRoles = await _userManager.GetRolesAsync(currentUser);
            var request = await _context.Requests
                .Include(r => r.Recipients)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null)
                return NotFound();

            // التحقق من صلاحية الرد
            bool canRespond = false;
            var isRecipient = request.Recipients.Any(rec => rec.RecipientId == currentUser.Id);
            var recipientRecord = request.Recipients.FirstOrDefault(rec => rec.RecipientId == currentUser.Id);

            if (userRoles.Contains("SuperAdmin") || userRoles.Contains("Admin") || userRoles.Contains("DistrictAdmin") ||
                userRoles.Contains("Manager") || userRoles.Contains("AssistantManager"))
            {
                canRespond = true;
            }
            else if (userRoles.Contains("فرد") && isRecipient)
            {
                if (recipientRecord != null)
                    canRespond = true;
            }

            if (request.SenderId == currentUser.Id)
                canRespond = false;

            if (!canRespond)
            {
                if (recipientRecord?.HasResponded == true)
                    TempData["ErrorMessage"] = "❌ لقد قمت بالرد على هذا الطلب مسبقاً. لا يمكنك الرد مرة أخرى.";
                else if (request.SenderId == currentUser.Id)
                    TempData["ErrorMessage"] = "❌ لا يمكنك الرد على طلبك الخاص.";
                return RedirectToAction(nameof(Details), new { id });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // ✅ حفظ الرد في جدول RequestReplies
                var reply = new RequestReply
                {
                    RequestId = request.Id,
                    UserId = currentUser.Id,
                    Reply = model.AdminResponse,
                    Notes = model.Notes,
                    NewStatus = model.NewStatus,
                    RepliedAt = DateTime.UtcNow
                };
                _context.RequestReplies.Add(reply);

                // تحديث الطلب الأساسي
                request.AdminResponse = model.AdminResponse;
                request.Status = model.NewStatus;
                request.ProcessedById = currentUser.Id;
                request.ProcessedAt = DateTime.UtcNow;
                request.UpdatedAt = DateTime.UtcNow;
                request.Notes = model.Notes;

                if (recipientRecord != null)
                {
                    recipientRecord.HasResponded = true;
                    recipientRecord.RespondedAt = DateTime.UtcNow;
                    _context.RequestRecipients.Update(recipientRecord);
                }

                _context.Requests.Update(request);
                await _context.SaveChangesAsync();

                // إرسال إشعار للمرسل
                var notification = new Notification
                {
                    Title = "📨 تم الرد على طلبك",
                    Message = $"تم الرد على طلبك '{request.Title}' بواسطة {await GetUserFullNameAsync(currentUser.Id)}",
                    ClickUrl = $"/Request/Details/{request.Id}",
                    TargetUserId = request.SenderId,
                    IsForAll = false,
                    Icon = "bi-reply-fill",
                    SentAt = DateTime.Now,
                    IsRead = false
                };
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                TempData["SuccessMessage"] = "✅ تم الرد على الطلب بنجاح";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "خطأ في الرد على الطلب {RequestId}", id);
                TempData["ErrorMessage"] = "❌ حدث خطأ أثناء الرد على الطلب";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // ========== دوال مساعدة ==========

        private string GetEnumDisplayName(Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = field?.GetCustomAttributes(typeof(DisplayAttribute), false)
                               .FirstOrDefault() as DisplayAttribute;
            return attribute?.Name ?? value.ToString();
        }

        private List<SelectListItem> GetRequestTitleOptions()
        {
            return _requestTitles
                .Select(title => new SelectListItem
                {
                    Value = title,
                    Text = title
                })
                .ToList();
        }

        private async Task<string> GetUserFullNameAsync(string userId)
        {
            var identify = await _context.Identifies
                .FirstOrDefaultAsync(i => i.UserId == userId);

            if (identify != null && !string.IsNullOrEmpty(identify.FullName))
                return identify.FullName;

            var user = await _userManager.FindByIdAsync(userId);
            return user?.UserName?.Split('@')[0] ?? "غير معروف";
        }

        private static string NormalizeGovernorate(string? governorate)
        {
            return string.IsNullOrWhiteSpace(governorate) ? string.Empty : governorate.Trim();
        }

        private static string NormalizeDistrict(string? district)
        {
            return string.IsNullOrWhiteSpace(district) ? string.Empty : district.Trim();
        }

        private static bool IsGovernorateInManagedScope(string? governorate, string? managedGovernorate)
        {
            if (string.IsNullOrWhiteSpace(governorate) || string.IsNullOrWhiteSpace(managedGovernorate))
                return false;

            var current = governorate.Trim();
            var managed = managedGovernorate.Trim();

            if (string.Equals(current, managed, StringComparison.OrdinalIgnoreCase))
                return true;

            return managed == "بغداد مركزي" &&
                   (current == "بغداد" || current.StartsWith("بغداد -", StringComparison.OrdinalIgnoreCase));
        }

        private static bool HasSharedLocationScope(IEnumerable<RequestLocationScope> first, IEnumerable<RequestLocationScope> second)
        {
            var firstScopes = first
                .Where(s => !string.IsNullOrWhiteSpace(s.Governorate))
                .ToList();

            var secondScopes = second
                .Where(s => !string.IsNullOrWhiteSpace(s.Governorate))
                .ToList();

            return firstScopes.Any(a => secondScopes.Any(b =>
                (IsGovernorateInManagedScope(a.Governorate, b.Governorate) ||
                 IsGovernorateInManagedScope(b.Governorate, a.Governorate)) &&
                (string.IsNullOrWhiteSpace(a.District) ||
                 string.IsNullOrWhiteSpace(b.District) ||
                 string.Equals(a.District, b.District, StringComparison.OrdinalIgnoreCase))));
        }

        private async Task<List<RequestLocationScope>> GetUserRequestLocationScopesAsync(string userId, IList<string>? roles = null)
        {
            var scopes = new List<RequestLocationScope>();

            var identify = await _context.Identifies
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.UserId == userId);

            if (roles?.Any(r => _managerRoles.Contains(r)) == true)
            {
                var assignmentGovernorates = await _context.ManagementAssignments
                    .AsNoTracking()
                    .Where(a => a.UserId == userId && !string.IsNullOrWhiteSpace(a.Governorate))
                    .Select(a => a.Governorate)
                    .ToListAsync();

                scopes.AddRange(assignmentGovernorates
                    .Select(g => new RequestLocationScope(NormalizeGovernorate(g), NormalizeDistrict(identify?.ManagedDistrict ?? identify?.WorkDistrict))));
            }

            if (roles?.Any(r => _adminRoles.Contains(r)) == true)
            {
                scopes.Add(new RequestLocationScope(
                    NormalizeGovernorate(identify?.ManagedGovernorate),
                    NormalizeDistrict(identify?.ManagedDistrict)));
            }

            if (identify != null)
            {
                var workLocation = await _context.WorkLocations
                    .AsNoTracking()
                    .Where(w => w.IdentifyId == identify.Id)
                    .Select(w => new { w.Governorate, w.District })
                    .FirstOrDefaultAsync();

                scopes.Add(new RequestLocationScope(
                    NormalizeGovernorate(workLocation?.Governorate),
                    NormalizeDistrict(workLocation?.District)));

                scopes.Add(new RequestLocationScope(
                    NormalizeGovernorate(identify.WorkGovernorate),
                    NormalizeDistrict(identify.WorkDistrict)));

                scopes.Add(new RequestLocationScope(
                    NormalizeGovernorate(identify.ManagedGovernorate),
                    NormalizeDistrict(identify.ManagedDistrict)));
            }

            return scopes
                .Select(s => new RequestLocationScope(NormalizeGovernorate(s.Governorate), NormalizeDistrict(s.District)))
                .Where(s => !string.IsNullOrWhiteSpace(s.Governorate))
                .Distinct()
                .ToList();
        }

        private static string GetRecipientCategory(IEnumerable<string> roles)
        {
            var roleList = roles?.ToList() ?? new List<string>();
            if (roleList.Contains("SuperAdmin"))
                return "superadmin";
            if (roleList.Any(r => r is "Admin" or "DistrictAdmin"))
                return "admin";
            if (roleList.Any(r => r is "Manager" or "AssistantManager"))
                return "manager";
            return "member";
        }

        private sealed record RequestAttachmentInfo(string Path, string FileName, string ContentType, long Size);

        private void ValidateRequestAttachment(IFormFile? file)
        {
            if (file == null || file.Length == 0)
                return;

            if (file.Length > MaxRequestAttachmentSize)
            {
                ModelState.AddModelError(nameof(RequestCreateViewModel.AttachmentFile), "حجم الملف يجب أن لا يتجاوز 10MB");
            }
        }

        private async Task<RequestAttachmentInfo?> SaveRequestAttachmentAsync(IFormFile? file)
        {
            if (file == null || file.Length == 0)
                return null;

            var safeFileName = Path.GetFileName(file.FileName);
            var extension = Path.GetExtension(safeFileName);
            var storedFileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(_requestUploadPath, storedFileName);

            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return new RequestAttachmentInfo(
                $"/MyApp_Uploads/Requests/{storedFileName}",
                safeFileName,
                file.ContentType,
                file.Length);
        }

        private async Task<List<RequestRecipientOptionViewModel>> GetTargetUsersWithDetails(IdentityUser currentUser, IList<string> userRoles)
        {
            var users = new List<RequestRecipientOptionViewModel>();

            if (userRoles.Contains("SuperAdmin"))
            {
                var allUsers = await _userManager.Users
                    .Where(u => u.Id != currentUser.Id)
                    .OrderBy(u => u.Email)
                    .ToListAsync();

                var userIds = allUsers.Select(u => u.Id).ToList();
                var usersData = await GetUsersFullNamesAsync(userIds);

                foreach (var u in allUsers)
                {
                    var roles = await _userManager.GetRolesAsync(u);
                    if (!roles.Any(r => _targetRoles.Contains(r)))
                        continue;

                    var fullName = usersData.GetValueOrDefault(u.Id) ?? u.Id;
                    users.Add(new RequestRecipientOptionViewModel
                    {
                        Id = u.Id,
                        Name = fullName,
                        RoleText = GetRolesDisplayName(roles),
                        Category = GetRecipientCategory(roles)
                    });
                }
            }
            else if (userRoles.Any(r => _adminRoles.Contains(r) || _managerRoles.Contains(r)))
            {
                var scopedTargets = await _userManager.Users
                    .Where(u => u.Id != currentUser.Id)
                    .ToListAsync();

                var userIds = scopedTargets.Select(u => u.Id).ToList();
                var usersData = await GetUsersFullNamesAsync(userIds);

                foreach (var u in scopedTargets)
                {
                    var roles = await _userManager.GetRolesAsync(u);
                    if (!roles.Any(r => _targetRoles.Contains(r)) || !await CanSendTo(currentUser, userRoles, u))
                        continue;

                    var fullName = usersData.GetValueOrDefault(u.Id) ?? u.Id;
                    users.Add(new RequestRecipientOptionViewModel
                    {
                        Id = u.Id,
                        Name = fullName,
                        RoleText = GetRolesDisplayName(roles),
                        Category = GetRecipientCategory(roles)
                    });
                }
            }
            else if (userRoles.Contains("فرد"))
            {
                var targets = await _userManager.Users
                    .Where(u => u.Id != currentUser.Id)
                    .ToListAsync();
                var userIds = targets.Select(a => a.Id).ToList();
                var usersData = await GetUsersFullNamesAsync(userIds);

                foreach (var a in targets)
                {
                    var roles = await _userManager.GetRolesAsync(a);
                    if (!roles.Any(r => r is "SuperAdmin" or "Admin" or "DistrictAdmin" or "Manager" or "AssistantManager"))
                        continue;

                    var fullName = usersData.GetValueOrDefault(a.Id) ?? a.Id;
                    users.Add(new RequestRecipientOptionViewModel
                    {
                        Id = a.Id,
                        Name = fullName,
                        RoleText = GetRolesDisplayName(roles),
                        Category = GetRecipientCategory(roles)
                    });
                }
            }

            return users
                .OrderBy(u => u.Category)
                .ThenBy(u => u.Name)
                .ToList();
        }
    }
}
