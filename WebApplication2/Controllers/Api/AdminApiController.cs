using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;
using WebApplication2.Models.Request;
using WebApplication2.Services;

namespace WebApplication2.Controllers.Api
{
    [Route("api/admin")]
    [ApiController]
    [Authorize(Roles = clsRoles.SuperAdmin + "," + clsRoles.Admin + "," + clsRoles.DistrictAdmin)]
    public class AdminApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly INotificationService _notificationService;
        private readonly ILogger<AdminApiController> _logger;

        public AdminApiController(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            INotificationService notificationService,
            ILogger<AdminApiController> logger)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
            _logger = logger;
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> Dashboard()
        {
            var visibleUserIds = await GetVisibleUserIdsAsync();
            var profiles = await _context.Identifies
                .Where(i => visibleUserIds.Contains(i.UserId))
                .ToListAsync();

            var users = await _userManager.Users
                .Where(u => visibleUserIds.Contains(u.Id))
                .ToListAsync();

            var promotedCount = profiles.Count(p => p.IsPromoted);
            var promotionRequestsCount = profiles.Count(p => p.RequestedPromotion && !p.IsPromoted);
            var pendingBasicInfoCount = profiles.Count(p => !p.IsBasicInfoApproved);

            return Ok(new
            {
                success = true,
                data = new
                {
                    totalUsers = users.Count,
                    totalProfiles = profiles.Count,
                    promotedCount,
                    promotionRequestsCount,
                    pendingBasicInfoCount,
                    maleCount = profiles.Count(p => p.Gender == "ذكر"),
                    femaleCount = profiles.Count(p => p.Gender == "أنثى"),
                    completedProfiles = profiles.Count(IsBasicInfoComplete),
                    incompleteProfiles = profiles.Count(p => !IsBasicInfoComplete(p)),
                    usersByGovernorate = profiles
                        .GroupBy(p => p.WorkGovernorate ?? "غير محدد")
                        .Select(g => new { governorate = g.Key, count = g.Count() })
                        .OrderByDescending(g => g.count)
                        .ToList()
                }
            });
        }

        [HttpGet("users")]
        public async Task<IActionResult> Users(
            [FromQuery] string? search = null,
            [FromQuery] string? role = null,
            [FromQuery] string? governorate = null,
            [FromQuery] bool? promoted = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var visibleUserIds = await GetVisibleUserIdsAsync();
            var users = await _userManager.Users
                .Where(u => visibleUserIds.Contains(u.Id))
                .ToListAsync();

            var userRows = new List<object>();

            foreach (var user in users)
            {
                var profile = await _context.Identifies.FirstOrDefaultAsync(i => i.UserId == user.Id);
                var address = await _context.Addresses.FirstOrDefaultAsync(a => a.UserId == user.Id);
                var roles = await _userManager.GetRolesAsync(user);
                var isLocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.Now;

                if (!string.IsNullOrWhiteSpace(search) &&
                    !Contains(user.Email, search) &&
                    !Contains(profile?.FullName, search) &&
                    !Contains(profile?.PhoneNumber, search))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(role) &&
                    !roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(governorate) &&
                    !string.Equals(profile?.WorkGovernorate, governorate, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(address?.Governorate, governorate, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (promoted.HasValue && profile?.IsPromoted != promoted.Value)
                {
                    continue;
                }

                userRows.Add(new
                {
                    user.Id,
                    user.Email,
                    user.PhoneNumber,
                    roles,
                    isActive = !isLocked,
                    profile = profile == null ? null : new
                    {
                        profile.Id,
                        profile.FullName,
                        profile.Gender,
                        profile.WorkGovernorate,
                        profile.WorkDistrict,
                        profile.ManagedGovernorate,
                        profile.ManagedDistrict,
                        profile.AccountType,
                        profile.IsPromoted,
                        profile.RequestedPromotion,
                        profile.RejectionReason,
                        profile.IsBasicInfoApproved,
                        hasCompleteProfile = IsBasicInfoComplete(profile)
                    },
                    address = address == null ? null : new
                    {
                        address.Governorate,
                        address.District,
                        address.Area
                    }
                });
            }

            var totalCount = userRows.Count;
            var data = userRows
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Ok(new
            {
                success = true,
                data,
                pagination = new
                {
                    page,
                    pageSize,
                    totalCount,
                    totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                }
            });
        }

        [HttpGet("users/{id}")]
        public async Task<IActionResult> Details(string id)
        {
            if (!await CanAccessUserAsync(id))
            {
                return Forbid();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound(new { success = false, message = "المستخدم غير موجود" });
            }

            var profile = await _context.Identifies.FirstOrDefaultAsync(i => i.UserId == id);
            var address = await _context.Addresses.FirstOrDefaultAsync(a => a.UserId == id);
            var voterCard = await _context.VoterCards.FirstOrDefaultAsync(v => v.UserId == id);
            var roles = await _userManager.GetRolesAsync(user);

            return Ok(new
            {
                success = true,
                data = new
                {
                    user.Id,
                    user.Email,
                    user.PhoneNumber,
                    roles,
                    profile,
                    address,
                    voterCard
                }
            });
        }

        [HttpGet("promotion-requests")]
        public async Task<IActionResult> PromotionRequests([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var visibleUserIds = await GetVisibleUserIdsAsync();
            var query = _context.Identifies
                .Where(i => visibleUserIds.Contains(i.UserId) && i.RequestedPromotion && !i.IsPromoted)
                .OrderByDescending(i => i.RequestedPromotionDate ?? i.CreatedAt);

            var totalCount = await query.CountAsync();
            var data = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(i => new
                {
                    i.Id,
                    i.UserId,
                    i.FullName,
                    i.Email,
                    i.PhoneNumber,
                    i.WorkGovernorate,
                    i.WorkDistrict,
                    i.RequestedPromotionDate,
                    i.PromotionRequestNotes,
                    i.RejectionReason
                })
                .ToListAsync();

            return OkPaged(data, page, pageSize, totalCount);
        }

        [HttpPost("promotion-requests/{profileId:int}/approve")]
        public async Task<IActionResult> ApprovePromotion(int profileId)
        {
            var profile = await _context.Identifies.FindAsync(profileId);
            if (profile == null)
            {
                return NotFound(new { success = false, message = "الطلب غير موجود" });
            }

            if (!await CanAccessUserAsync(profile.UserId))
            {
                return Forbid();
            }

            profile.IsPromoted = true;
            profile.RequestedPromotion = false;
            profile.PromotionDate = DateTime.Now;
            profile.PromotedBy = _userManager.GetUserId(User);
            profile.RejectionReason = null;

            var user = await _userManager.FindByIdAsync(profile.UserId);
            if (user != null && !await _userManager.IsInRoleAsync(user, clsRoles.Member))
            {
                await _userManager.AddToRoleAsync(user, clsRoles.Member);
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "تمت الموافقة على طلب الترقية" });
        }

        [HttpPost("promotion-requests/{profileId:int}/reject")]
        public async Task<IActionResult> RejectPromotion(int profileId, [FromBody] RejectAdminRequest request)
        {
            var profile = await _context.Identifies.FindAsync(profileId);
            if (profile == null)
            {
                return NotFound(new { success = false, message = "الطلب غير موجود" });
            }

            if (!await CanAccessUserAsync(profile.UserId))
            {
                return Forbid();
            }

            profile.RequestedPromotion = false;
            profile.RejectionReason = string.IsNullOrWhiteSpace(request.Reason)
                ? "تم رفض الطلب"
                : request.Reason;

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "تم رفض طلب الترقية" });
        }

        [HttpGet("pending-basic-info")]
        public async Task<IActionResult> PendingBasicInfo([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var visibleUserIds = await GetVisibleUserIdsAsync();
            var query = _context.Identifies
                .Where(i => visibleUserIds.Contains(i.UserId) && !i.IsBasicInfoApproved)
                .OrderByDescending(i => i.CreatedAt);

            var totalCount = await query.CountAsync();
            var data = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(i => new
                {
                    i.Id,
                    i.UserId,
                    i.FullName,
                    i.Email,
                    i.PhoneNumber,
                    i.WorkGovernorate,
                    i.WorkDistrict,
                    i.CreatedAt,
                    i.BasicInfoRejectionReason
                })
                .ToListAsync();

            return OkPaged(data, page, pageSize, totalCount);
        }

        [HttpPost("basic-info/{profileId:int}/approve")]
        public async Task<IActionResult> ApproveBasicInfo(int profileId)
        {
            var profile = await _context.Identifies.FindAsync(profileId);
            if (profile == null)
            {
                return NotFound(new { success = false, message = "البيانات غير موجودة" });
            }

            if (!await CanAccessUserAsync(profile.UserId))
            {
                return Forbid();
            }

            profile.IsBasicInfoApproved = true;
            profile.BasicInfoApprovalDate = DateTime.Now;
            profile.BasicInfoApprovedBy = _userManager.GetUserId(User);
            profile.BasicInfoRejectionReason = null;

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "تم اعتماد البيانات الأساسية" });
        }

        [HttpPost("basic-info/{profileId:int}/reject")]
        public async Task<IActionResult> RejectBasicInfo(int profileId, [FromBody] RejectAdminRequest request)
        {
            var profile = await _context.Identifies.FindAsync(profileId);
            if (profile == null)
            {
                return NotFound(new { success = false, message = "البيانات غير موجودة" });
            }

            if (!await CanAccessUserAsync(profile.UserId))
            {
                return Forbid();
            }

            profile.IsBasicInfoApproved = false;
            profile.BasicInfoRejectionReason = string.IsNullOrWhiteSpace(request.Reason)
                ? "تم رفض البيانات"
                : request.Reason;

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "تم رفض البيانات الأساسية" });
        }

        [HttpPost("users/{id}/toggle-status")]
        public async Task<IActionResult> ToggleUserStatus(string id)
        {
            if (!await CanAccessUserAsync(id))
            {
                return Forbid();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound(new { success = false, message = "المستخدم غير موجود" });
            }

            var isLocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.Now;
            user.LockoutEnabled = true;
            user.LockoutEnd = isLocked ? null : DateTimeOffset.MaxValue;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                return BadRequest(new { success = false, errors = result.Errors.Select(e => e.Description) });
            }

            return Ok(new
            {
                success = true,
                message = isLocked ? "تم تفعيل المستخدم" : "تم تعطيل المستخدم",
                isActive = isLocked
            });
        }

        [HttpPut("users/{id}/roles")]
        public async Task<IActionResult> UpdateUserRoles(string id, [FromBody] AdminUpdateRolesRequest request)
        {
            if (!await CanAccessUserAsync(id))
            {
                return Forbid();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound(new { success = false, message = "المستخدم غير موجود" });
            }

            var allowedRoles = GetAssignableRoles();
            var requestedRoles = request.Roles
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .Distinct()
                .ToList();

            if (requestedRoles.Count == 0)
            {
                return BadRequest(new { success = false, message = "يجب تحديد دور واحد على الأقل" });
            }

            if (requestedRoles.Any(role => !allowedRoles.Contains(role)))
            {
                return Forbid();
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
            {
                return BadRequest(new { success = false, errors = removeResult.Errors.Select(e => e.Description) });
            }

            var addResult = await _userManager.AddToRolesAsync(user, requestedRoles);
            if (!addResult.Succeeded)
            {
                return BadRequest(new { success = false, errors = addResult.Errors.Select(e => e.Description) });
            }

            return Ok(new { success = true, message = "تم تحديث أدوار المستخدم", roles = requestedRoles });
        }

        [HttpPut("users/{id}")]
        public async Task<IActionResult> EditUser(string id, [FromBody] AdminEditUserRequest request)
        {
            if (!await CanAccessUserAsync(id))
            {
                return Forbid();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound(new { success = false, message = "المستخدم غير موجود" });
            }

            if (!string.IsNullOrWhiteSpace(request.Email) && request.Email != user.Email)
            {
                var existingEmailUser = await _userManager.FindByEmailAsync(request.Email);
                if (existingEmailUser != null && existingEmailUser.Id != user.Id)
                {
                    return Conflict(new { success = false, message = "البريد الإلكتروني مستخدم من حساب آخر" });
                }

                user.Email = request.Email;
                user.UserName = request.Email;
                user.EmailConfirmed = true;
            }

            if (request.PhoneNumber != null)
            {
                user.PhoneNumber = request.PhoneNumber;
            }

            var updateUserResult = await _userManager.UpdateAsync(user);
            if (!updateUserResult.Succeeded)
            {
                return BadRequest(new { success = false, errors = updateUserResult.Errors.Select(e => e.Description) });
            }

            var profile = await _context.Identifies.FirstOrDefaultAsync(i => i.UserId == id);
            if (profile == null)
            {
                profile = new Identify
                {
                    UserId = id,
                    Email = user.Email ?? string.Empty,
                    Date = DateTime.Now,
                    identityDate = DateTime.Now,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Identifies.Add(profile);
            }

            profile.Email = user.Email ?? profile.Email;
            if (request.FullName != null) profile.FullName = request.FullName;
            if (request.MotherName != null) profile.MotherName = request.MotherName;
            if (request.Gender != null) profile.Gender = request.Gender;
            if (request.PhoneNumber != null) profile.PhoneNumber = request.PhoneNumber;
            if (request.IdentityCardNumber != null) profile.IdentityCardN = request.IdentityCardNumber;
            if (request.WorkGovernorate != null) profile.WorkGovernorate = request.WorkGovernorate;
            if (request.WorkDistrict != null) profile.WorkDistrict = request.WorkDistrict;
            if (request.ManagedGovernorate != null) profile.ManagedGovernorate = request.ManagedGovernorate;
            if (request.ManagedDistrict != null) profile.ManagedDistrict = request.ManagedDistrict;
            if (request.AccountType != null) profile.AccountType = request.AccountType;
            if (request.JobTitle != null) profile.JobTitle = request.JobTitle;
            if (request.JobGrade != null) profile.JobGrade = request.JobGrade;
            if (request.Education != null) profile.Education = request.Education;
            if (request.StudyStage != null) profile.StudyStage = request.StudyStage;
            if (request.LastName != null) profile.LastName = request.LastName;
            if (request.DateOfBirth.HasValue) profile.Date = request.DateOfBirth.Value;
            if (request.MaritalStatus != null) profile.MaritalStatus = request.MaritalStatus;
            if (request.Specialization != null) profile.Specialization = request.Specialization;
            if (request.UniversityType != null) profile.UniversityType = request.UniversityType;
            if (request.InstitutionType != null) profile.InstitutionType = request.InstitutionType;
            if (request.InstitutionName != null) profile.InstitutionName = request.InstitutionName;
            if (request.FacultyDepartment != null) profile.FacultyDepartment = request.FacultyDepartment;
            if (request.StudyType != null) profile.StudyType = request.StudyType;
            if (request.Work != null) profile.Work = request.Work;
            if (request.Ministry != null) profile.Ministry = request.Ministry;
            if (request.Department != null) profile.Department = request.Department;
            if (request.Position != null) profile.Position = request.Position;
            if (request.EmploymentStatus != null) profile.EmploymentStatus = request.EmploymentStatus;
            if (request.WhatsAppNumber != null) profile.WhatsAppNumber = request.WhatsAppNumber;
            if (request.IsWhatsAppVerified.HasValue) profile.IsWhatsAppVerified = request.IsWhatsAppVerified.Value;
            if (request.WhatsAppVerifiedAt.HasValue) profile.WhatsAppVerifiedAt = request.WhatsAppVerifiedAt.Value;
            if (request.IdentityDate.HasValue) profile.identityDate = request.IdentityDate.Value;
            if (request.IsPromoted.HasValue) profile.IsPromoted = request.IsPromoted.Value;
            if (request.PromotionDate.HasValue) profile.PromotionDate = request.PromotionDate.Value;
            if (request.PromotedBy != null) profile.PromotedBy = request.PromotedBy;
            if (request.RequestedPromotion.HasValue) profile.RequestedPromotion = request.RequestedPromotion.Value;
            if (request.RequestedPromotionDate.HasValue) profile.RequestedPromotionDate = request.RequestedPromotionDate.Value;
            if (request.PromotionRequestNotes != null) profile.PromotionRequestNotes = request.PromotionRequestNotes;
            if (request.RejectionReason != null) profile.RejectionReason = request.RejectionReason;

            await UpdateAddressAsync(id, request.Address);
            await UpdateWorkLocationAsync(profile, request.WorkLocation);
            await UpdateVoterCardAsync(id, request.Documents);
            await UpdateAffiliationAsync(id, request);
            await UpdateMembershipsAsync(id, request.Memberships);

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "تم تحديث المستخدم بنجاح" });
        }

        [HttpPost("users/{id}/cover-image")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadCoverImage(string id, [FromForm] AdminCoverImageUploadRequest request)
        {
            if (!await CanAccessUserAsync(id))
            {
                return Forbid();
            }

            if (request.CoverImageFile == null || request.CoverImageFile.Length == 0)
            {
                return BadRequest(new { success = false, message = "صورة الغلاف مطلوبة" });
            }

            var profile = await _context.Identifies.FirstOrDefaultAsync(i => i.UserId == id);
            if (profile == null)
            {
                return NotFound(new { success = false, message = "البيانات غير موجودة" });
            }

            var coverImagePath = await SaveCoverImageAsync(request.CoverImageFile);
            profile.CoverImage = coverImagePath;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "تم تحديث صورة الغلاف بنجاح",
                coverImage = coverImagePath
            });
        }

        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            if (!await CanAccessUserAsync(id))
            {
                return Forbid();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound(new { success = false, message = "المستخدم غير موجود" });
            }

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                return BadRequest(new { success = false, errors = result.Errors.Select(e => e.Description) });
            }

            return Ok(new { success = true, message = "تم حذف المستخدم بنجاح" });
        }

        [HttpPost("users/bulk-delete")]
        public async Task<IActionResult> BulkDeleteUsers([FromBody] BulkDeleteUsersRequest request)
        {
            if (request.UserIds.Count == 0)
            {
                return BadRequest(new { success = false, message = "لم يتم تحديد مستخدمين للحذف" });
            }

            var deletedCount = 0;
            var errors = new List<string>();

            foreach (var userId in request.UserIds.Distinct())
            {
                if (!await CanAccessUserAsync(userId))
                {
                    errors.Add($"لا يمكن الوصول إلى المستخدم {userId}");
                    continue;
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    errors.Add($"المستخدم غير موجود: {userId}");
                    continue;
                }

                var result = await _userManager.DeleteAsync(user);
                if (result.Succeeded)
                {
                    deletedCount++;
                    continue;
                }

                errors.AddRange(result.Errors.Select(e => $"{userId}: {e.Description}"));
            }

            return Ok(new
            {
                success = errors.Count == 0,
                message = $"تم حذف {deletedCount} مستخدم",
                deletedCount,
                errors
            });
        }

        [HttpPost("send-notification")]
        public async Task<IActionResult> SendNotification([FromBody] AdminSendNotificationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return BadRequest(new { success = false, message = "عنوان الإشعار مطلوب" });
            }

            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { success = false, message = "نص الإشعار مطلوب" });
            }

            if (!string.IsNullOrWhiteSpace(request.TargetUserId) && !await CanAccessUserAsync(request.TargetUserId))
            {
                return Forbid();
            }

            var notification = await _notificationService.CreateNotification(
                request.Title,
                request.Message,
                request.TargetUserId,
                request.Icon,
                request.ClickUrl);

            return Ok(new
            {
                success = true,
                message = "تم إرسال الإشعار بنجاح إلى المستخدمين المحددين",
                notificationId = notification.Id
            });
        }

        [HttpGet("request-history")]
        public async Task<IActionResult> RequestHistory(
            [FromQuery] string? search = null,
            [FromQuery] RequestStatus? status = null,
            [FromQuery] RequestType? type = null,
            [FromQuery] string? senderId = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var visibleUserIds = await GetVisibleUserIdsAsync();
            var query = _context.Requests
                .AsNoTracking()
                .Where(r => visibleUserIds.Contains(r.SenderId));

            if (!string.IsNullOrWhiteSpace(senderId))
            {
                query = query.Where(r => r.SenderId == senderId);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(r => r.Title.Contains(search) || r.Content.Contains(search));
            }

            if (status.HasValue)
            {
                query = query.Where(r => r.Status == status.Value);
            }

            if (type.HasValue)
            {
                query = query.Where(r => r.Type == type.Value);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(r => r.CreatedAt >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(r => r.CreatedAt <= toDate.Value);
            }

            var totalCount = await query.CountAsync();
            var requests = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var data = new List<object>();
            foreach (var request in requests)
            {
                var sender = await _userManager.FindByIdAsync(request.SenderId);
                var senderProfile = await _context.Identifies
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.UserId == request.SenderId);

                data.Add(new
                {
                    request.Id,
                    request.Title,
                    request.Content,
                    request.Status,
                    statusName = request.Status.ToString(),
                    request.Type,
                    typeName = request.Type.ToString(),
                    request.Priority,
                    priorityName = request.Priority.ToString(),
                    request.CreatedAt,
                    request.UpdatedAt,
                    request.ProcessedAt,
                    request.SenderId,
                    senderEmail = sender?.Email,
                    senderName = senderProfile?.FullName,
                    senderGovernorate = senderProfile?.WorkGovernorate,
                    request.ProcessedById,
                    request.AdminResponse,
                    request.Notes,
                    request.AttachmentFileName,
                    request.AttachmentContentType,
                    request.AttachmentSize
                });
            }

            return OkPaged(data, page, pageSize, totalCount);
        }

        [HttpGet("administrative-managers")]
        public async Task<IActionResult> AdministrativeManagers()
        {
            var visibleUserIds = await GetVisibleUserIdsAsync();
            var users = await _userManager.Users
                .Where(u => visibleUserIds.Contains(u.Id))
                .ToListAsync();

            var data = new List<object>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (!roles.Contains(clsRoles.Manager) && !roles.Contains(clsRoles.AssistantManager))
                {
                    continue;
                }

                var profile = await _context.Identifies.FirstOrDefaultAsync(i => i.UserId == user.Id);
                data.Add(new
                {
                    user.Id,
                    user.Email,
                    roles,
                    profile?.FullName,
                    profile?.WorkGovernorate,
                    profile?.WorkDistrict,
                    profile?.ManagedGovernorate,
                    profile?.ManagedDistrict
                });
            }

            return Ok(new { success = true, data });
        }

        [HttpGet("affiliation/divisions")]
        public async Task<IActionResult> GetDivisionsByEntityName([FromQuery] string entityName)
        {
            if (string.IsNullOrWhiteSpace(entityName))
            {
                return Ok(new { success = true, data = new List<object>() });
            }

            var entity = await _context.AffiliationEntities.FirstOrDefaultAsync(e => e.Name == entityName);
            if (entity == null)
            {
                return Ok(new { success = true, data = new List<object>() });
            }

            var data = await _context.Divisions
                .Where(d => d.AffiliationEntityId == entity.Id)
                .Select(d => new { id = d.Id, name = d.Name })
                .ToListAsync();

            return Ok(new { success = true, data });
        }

        [HttpGet("affiliation/sections")]
        public async Task<IActionResult> GetSectionsByDivisionName([FromQuery] string divisionName)
        {
            if (string.IsNullOrWhiteSpace(divisionName))
            {
                return Ok(new { success = true, data = new List<object>() });
            }

            var division = await _context.Divisions.FirstOrDefaultAsync(d => d.Name == divisionName);
            if (division == null)
            {
                return Ok(new { success = true, data = new List<object>() });
            }

            var data = await _context.Sections
                .Where(s => s.DivisionId == division.Id)
                .Select(s => new { id = s.Id, name = s.Name })
                .ToListAsync();

            return Ok(new { success = true, data });
        }

        [HttpGet("affiliation/groups")]
        public async Task<IActionResult> GetGroupsBySectionName([FromQuery] string sectionName)
        {
            if (string.IsNullOrWhiteSpace(sectionName))
            {
                return Ok(new { success = true, data = new List<object>() });
            }

            var section = await _context.Sections.FirstOrDefaultAsync(s => s.Name == sectionName);
            if (section == null)
            {
                return Ok(new { success = true, data = new List<object>() });
            }

            var data = await _context.Groups
                .Where(g => g.SectionId == section.Id)
                .Select(g => new { id = g.Id, name = g.Name })
                .ToListAsync();

            return Ok(new { success = true, data });
        }

        [HttpGet("export/users.csv")]
        [HttpGet("export/users.xlsx")]
        public async Task<IActionResult> ExportUsersCsv()
        {
            var visibleUserIds = await GetVisibleUserIdsAsync();
            var users = await _userManager.Users
                .Where(u => visibleUserIds.Contains(u.Id))
                .ToListAsync();

            var rows = new List<string[]>
            {
                new[] { "UserId", "Email", "PhoneNumber", "FullName", "Roles", "WorkGovernorate", "WorkDistrict", "IsPromoted", "IsBasicInfoApproved" }
            };

            foreach (var user in users)
            {
                var profile = await _context.Identifies.FirstOrDefaultAsync(i => i.UserId == user.Id);
                var roles = await _userManager.GetRolesAsync(user);

                rows.Add(new[]
                {
                    user.Id,
                    user.Email ?? string.Empty,
                    user.PhoneNumber ?? string.Empty,
                    profile?.FullName ?? string.Empty,
                    string.Join("|", roles),
                    profile?.WorkGovernorate ?? string.Empty,
                    profile?.WorkDistrict ?? string.Empty,
                    profile?.IsPromoted.ToString() ?? string.Empty,
                    profile?.IsBasicInfoApproved.ToString() ?? string.Empty
                });
            }

            return ExcelFile(rows, $"All_Users_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        }

        [HttpGet("export/promotion-requests.csv")]
        [HttpGet("export/promotion-requests.xlsx")]
        public async Task<IActionResult> ExportPromotionRequestsCsv()
        {
            var visibleUserIds = await GetVisibleUserIdsAsync();
            var requests = await _context.Identifies
                .Where(i => visibleUserIds.Contains(i.UserId) && i.RequestedPromotion && !i.IsPromoted)
                .OrderByDescending(i => i.RequestedPromotionDate ?? i.CreatedAt)
                .ToListAsync();

            var rows = new List<string[]>
            {
                new[] { "ProfileId", "UserId", "FullName", "Email", "PhoneNumber", "WorkGovernorate", "WorkDistrict", "RequestedPromotionDate", "Notes" }
            };

            foreach (var request in requests)
            {
                rows.Add(new[]
                {
                    request.Id.ToString(),
                    request.UserId,
                    request.FullName,
                    request.Email,
                    request.PhoneNumber,
                    request.WorkGovernorate ?? string.Empty,
                    request.WorkDistrict ?? string.Empty,
                    request.RequestedPromotionDate?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty,
                    request.PromotionRequestNotes ?? string.Empty
                });
            }

            return ExcelFile(rows, $"PromotionRequests_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        }

        [HttpGet("export/members.csv")]
        [HttpGet("export/members.xlsx")]
        public async Task<IActionResult> ExportMembersCsv()
        {
            var rows = await BuildUsersExportRowsAsync(async user =>
                await _userManager.IsInRoleAsync(user, clsRoles.Member));

            return ExcelFile(rows, $"Members_Only_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        }

        [HttpGet("export/students.csv")]
        [HttpGet("export/students.xlsx")]
        public async Task<IActionResult> ExportStudentsCsv()
        {
            var rows = await BuildUsersExportRowsAsync(user => Task.FromResult(false), includeByProfile: profile =>
                !string.IsNullOrWhiteSpace(profile.UniversityType) ||
                !string.IsNullOrWhiteSpace(profile.InstitutionType) ||
                !string.IsNullOrWhiteSpace(profile.InstitutionName) ||
                !string.IsNullOrWhiteSpace(profile.StudyStage));

            return ExcelFile(rows, $"Students_Only_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        }

        [HttpGet("export/administrative-managers.csv")]
        [HttpGet("export/administrative-managers.xlsx")]
        public async Task<IActionResult> ExportAdministrativeManagersCsv()
        {
            var rows = await BuildUsersExportRowsAsync(async user =>
                await _userManager.IsInRoleAsync(user, clsRoles.Manager) ||
                await _userManager.IsInRoleAsync(user, clsRoles.AssistantManager));

            return ExcelFile(rows, $"Administrative_Managers_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        }

        private async Task<List<string>> GetVisibleUserIdsAsync()
        {
            if (User.IsInRole(clsRoles.SuperAdmin))
            {
                return await _userManager.Users.Select(u => u.Id).ToListAsync();
            }

            var currentUserId = _userManager.GetUserId(User);
            var currentProfile = await _context.Identifies
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.UserId == currentUserId);

            var adminGovernorate = currentProfile?.ManagedGovernorate ?? currentProfile?.WorkGovernorate;
            var adminDistrict = currentProfile?.ManagedDistrict ?? currentProfile?.WorkDistrict;
            var hasDistrictScope = !string.IsNullOrWhiteSpace(adminDistrict);

            if (string.IsNullOrWhiteSpace(adminGovernorate))
            {
                return new List<string>();
            }

            var allUsers = await _userManager.Users.ToListAsync();
            var allProfiles = await _context.Identifies.AsNoTracking().ToListAsync();
            var profileIds = allProfiles.Select(i => i.Id).ToHashSet();
            var workLocations = await _context.WorkLocations
                .AsNoTracking()
                .Where(w => profileIds.Contains(w.IdentifyId))
                .ToListAsync();

            var workLocationByProfileId = workLocations
                .GroupBy(w => w.IdentifyId)
                .ToDictionary(g => g.Key, g => g.First());

            var visibleUserIds = new List<string>();

            foreach (var user in allUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains(clsRoles.SuperAdmin) || roles.Contains(clsRoles.Admin))
                {
                    continue;
                }

                var profile = allProfiles.FirstOrDefault(i => i.UserId == user.Id);
                if (profile == null)
                {
                    continue;
                }

                if (roles.Contains(clsRoles.DistrictAdmin))
                {
                    if (!IsGovernorateInManagedScope(profile.ManagedGovernorate, adminGovernorate))
                    {
                        continue;
                    }

                    if (hasDistrictScope && profile.ManagedDistrict != adminDistrict)
                    {
                        continue;
                    }

                    visibleUserIds.Add(user.Id);
                    continue;
                }

                workLocationByProfileId.TryGetValue(profile.Id, out var workLocation);
                var userGovernorate = GetEffectiveGovernorate(profile, workLocation);
                var userDistrict = GetEffectiveDistrict(profile, workLocation);

                if (!IsGovernorateInManagedScope(userGovernorate, adminGovernorate))
                {
                    continue;
                }

                if (hasDistrictScope && userDistrict != adminDistrict)
                {
                    continue;
                }

                visibleUserIds.Add(user.Id);
            }

            return visibleUserIds.Distinct().ToList();
        }

        private static bool IsGovernorateInManagedScope(string? governorate, string? managedGovernorate)
        {
            if (string.IsNullOrWhiteSpace(governorate) || string.IsNullOrWhiteSpace(managedGovernorate))
            {
                return false;
            }

            var current = governorate.Trim();
            var managed = managedGovernorate.Trim();

            if (string.Equals(current, managed, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return managed == "بغداد عامة" &&
                   (current == "بغداد" || current.StartsWith("بغداد -", StringComparison.OrdinalIgnoreCase));
        }

        private static string GetEffectiveGovernorate(Identify profile, WorkLocation? workLocation)
        {
            if (!string.IsNullOrWhiteSpace(workLocation?.Governorate))
            {
                return workLocation.Governorate;
            }

            if (!string.IsNullOrWhiteSpace(profile.WorkGovernorate))
            {
                return profile.WorkGovernorate;
            }

            return string.Empty;
        }

        private static string GetEffectiveDistrict(Identify profile, WorkLocation? workLocation)
        {
            if (!string.IsNullOrWhiteSpace(workLocation?.Governorate) && workLocation.Governorate == "بغداد")
            {
                return workLocation.District ?? string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(profile.WorkGovernorate) && profile.WorkGovernorate == "بغداد")
            {
                return profile.WorkDistrict ?? string.Empty;
            }

            return string.Empty;
        }

        private static async Task<string> SaveCoverImageAsync(IFormFile coverImageFile)
        {
            var uploadsFolder = Path.Combine("C:\\Users", "Public", "MyApp_Uploads", "Profiles");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = Guid.NewGuid() + "_" + Path.GetFileName(coverImageFile.FileName);
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await coverImageFile.CopyToAsync(fileStream);
            }

            return "/MyApp_Uploads/Profiles/" + uniqueFileName;
        }

        private async Task UpdateAddressAsync(string userId, AdminAddressRequest? request)
        {
            if (request == null)
            {
                return;
            }

            var address = await _context.Addresses.FirstOrDefaultAsync(a => a.UserId == userId);
            if (address == null)
            {
                address = new Address { UserId = userId };
                _context.Addresses.Add(address);
            }

            if (request.Governorate != null) address.Governorate = request.Governorate;
            if (request.District != null) address.District = request.District;
            if (request.Area != null) address.Area = request.Area;
            if (request.Alley != null) address.Alley = request.Alley;
            if (request.Street != null) address.Street = request.Street;
            if (request.House != null) address.House = request.House;
            if (request.NearestPoint != null) address.NearestPoint = request.NearestPoint;
        }

        private async Task UpdateWorkLocationAsync(Identify profile, AdminWorkLocationRequest? request)
        {
            if (request == null)
            {
                return;
            }

            var workLocation = await _context.WorkLocations.FirstOrDefaultAsync(w => w.IdentifyId == profile.Id);
            if (workLocation == null)
            {
                workLocation = new WorkLocation { IdentifyId = profile.Id };
                _context.WorkLocations.Add(workLocation);
            }

            if (request.Governorate != null)
            {
                workLocation.Governorate = request.Governorate;
                profile.WorkGovernorate = request.Governorate;
            }

            if (request.District != null)
            {
                workLocation.District = request.District;
                profile.WorkDistrict = request.District;
            }
        }

        private async Task UpdateVoterCardAsync(string userId, AdminDocumentsRequest? request)
        {
            if (request == null)
            {
                return;
            }

            var voterCard = await _context.VoterCards.FirstOrDefaultAsync(v => v.UserId == userId);
            if (voterCard == null)
            {
                voterCard = new VoterCard { UserId = userId };
                _context.VoterCards.Add(voterCard);
            }

            if (request.VoterCardNumber != null) voterCard.VoterCardNumber = request.VoterCardNumber;
            if (request.PollingCenterNumber != null) voterCard.PollingCenterNumber = request.PollingCenterNumber;
        }

        private async Task UpdateAffiliationAsync(string userId, AdminEditUserRequest request)
        {
            if (request.Affiliation == null &&
                !request.AffiliationEntityId.HasValue &&
                !request.DivisionId.HasValue &&
                !request.SectionId.HasValue &&
                !request.GroupId.HasValue)
            {
                return;
            }

            var affiliation = await _context.AffiliationInfos.FirstOrDefaultAsync(a => a.UserId == userId);
            if (affiliation == null)
            {
                affiliation = new AffiliationInfo { UserId = userId };
                _context.AffiliationInfos.Add(affiliation);
            }

            affiliation.AffiliationEntityId = request.AffiliationEntityId ?? affiliation.AffiliationEntityId;
            affiliation.DivisionId = request.DivisionId ?? affiliation.DivisionId;
            affiliation.SectionId = request.SectionId ?? affiliation.SectionId;
            affiliation.GroupId = request.GroupId ?? affiliation.GroupId;

            if (request.Affiliation == null)
            {
                return;
            }

            if (request.Affiliation.AffiliationEntity != null)
            {
                affiliation.AffiliationEntityId = await FindAffiliationEntityIdAsync(request.Affiliation.AffiliationEntity);
            }

            if (request.Affiliation.Division != null)
            {
                affiliation.DivisionId = await FindDivisionIdAsync(request.Affiliation.Division);
            }

            if (request.Affiliation.Section != null)
            {
                affiliation.SectionId = await FindSectionIdAsync(request.Affiliation.Section);
            }

            if (request.Affiliation.Group != null)
            {
                affiliation.GroupId = await FindGroupIdAsync(request.Affiliation.Group);
            }

            if (request.Affiliation.MozakeName != null) affiliation.MozakeName = request.Affiliation.MozakeName;
            if (request.Affiliation.MozakePhoneNumber != null) affiliation.MozakePhoneNumber = request.Affiliation.MozakePhoneNumber;
            if (request.Affiliation.BadgeNumber != null) affiliation.BadgeNumber = request.Affiliation.BadgeNumber;
            if (request.Affiliation.AffiliationDate.HasValue) affiliation.AffiliationDate = request.Affiliation.AffiliationDate.Value;
        }

        private async Task UpdateMembershipsAsync(string userId, AdminMembershipRequest? request)
        {
            if (request == null)
            {
                return;
            }

            await UpdateUnionMembershipAsync(userId, request);
            await UpdateFederationMembershipAsync(userId, request);
            await UpdateAssociationMembershipAsync(userId, request);
            await UpdateNgoMembershipAsync(userId, request);
        }

        private async Task UpdateUnionMembershipAsync(string userId, AdminMembershipRequest request)
        {
            if (request.UnionName == null &&
                request.UnionPosition == null &&
                request.UnionIdNumber == null &&
                !request.UnionAffiliationDate.HasValue)
            {
                return;
            }

            var membership = await _context.UnionMemberships.FirstOrDefaultAsync(m => m.UserId == userId);
            if (membership == null)
            {
                membership = new UnionMembership { UserId = userId };
                _context.UnionMemberships.Add(membership);
            }

            if (request.UnionName != null) membership.UnionName = request.UnionName;
            if (request.UnionPosition != null) membership.Position = request.UnionPosition;
            if (request.UnionIdNumber != null) membership.IdNumber = request.UnionIdNumber;
            if (request.UnionAffiliationDate.HasValue) membership.AffiliationDate = request.UnionAffiliationDate.Value;
        }

        private async Task UpdateFederationMembershipAsync(string userId, AdminMembershipRequest request)
        {
            if (request.FederationName == null &&
                request.FederationDivisionName == null &&
                request.FederationSectionName == null &&
                request.FederationGroupName == null &&
                request.FederationPosition == null &&
                request.FederationIdNumber == null &&
                !request.FederationAffiliationDate.HasValue)
            {
                return;
            }

            var membership = await _context.FederationMemberships.FirstOrDefaultAsync(m => m.UserId == userId);
            if (membership == null)
            {
                membership = new FederationMembership { UserId = userId };
                _context.FederationMemberships.Add(membership);
            }

            if (request.FederationName != null) membership.FederationId = await FindFederationIdAsync(request.FederationName);
            if (request.FederationDivisionName != null) membership.FederationDivisionId = await FindFederationDivisionIdAsync(request.FederationDivisionName);
            if (request.FederationSectionName != null) membership.FederationSectionId = await FindFederationSectionIdAsync(request.FederationSectionName);
            if (request.FederationGroupName != null) membership.FederationGroupId = await FindFederationGroupIdAsync(request.FederationGroupName);
            if (request.FederationPosition != null) membership.Position = request.FederationPosition;
            if (request.FederationIdNumber != null) membership.IdNumber = request.FederationIdNumber;
            if (request.FederationAffiliationDate.HasValue) membership.AffiliationDate = request.FederationAffiliationDate.Value;
        }

        private async Task UpdateAssociationMembershipAsync(string userId, AdminMembershipRequest request)
        {
            if (request.AssociationName == null &&
                request.AssociationPosition == null &&
                request.AssociationIdNumber == null &&
                !request.AssociationAffiliationDate.HasValue)
            {
                return;
            }

            var membership = await _context.AssociationMemberships.FirstOrDefaultAsync(m => m.UserId == userId);
            if (membership == null)
            {
                membership = new AssociationMembership { UserId = userId };
                _context.AssociationMemberships.Add(membership);
            }

            if (request.AssociationName != null) membership.AssociationName = request.AssociationName;
            if (request.AssociationPosition != null) membership.Position = request.AssociationPosition;
            if (request.AssociationIdNumber != null) membership.IdNumber = request.AssociationIdNumber;
            if (request.AssociationAffiliationDate.HasValue) membership.AffiliationDate = request.AssociationAffiliationDate.Value;
        }

        private async Task UpdateNgoMembershipAsync(string userId, AdminMembershipRequest request)
        {
            if (request.NgoName == null &&
                request.NgoPosition == null &&
                request.NgoIdNumber == null &&
                !request.NgoAffiliationDate.HasValue)
            {
                return;
            }

            var membership = await _context.NgoMemberships.FirstOrDefaultAsync(m => m.UserId == userId);
            if (membership == null)
            {
                membership = new NgoMembership { UserId = userId };
                _context.NgoMemberships.Add(membership);
            }

            if (request.NgoName != null) membership.NgoName = request.NgoName;
            if (request.NgoPosition != null) membership.Position = request.NgoPosition;
            if (request.NgoIdNumber != null) membership.IdNumber = request.NgoIdNumber;
            if (request.NgoAffiliationDate.HasValue) membership.AffiliationDate = request.NgoAffiliationDate.Value;
        }

        private async Task<bool> CanAccessUserAsync(string userId)
        {
            if (User.IsInRole(clsRoles.SuperAdmin))
            {
                return true;
            }

            var visibleUserIds = await GetVisibleUserIdsAsync();
            return visibleUserIds.Contains(userId);
        }

        private static bool IsBasicInfoComplete(Identify profile)
        {
            if (string.IsNullOrWhiteSpace(profile.FullName)) return false;
            if (string.IsNullOrWhiteSpace(profile.MotherName)) return false;
            if (profile.Date == DateTime.MinValue) return false;
            if (string.IsNullOrWhiteSpace(profile.Gender)) return false;
            if (string.IsNullOrWhiteSpace(profile.PhoneNumber)) return false;
            if (string.IsNullOrWhiteSpace(profile.IdentityCardN)) return false;
            if (profile.IdentityCardN.Length != 12) return false;
            if (string.IsNullOrWhiteSpace(profile.WorkGovernorate)) return false;

            return true;
        }

        private static bool Contains(string? value, string search)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                value.Contains(search, StringComparison.OrdinalIgnoreCase);
        }

        private async Task<List<string[]>> BuildUsersExportRowsAsync(
            Func<IdentityUser, Task<bool>> includeByUser,
            Func<Identify, bool>? includeByProfile = null)
        {
            var visibleUserIds = await GetVisibleUserIdsAsync();
            var users = await _userManager.Users
                .Where(u => visibleUserIds.Contains(u.Id))
                .ToListAsync();

            var rows = new List<string[]>
            {
                new[]
                {
                    "UserId", "Email", "PhoneNumber", "FullName", "Roles",
                    "WorkGovernorate", "WorkDistrict", "Gender", "AccountType",
                    "Education", "StudyStage", "IsPromoted", "IsBasicInfoApproved"
                }
            };

            foreach (var user in users)
            {
                var profile = await _context.Identifies.FirstOrDefaultAsync(i => i.UserId == user.Id);
                var include = await includeByUser(user);

                if (!include && profile != null && includeByProfile != null)
                {
                    include = includeByProfile(profile);
                }

                if (!include)
                {
                    continue;
                }

                var roles = await _userManager.GetRolesAsync(user);
                rows.Add(new[]
                {
                    user.Id,
                    user.Email ?? string.Empty,
                    user.PhoneNumber ?? string.Empty,
                    profile?.FullName ?? string.Empty,
                    string.Join("|", roles),
                    profile?.WorkGovernorate ?? string.Empty,
                    profile?.WorkDistrict ?? string.Empty,
                    profile?.Gender ?? string.Empty,
                    profile?.AccountType ?? string.Empty,
                    profile?.Education ?? string.Empty,
                    profile?.StudyStage ?? string.Empty,
                    profile?.IsPromoted.ToString() ?? string.Empty,
                    profile?.IsBasicInfoApproved.ToString() ?? string.Empty
                });
            }

            return rows;
        }

        private IActionResult ExcelFile(List<string[]> rows, string fileName)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Users");

            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                for (var columnIndex = 0; columnIndex < rows[rowIndex].Length; columnIndex++)
                {
                    worksheet.Cell(rowIndex + 1, columnIndex + 1).Value = rows[rowIndex][columnIndex] ?? string.Empty;
                }
            }

            if (rows.Count > 0 && rows[0].Length > 0)
            {
                var range = worksheet.Range(1, 1, rows.Count, rows[0].Length);
                range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                var headerRange = worksheet.Range(1, 1, 1, rows[0].Length);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                worksheet.Columns().AdjustToContents();
            }

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        private HashSet<string> GetAssignableRoles()
        {
            if (User.IsInRole(clsRoles.SuperAdmin))
            {
                return new HashSet<string>
                {
                    clsRoles.SuperAdmin,
                    clsRoles.Admin,
                    clsRoles.DistrictAdmin,
                    clsRoles.User,
                    clsRoles.Member,
                    clsRoles.NewsEditor,
                    clsRoles.MapViewer,
                    clsRoles.Manager,
                    clsRoles.AssistantManager,
                    "ManagerViewer"
                };
            }

            return new HashSet<string> { clsRoles.User };
        }

        private async Task<int?> FindAffiliationEntityIdAsync(string name)
        {
            return (await _context.AffiliationEntities.FirstOrDefaultAsync(e => e.Name == name))?.Id;
        }

        private async Task<int?> FindDivisionIdAsync(string name)
        {
            return (await _context.Divisions.FirstOrDefaultAsync(e => e.Name == name))?.Id;
        }

        private async Task<int?> FindSectionIdAsync(string name)
        {
            return (await _context.Sections.FirstOrDefaultAsync(e => e.Name == name))?.Id;
        }

        private async Task<int?> FindGroupIdAsync(string name)
        {
            return (await _context.Groups.FirstOrDefaultAsync(e => e.Name == name))?.Id;
        }

        private async Task<int?> FindFederationIdAsync(string name)
        {
            return (await _context.Federations.FirstOrDefaultAsync(e => e.Name == name))?.Id;
        }

        private async Task<int?> FindFederationDivisionIdAsync(string name)
        {
            return (await _context.FederationDivisions.FirstOrDefaultAsync(e => e.Name == name))?.Id;
        }

        private async Task<int?> FindFederationSectionIdAsync(string name)
        {
            return (await _context.FederationSections.FirstOrDefaultAsync(e => e.Name == name))?.Id;
        }

        private async Task<int?> FindFederationGroupIdAsync(string name)
        {
            return (await _context.FederationGroups.FirstOrDefaultAsync(e => e.Name == name))?.Id;
        }

        private static IActionResult OkPaged<T>(IEnumerable<T> data, int page, int pageSize, int totalCount)
        {
            return new OkObjectResult(new
            {
                success = true,
                data,
                pagination = new
                {
                    page,
                    pageSize,
                    totalCount,
                    totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                }
            });
        }
    }

    public class RejectAdminRequest
    {
        public string? Reason { get; set; }
    }

    public class BulkDeleteUsersRequest
    {
        public List<string> UserIds { get; set; } = new();
    }

    public class AdminSendNotificationRequest
    {
        public string? Title { get; set; }
        public string? Message { get; set; }
        public string? TargetUserId { get; set; }
        public string? Icon { get; set; } = "bi-bell";
        public string? ClickUrl { get; set; }
    }

    public class AdminCoverImageUploadRequest
    {
        public IFormFile? CoverImageFile { get; set; }
    }

    public class AdminEditUserRequest
    {
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? FullName { get; set; }
        public string? LastName { get; set; }
        public string? MotherName { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Gender { get; set; }
        public string? MaritalStatus { get; set; }
        public string? IdentityCardNumber { get; set; }
        public DateTime? IdentityDate { get; set; }
        public string? WorkGovernorate { get; set; }
        public string? WorkDistrict { get; set; }
        public string? ManagedGovernorate { get; set; }
        public string? ManagedDistrict { get; set; }
        public string? AccountType { get; set; }
        public bool? IsPromoted { get; set; }
        public DateTime? PromotionDate { get; set; }
        public string? PromotedBy { get; set; }
        public bool? RequestedPromotion { get; set; }
        public DateTime? RequestedPromotionDate { get; set; }
        public string? PromotionRequestNotes { get; set; }
        public string? RejectionReason { get; set; }
        public string? JobTitle { get; set; }
        public string? JobGrade { get; set; }
        public string? Education { get; set; }
        public string? Specialization { get; set; }
        public string? StudyStage { get; set; }
        public string? UniversityType { get; set; }
        public string? InstitutionType { get; set; }
        public string? InstitutionName { get; set; }
        public string? FacultyDepartment { get; set; }
        public string? StudyType { get; set; }
        public string? Work { get; set; }
        public string? Ministry { get; set; }
        public string? Department { get; set; }
        public string? Position { get; set; }
        public string? EmploymentStatus { get; set; }
        public string? WhatsAppNumber { get; set; }
        public bool? IsWhatsAppVerified { get; set; }
        public DateTime? WhatsAppVerifiedAt { get; set; }
        public int? AffiliationEntityId { get; set; }
        public int? DivisionId { get; set; }
        public int? SectionId { get; set; }
        public int? GroupId { get; set; }
        public AdminAddressRequest? Address { get; set; }
        public AdminWorkLocationRequest? WorkLocation { get; set; }
        public AdminDocumentsRequest? Documents { get; set; }
        public AdminAffiliationRequest? Affiliation { get; set; }
        public AdminMembershipRequest? Memberships { get; set; }
    }

    public class AdminAddressRequest
    {
        public string? Governorate { get; set; }
        public string? District { get; set; }
        public string? Area { get; set; }
        public string? Alley { get; set; }
        public string? Street { get; set; }
        public string? House { get; set; }
        public string? NearestPoint { get; set; }
    }

    public class AdminWorkLocationRequest
    {
        public string? Governorate { get; set; }
        public string? District { get; set; }
    }

    public class AdminDocumentsRequest
    {
        public string? VoterCardNumber { get; set; }
        public string? PollingCenterNumber { get; set; }
    }

    public class AdminAffiliationRequest
    {
        public string? AffiliationEntity { get; set; }
        public string? Division { get; set; }
        public string? Section { get; set; }
        public string? Group { get; set; }
        public string? MozakeName { get; set; }
        public string? MozakePhoneNumber { get; set; }
        public string? BadgeNumber { get; set; }
        public DateTime? AffiliationDate { get; set; }
    }

    public class AdminMembershipRequest
    {
        public string? UnionName { get; set; }
        public string? UnionPosition { get; set; }
        public string? UnionIdNumber { get; set; }
        public DateTime? UnionAffiliationDate { get; set; }
        public string? FederationName { get; set; }
        public string? FederationDivisionName { get; set; }
        public string? FederationSectionName { get; set; }
        public string? FederationGroupName { get; set; }
        public string? FederationPosition { get; set; }
        public string? FederationIdNumber { get; set; }
        public DateTime? FederationAffiliationDate { get; set; }
        public string? AssociationName { get; set; }
        public string? AssociationPosition { get; set; }
        public string? AssociationIdNumber { get; set; }
        public DateTime? AssociationAffiliationDate { get; set; }
        public string? NgoName { get; set; }
        public string? NgoPosition { get; set; }
        public string? NgoIdNumber { get; set; }
        public DateTime? NgoAffiliationDate { get; set; }
    }

    public class AdminUpdateRolesRequest
    {
        public List<string> Roles { get; set; } = new();
    }
}

