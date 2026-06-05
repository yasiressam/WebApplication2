using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;
using WebApplication2.Models.Profile;
using WebApplication2.Services;

namespace WebApplication2.Controllers
{
    [Authorize]
    public class ManagementAssignmentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly INotificationService _notificationService;
        private readonly ILogger<ManagementAssignmentsController> _logger;

        public ManagementAssignmentsController(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            INotificationService notificationService,
            ILogger<ManagementAssignmentsController> logger)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
            _logger = logger;
        }

        private IActionResult RedirectToUsersPage()
        {
            return RedirectToAction("Users", "SuperAdmin");
        }

        // ===== الحصول على محافظة الأدمن الحالي =====
        private async Task<string?> GetCurrentAdminGovernorate()
        {
            var currentUserId = _userManager.GetUserId(User);
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return null;

            var roles = await _userManager.GetRolesAsync(currentUser);
            if (roles.Contains(clsRoles.SuperAdmin)) return null;

            if (roles.Contains(clsRoles.Admin))
            {
                var identify = await _context.Identifies
                    .FirstOrDefaultAsync(i => i.UserId == currentUserId);
                return identify?.ManagedGovernorate;
            }

            return null;
        }

        // ===== التحقق إذا كان المستخدم الحالي SuperAdmin =====
        private async Task<bool> IsCurrentUserSuperAdmin()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return false;

            var roles = await _userManager.GetRolesAsync(currentUser);
            return roles.Contains(clsRoles.SuperAdmin);
        }

        private string GetEffectiveGovernorate(Identify? identify, Address? address)
        {
            var workLocation = identify != null
                ? identify.WorkLocation ?? _context.WorkLocations.AsNoTracking().FirstOrDefault(w => w.IdentifyId == identify.Id)
                : null;

            return !string.IsNullOrWhiteSpace(workLocation?.Governorate)
                ? workLocation.Governorate
                : !string.IsNullOrWhiteSpace(identify?.WorkGovernorate)
                ? identify.WorkGovernorate
                : string.Empty;
        }

        private static bool IsGovernorateInManagedScope(string? governorate, string? managedGovernorate)
        {
            if (string.IsNullOrWhiteSpace(governorate) || string.IsNullOrWhiteSpace(managedGovernorate))
                return false;

            var current = governorate.Trim();
            var managed = managedGovernorate.Trim();

            if (string.Equals(current, managed, StringComparison.OrdinalIgnoreCase))
                return true;

            return managed == "بغداد عامة" &&
                   (current == "بغداد" || current.StartsWith("بغداد -", StringComparison.OrdinalIgnoreCase));
        }

        private string GetEffectiveDistrict(Identify? identify, Address? address)
        {
            var workLocation = identify != null
                ? identify.WorkLocation ?? _context.WorkLocations.AsNoTracking().FirstOrDefault(w => w.IdentifyId == identify.Id)
                : null;

            if (!string.IsNullOrWhiteSpace(workLocation?.Governorate) && workLocation.Governorate == "بغداد")
            {
                return workLocation.District ?? string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(identify?.WorkGovernorate) && identify.WorkGovernorate == "بغداد")
            {
                return identify.WorkDistrict ?? string.Empty;
            }

            return string.Empty;
        }

        private static string NormalizeBaghdadScope(string? district)
        {
            return string.IsNullOrWhiteSpace(district) ? "مركزي" : district;
        }

        private static string? GetAssignmentBaghdadScope(string governorate, string? district)
        {
            return governorate == "بغداد" ? NormalizeBaghdadScope(district) : null;
        }

        private async Task<string?> GetStoredAssignmentBaghdadScopeAsync(ManagementAssignment assignment)
        {
            if (assignment.Governorate != "بغداد")
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(assignment.BaghdadScope))
            {
                return NormalizeBaghdadScope(assignment.BaghdadScope);
            }

            var identify = await _context.Identifies
                .Include(i => i.WorkLocation)
                .FirstOrDefaultAsync(i => i.UserId == assignment.UserId);

            return NormalizeBaghdadScope(GetEffectiveDistrict(identify, null));
        }

        // =========================================
        // صفحة عرض الأفراد للسوبر أدمن
        // =========================================
        [Authorize(Roles = clsRoles.SuperAdmin)]
        [HttpGet]
        public async Task<IActionResult> Individuals(
            string? search = null,
            string? status = null,
            string? responsibility = null,
            string? governorate = null,
            int page = 1,
            int pageSize = 15)
        {
            var people = await (
                from identify in _context.Identifies.Include(i => i.WorkLocation)
                join user in _context.Users on identify.UserId equals user.Id
                join address in _context.Addresses
                    on identify.UserId equals address.UserId into addrJoin
                from address in addrJoin.DefaultIfEmpty()

                join aff in _context.AffiliationInfos
                    .Include(x => x.AffiliationEntity)
                    .Include(x => x.Division)
                    .Include(x => x.Section)
                    .Include(x => x.Group)
                    on identify.UserId equals aff.UserId into affJoin
                from aff in affJoin.DefaultIfEmpty()

                where identify.IsPromoted && identify.AccountType == "فرد"
                select new
                {
                    UserId = identify.UserId,
                    IdentifyId = identify.Id,
                    FullName = identify.FullName ?? "",
                    Email = user.Email ?? "",
                    Identify = identify,
                    Address = address,
                    AffiliationEntityId = aff != null ? aff.AffiliationEntityId : null,
                    DivisionId = aff != null ? aff.DivisionId : null,
                    SectionId = aff != null ? aff.SectionId : null,
                    GroupId = aff != null ? aff.GroupId : null,
                    AffiliationEntityName = aff != null && aff.AffiliationEntity != null ? aff.AffiliationEntity.Name : "",
                    DivisionName = aff != null && aff.Division != null ? aff.Division.Name : "",
                    SectionName = aff != null && aff.Section != null ? aff.Section.Name : "",
                    GroupName = aff != null && aff.Group != null ? aff.Group.Name : ""
                }
            )
            .ToListAsync();

            var model = people
                .Select(x => new AssignManagerViewModel
                {
                    UserId = x.UserId,
                    IdentifyId = x.IdentifyId,
                    FullName = x.FullName,
                    Email = x.Email,
                    Governorate = GetEffectiveGovernorate(x.Identify, x.Address),
                    District = GetEffectiveDistrict(x.Identify, x.Address),
                    AffiliationEntityId = x.AffiliationEntityId,
                    DivisionId = x.DivisionId,
                    SectionId = x.SectionId,
                    GroupId = x.GroupId,
                    AffiliationEntityName = x.AffiliationEntityName,
                    DivisionName = x.DivisionName,
                    SectionName = x.SectionName,
                    GroupName = x.GroupName,
                    AssignmentRole = "Manager",
                    IsSuperAdmin = true
                })
                .OrderBy(x => x.FullName)
                .ToList();

            var userIds = model.Select(x => x.UserId).Distinct().ToList();
            var assignments = await _context.ManagementAssignments
                .Where(x => userIds.Contains(x.UserId))
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            foreach (var item in model)
            {
                var assignment = assignments.FirstOrDefault(x => x.UserId == item.UserId);
                if (assignment != null)
                {
                    item.IsAssigned = true;
                    item.CurrentAssignmentRole = assignment.AssignmentRole;
                    item.CurrentAssignmentRoleArabic = assignment.AssignmentRole == "Assistant" ? "معاون" : "مسؤول";
                    item.CurrentManagementLevel = assignment.ManagementLevel;
                    item.CurrentManagementLevelArabic = assignment.ManagementLevel switch
                    {
                        "Division" => "قسم",
                        "Section" => "شعبة",
                        "Group" => "وحدة",
                        _ => "جهة"
                    };
                    item.CurrentAssignmentDisplayArabic = $"{item.CurrentAssignmentRoleArabic} {item.CurrentManagementLevelArabic}";
                }
                else
                {
                    item.IsAssigned = false;
                    item.CurrentAssignmentRole = string.Empty;
                    item.CurrentAssignmentRoleArabic = "غير مكلّف";
                    item.CurrentManagementLevel = string.Empty;
                    item.CurrentManagementLevelArabic = string.Empty;
                    item.CurrentAssignmentDisplayArabic = "غير مكلّف";
                }
            }

            page = Math.Max(1, page);
            pageSize = pageSize is 10 or 15 or 25 or 50 ? pageSize : 15;

            IEnumerable<AssignManagerViewModel> filteredModel = model;

            if (!string.IsNullOrWhiteSpace(search))
            {
                var normalizedSearch = search.Trim().ToLowerInvariant();
                filteredModel = filteredModel.Where(x =>
                    (x.FullName ?? string.Empty).ToLowerInvariant().Contains(normalizedSearch) ||
                    (x.Email ?? string.Empty).ToLowerInvariant().Contains(normalizedSearch));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                filteredModel = status == "assigned"
                    ? filteredModel.Where(x => x.IsAssigned)
                    : status == "unassigned"
                        ? filteredModel.Where(x => !x.IsAssigned)
                        : filteredModel;
            }

            if (!string.IsNullOrWhiteSpace(responsibility))
            {
                filteredModel = responsibility == "manager"
                    ? filteredModel.Where(x => x.CurrentAssignmentRole == "Manager")
                    : responsibility == "assistant"
                        ? filteredModel.Where(x => x.CurrentAssignmentRole == "Assistant")
                        : filteredModel;
            }

            if (!string.IsNullOrWhiteSpace(governorate))
            {
                filteredModel = filteredModel.Where(x => IsGovernorateInManagedScope(x.Governorate, governorate));
            }

            var totalItems = filteredModel.Count();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
            page = Math.Min(page, totalPages);

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;
            ViewBag.StartIndex = ((page - 1) * pageSize) + 1;
            ViewBag.Search = search ?? string.Empty;
            ViewBag.Status = status ?? string.Empty;
            ViewBag.Responsibility = responsibility ?? string.Empty;
            ViewBag.Governorate = governorate ?? string.Empty;
            ViewBag.Governorates = model
                .Select(x => x.Governorate)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            return View(filteredModel
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList());
        }

        // =========================================
        // إرسال الاستمارة إلى المستخدم مباشرة
        // =========================================
        [Authorize(Roles = clsRoles.SuperAdmin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendRequest(string userId, string assignmentRole = "Manager", string selectedLevel = "Entity")
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                TempData["ErrorMessage"] = "معرف المستخدم غير صالح";
                return RedirectToUsersPage();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "المستخدم غير موجود";
                return RedirectToUsersPage();
            }

            var identify = await _context.Identifies.FirstOrDefaultAsync(i => i.UserId == userId);
            var address = await _context.Addresses.FirstOrDefaultAsync(a => a.UserId == userId);

            var effectiveGovernorate = GetEffectiveGovernorate(identify, address);
            if (identify == null || string.IsNullOrWhiteSpace(effectiveGovernorate))
            {
                TempData["ErrorMessage"] = "بيانات المستخدم غير مكتملة";
                return RedirectToUsersPage();
            }

            if (!identify.IsPromoted || identify.AccountType != "فرد")
            {
                TempData["ErrorMessage"] = "يجب أن يكون المستخدم مصعدًا إلى فرد أولاً";
                return RedirectToUsersPage();
            }

            assignmentRole = assignmentRole == "Assistant" ? "Assistant" : "Manager";
            selectedLevel = selectedLevel switch
            {
                "Division" => "Division",
                "Section" => "Section",
                "Group" => "Group",
                _ => "Entity"
            };

            var hasOpenRequest = await _context.ManagementAssignmentRequests.AnyAsync(x =>
                x.UserId == userId &&
                x.AssignmentRole == assignmentRole &&
                x.ManagementLevel == selectedLevel &&
                (x.Status == "PendingUserResponse" || x.Status == "SubmittedToSuperAdmin"));

            if (hasOpenRequest)
            {
                TempData["ErrorMessage"] = "يوجد طلب استمارة مفتوح لهذا المستخدم مسبقًا";
                return RedirectToUsersPage();
            }

            var request = new ManagementAssignmentRequest
            {
                UserId = userId,
                RequestedByUserId = _userManager.GetUserId(User) ?? string.Empty,
                Governorate = effectiveGovernorate,
                ManagementLevel = selectedLevel,
                AssignmentRole = assignmentRole,
                Status = "PendingUserResponse",
                CreatedAt = DateTime.Now
            };

            _context.ManagementAssignmentRequests.Add(request);
            await _context.SaveChangesAsync();

            try
            {
                var roleArabic = assignmentRole == "Assistant" ? "معاون" : "مسؤول";
                var levelArabic = selectedLevel switch
                {
                    "Division" => "قسم",
                    "Section" => "شعبة",
                    "Group" => "وحدة",
                    _ => "جهة"
                };
                await _notificationService.CreateNotification(
                    "📋 استمارة تكليف إداري",
                    $"تم إرسال استمارة إليك لإكمال طلب التكليف كـ {roleArabic} {levelArabic}. يرجى تعبئة البيانات المطلوبة ثم الإرسال.",
                    userId,
                    "bi-file-earmark-text-fill",
                    $"/ManagementAssignments/RespondToRequest/{request.Id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في إرسال إشعار استمارة التكليف");
            }

            TempData["SuccessMessage"] = assignmentRole == "Assistant"
                ? "✅ تم إرسال استمارة المعاون إلى المستخدم"
                : "✅ تم إرسال استمارة المسؤول إلى المستخدم";

            return RedirectToUsersPage();
        }

        // =========================================
        // شاشة إرسال الاستمارة من السوبر أدمن
        // =========================================
        [Authorize(Roles = clsRoles.SuperAdmin)]
        [HttpGet]
        public async Task<IActionResult> Assign(string userId, string assignmentRole = "Manager")
        {
            if (string.IsNullOrWhiteSpace(userId))
                return RedirectToUsersPage();

            var adminGovernorate = await GetCurrentAdminGovernorate();
            var isSuperAdmin = await IsCurrentUserSuperAdmin();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "المستخدم غير موجود";
                return RedirectToUsersPage();
            }

            var identify = await _context.Identifies
                .FirstOrDefaultAsync(i => i.UserId == userId);

            if (identify == null)
            {
                TempData["ErrorMessage"] = "لا يوجد ملف شخصي لهذا المستخدم";
                return RedirectToUsersPage();
            }

            if (!identify.IsPromoted || identify.AccountType != "فرد")
            {
                TempData["ErrorMessage"] = "يجب أن يكون المستخدم مصعّدًا إلى فرد أولاً";
                return RedirectToUsersPage();
            }

            var address = await _context.Addresses
                .FirstOrDefaultAsync(a => a.UserId == userId);

            if (!isSuperAdmin && !string.IsNullOrEmpty(adminGovernorate))
            {
                if (!IsGovernorateInManagedScope(GetEffectiveGovernorate(identify, address), adminGovernorate))
                {
                    TempData["ErrorMessage"] = $"لا يمكنك تعيين مستخدم خارج محافظة {adminGovernorate}";
                    return RedirectToUsersPage();
                }
            }

            var affiliation = await _context.AffiliationInfos
                .Include(a => a.AffiliationEntity)
                .Include(a => a.Division)
                .Include(a => a.Section)
                .Include(a => a.Group)
                .FirstOrDefaultAsync(a => a.UserId == userId);

            var effectiveGovernorateAssign = GetEffectiveGovernorate(identify, address);
            if (string.IsNullOrWhiteSpace(effectiveGovernorateAssign))
            {
                TempData["ErrorMessage"] = "هذا المستخدم لا يملك محافظة محفوظة";
                return RedirectToUsersPage();
            }

            if (affiliation == null || !affiliation.AffiliationEntityId.HasValue)
            {
                TempData["ErrorMessage"] = "هذا المستخدم لا يملك بيانات انتساب مكتملة";
                return RedirectToUsersPage();
            }

            var entity = await _context.AffiliationEntities
                .FirstOrDefaultAsync(e => e.Id == affiliation.AffiliationEntityId.Value);

            var division = affiliation.DivisionId.HasValue
                ? await _context.Divisions.FirstOrDefaultAsync(d => d.Id == affiliation.DivisionId.Value)
                : null;

            var section = affiliation.SectionId.HasValue
                ? await _context.Sections.FirstOrDefaultAsync(s => s.Id == affiliation.SectionId.Value)
                : null;

            var group = affiliation.GroupId.HasValue
                ? await _context.Groups.FirstOrDefaultAsync(g => g.Id == affiliation.GroupId.Value)
                : null;

            var vm = new AssignManagerViewModel
            {
                UserId = userId,
                IdentifyId = identify.Id,
                FullName = identify.FullName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                Governorate = effectiveGovernorateAssign,
                District = GetEffectiveDistrict(identify, address),
                AffiliationEntityId = affiliation.AffiliationEntityId,
                DivisionId = affiliation.DivisionId,
                SectionId = affiliation.SectionId,
                GroupId = affiliation.GroupId,
                AffiliationEntityName = entity?.Name ?? string.Empty,
                DivisionName = division?.Name ?? string.Empty,
                SectionName = section?.Name ?? string.Empty,
                GroupName = group?.Name ?? string.Empty,
                AvailableLevels = BuildAvailableLevels(affiliation),
                AssignmentRole = string.IsNullOrWhiteSpace(assignmentRole) ? "Manager" : assignmentRole,
                IsSuperAdmin = isSuperAdmin
            };

            return View(vm);
        }

        // =========================================
        // تعيين مباشر من السوبر أدمن بدون استمارة
        // =========================================
        [Authorize(Roles = clsRoles.SuperAdmin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(AssignManagerViewModel model)
        {
            var adminGovernorate = await GetCurrentAdminGovernorate();
            var isSuperAdmin = await IsCurrentUserSuperAdmin();

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "المستخدم غير موجود";
                return RedirectToUsersPage();
            }

            var identify = await _context.Identifies
                .FirstOrDefaultAsync(i => i.UserId == model.UserId);

            var address = await _context.Addresses
                .FirstOrDefaultAsync(a => a.UserId == model.UserId);

            var effectiveGovernorateAssign = GetEffectiveGovernorate(identify, address);
            var effectiveDistrictAssign = GetEffectiveDistrict(identify, address);
            var assignmentBaghdadScope = GetAssignmentBaghdadScope(effectiveGovernorateAssign, effectiveDistrictAssign);

            if (!isSuperAdmin && !string.IsNullOrEmpty(adminGovernorate))
            {
                if (!IsGovernorateInManagedScope(effectiveGovernorateAssign, adminGovernorate))
                {
                    TempData["ErrorMessage"] = $"لا يمكنك تعيين مستخدم خارج محافظة {adminGovernorate}";
                    return RedirectToUsersPage();
                }
            }

            var affiliation = await _context.AffiliationInfos
                .Include(a => a.AffiliationEntity)
                .Include(a => a.Division)
                .Include(a => a.Section)
                .Include(a => a.Group)
                .FirstOrDefaultAsync(a => a.UserId == model.UserId);

            if (identify == null || string.IsNullOrWhiteSpace(effectiveGovernorateAssign) || affiliation == null || !affiliation.AffiliationEntityId.HasValue)
            {
                TempData["ErrorMessage"] = "بيانات المستخدم غير مكتملة";
                return RedirectToUsersPage();
            }

            if (!identify.IsPromoted || identify.AccountType != "فرد")
            {
                TempData["ErrorMessage"] = "يجب أن يكون المستخدم مصعّدًا إلى فرد أولاً";
                return RedirectToUsersPage();
            }

            model.AvailableLevels = BuildAvailableLevels(affiliation);

            if (string.IsNullOrWhiteSpace(model.AssignmentRole))
                model.AssignmentRole = "Manager";

            var arabicRoleName = model.AssignmentRole == "Assistant" ? "معاون" : "مسؤول";

            int? entityId = affiliation.AffiliationEntityId;
            int? divisionId = null;
            int? sectionId = null;
            int? groupId = null;

            switch (model.SelectedLevel)
            {
                case "Entity":
                    break;

                case "Division":
                    if (!affiliation.DivisionId.HasValue)
                    {
                        ModelState.AddModelError("", $"لا يمكن تعيينه {arabicRoleName} قسم لأن القسم غير موجود في استمارته");
                    }
                    else
                    {
                        divisionId = affiliation.DivisionId;
                    }
                    break;

                case "Section":
                    if (!affiliation.DivisionId.HasValue || !affiliation.SectionId.HasValue)
                    {
                        ModelState.AddModelError("", $"لا يمكن تعيينه {arabicRoleName} شعبة لأن الشعبة غير موجودة في استمارته");
                    }
                    else
                    {
                        divisionId = affiliation.DivisionId;
                        sectionId = affiliation.SectionId;
                    }
                    break;

                case "Group":
                    if (!affiliation.DivisionId.HasValue || !affiliation.SectionId.HasValue || !affiliation.GroupId.HasValue)
                    {
                        ModelState.AddModelError("", $"لا يمكن تعيينه {arabicRoleName} وحدة لأن الوحدة غير موجودة في استمارته");
                    }
                    else
                    {
                        divisionId = affiliation.DivisionId;
                        sectionId = affiliation.SectionId;
                        groupId = affiliation.GroupId;
                    }
                    break;

                default:
                    ModelState.AddModelError("", "نوع المسؤولية غير صحيح");
                    break;
            }

            if (!ModelState.IsValid)
            {
                await FillNames(model, affiliation);
                model.Governorate = effectiveGovernorateAssign;
                model.District = GetEffectiveDistrict(identify, address);
                model.FullName = identify.FullName ?? string.Empty;
                model.Email = user.Email ?? string.Empty;
                model.IsSuperAdmin = isSuperAdmin;
                return View(model);
            }

            var alreadyExists = false;

            if (model.AssignmentRole == "Manager")
            {
                var existingAssignments = await _context.ManagementAssignments
                    .Where(x =>
                        x.Governorate == effectiveGovernorateAssign &&
                        x.AffiliationEntityId == entityId &&
                        x.DivisionId == divisionId &&
                        x.SectionId == sectionId &&
                        x.GroupId == groupId &&
                        x.ManagementLevel == model.SelectedLevel &&
                        x.AssignmentRole == "Manager")
                    .ToListAsync();

                if (effectiveGovernorateAssign == "بغداد")
                {
                    foreach (var existingAssignment in existingAssignments)
                    {
                        if (await GetStoredAssignmentBaghdadScopeAsync(existingAssignment) == assignmentBaghdadScope)
                        {
                            alreadyExists = true;
                            break;
                        }
                    }
                }
                else
                {
                    alreadyExists = existingAssignments.Any();
                }
            }

            if (alreadyExists)
            {
                ModelState.AddModelError("", "يوجد مسؤول مسبقًا لنفس هذا المستوى وفي نفس المحافظة");
                await FillNames(model, affiliation);
                model.Governorate = effectiveGovernorateAssign;
                model.District = GetEffectiveDistrict(identify, address);
                model.FullName = identify.FullName ?? string.Empty;
                model.Email = user.Email ?? string.Empty;
                model.IsSuperAdmin = isSuperAdmin;
                return View(model);
            }

            var userAssignments = await _context.ManagementAssignments
                .Where(x => x.UserId == model.UserId)
                .ToListAsync();

            var userAlreadyHasSameAssignment = false;
            foreach (var userAssignment in userAssignments.Where(x =>
                         x.Governorate == effectiveGovernorateAssign &&
                         x.AffiliationEntityId == entityId &&
                         x.DivisionId == divisionId &&
                         x.SectionId == sectionId &&
                         x.GroupId == groupId &&
                         x.ManagementLevel == model.SelectedLevel &&
                         x.AssignmentRole == model.AssignmentRole))
            {
                if (effectiveGovernorateAssign != "بغداد" ||
                    await GetStoredAssignmentBaghdadScopeAsync(userAssignment) == assignmentBaghdadScope)
                {
                    userAlreadyHasSameAssignment = true;
                    break;
                }
            }

            if (userAlreadyHasSameAssignment)
            {
                ModelState.AddModelError("", "هذا المستخدم لديه نفس التكليف مسبقًا");
                await FillNames(model, affiliation);
                model.Governorate = effectiveGovernorateAssign;
                model.District = GetEffectiveDistrict(identify, address);
                model.FullName = identify.FullName ?? string.Empty;
                model.Email = user.Email ?? string.Empty;
                model.IsSuperAdmin = isSuperAdmin;
                return View(model);
            }

            var assignment = new ManagementAssignment
            {
                UserId = model.UserId,
                Governorate = effectiveGovernorateAssign,
                BaghdadScope = assignmentBaghdadScope,
                AffiliationEntityId = entityId,
                DivisionId = divisionId,
                SectionId = sectionId,
                GroupId = groupId,
                ManagementLevel = model.SelectedLevel,
                AssignmentRole = model.AssignmentRole,
                CreatedAt = DateTime.Now
            };

            _context.ManagementAssignments.Add(assignment);
            await _context.SaveChangesAsync();

            var roleName = model.AssignmentRole == "Assistant" ? clsRoles.AssistantManager : clsRoles.Manager;
            if (!await _userManager.IsInRoleAsync(user, roleName))
            {
                await _userManager.AddToRoleAsync(user, roleName);
            }

            try
            {
                var managedName = await GetManagedEntityDisplayNameAsync(model.SelectedLevel, entityId, divisionId, sectionId, groupId);
                var displayRoleName = GetArabicAssignmentName(model.SelectedLevel, model.AssignmentRole);

                var message = string.IsNullOrWhiteSpace(managedName)
                    ? $"تم تعيينك مباشرة كـ {displayRoleName} في محافظة {effectiveGovernorateAssign}"
                    : $"تم تعيينك مباشرة كـ {displayRoleName}: {managedName} في محافظة {effectiveGovernorateAssign}";

                await _notificationService.CreateNotification(
                    "✅ تم التعيين الإداري",
                    message,
                    model.UserId,
                    "bi-patch-check-fill",
                    "/Register/ProfileDetails"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في إرسال إشعار التعيين الإداري المباشر");
            }

            TempData["SuccessMessage"] = model.AssignmentRole == "Assistant"
                ? "✅ تم تعيين المستخدم معاونًا بنجاح"
                : "✅ تم تعيين المستخدم مسؤولًا بنجاح";

            return RedirectToUsersPage();
        }

        // =========================================
        // فتح الاستمارة من الإشعار
        // =========================================
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> RespondToRequest(int id)
        {
            var currentUserId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(currentUserId))
                return RedirectToAction("Login", "Account");

            var request = await _context.ManagementAssignmentRequests
                .FirstOrDefaultAsync(x => x.Id == id);

            if (request == null)
            {
                TempData["ErrorMessage"] = "الطلب غير موجود";
                return RedirectToAction("Index", "Home");
            }

            if (request.UserId != currentUserId)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية لهذا الطلب";
                return RedirectToAction("Index", "Home");
            }

            if (request.Status != "PendingUserResponse")
            {
                TempData["WarningMessage"] = "تم التعامل مع هذا الطلب مسبقًا";
                return RedirectToAction("Index", "Home");
            }

            var user = await _userManager.FindByIdAsync(request.UserId);
            var identify = await _context.Identifies.FirstOrDefaultAsync(i => i.UserId == request.UserId);
            var model = await BuildResponseViewModelAsync(request, user?.Email ?? string.Empty, identify?.FullName ?? string.Empty);

            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RespondToRequest(ManagementAssignmentRequestResponseViewModel model)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(currentUserId))
                return RedirectToAction("Login", "Account");

            var request = await _context.ManagementAssignmentRequests.FirstOrDefaultAsync(x => x.Id == model.RequestId);
            if (request == null)
            {
                TempData["ErrorMessage"] = "الطلب غير موجود";
                return RedirectToAction("Index", "Home");
            }

            if (request.UserId != currentUserId)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية لهذا الطلب";
                return RedirectToAction("Index", "Home");
            }

            if (request.Status != "PendingUserResponse")
            {
                TempData["WarningMessage"] = "تم التعامل مع هذا الطلب مسبقًا";
                return RedirectToAction("Index", "Home");
            }

            var user = await _userManager.FindByIdAsync(request.UserId);
            var identify = await _context.Identifies.FirstOrDefaultAsync(i => i.UserId == request.UserId);

            if (!ModelState.IsValid)
            {
                var invalidModel = await BuildResponseViewModelAsync(request, user?.Email ?? string.Empty, identify?.FullName ?? string.Empty, model);
                return View(invalidModel);
            }

            var entity = await _context.AffiliationEntities.FirstOrDefaultAsync(x => x.Id == model.AffiliationEntityId);
            if (entity == null)
            {
                ModelState.AddModelError(nameof(model.AffiliationEntityId), "جهة الانتساب غير صحيحة");
            }

            Division? division = null;
            Section? section = null;
            Group? group = null;

            if (model.DivisionId.HasValue)
            {
                division = await _context.Divisions.FirstOrDefaultAsync(x =>
                    x.Id == model.DivisionId.Value &&
                    x.AffiliationEntityId == model.AffiliationEntityId);

                if (division == null)
                    ModelState.AddModelError(nameof(model.DivisionId), "القسم لا يتبع جهة الانتساب المحددة");
            }

            if (model.SectionId.HasValue)
            {
                if (!model.DivisionId.HasValue)
                {
                    ModelState.AddModelError(nameof(model.SectionId), "يجب اختيار القسم أولاً");
                }
                else
                {
                    section = await _context.Sections.FirstOrDefaultAsync(x =>
                        x.Id == model.SectionId.Value &&
                        x.DivisionId == model.DivisionId.Value);

                    if (section == null)
                        ModelState.AddModelError(nameof(model.SectionId), "الشعبة لا تتبع القسم المحدد");
                }
            }

            if (model.GroupId.HasValue)
            {
                if (!model.SectionId.HasValue)
                {
                    ModelState.AddModelError(nameof(model.GroupId), "يجب اختيار الشعبة أولاً");
                }
                else
                {
                    group = await _context.Groups.FirstOrDefaultAsync(x =>
                        x.Id == model.GroupId.Value &&
                        x.SectionId == model.SectionId.Value);

                    if (group == null)
                        ModelState.AddModelError(nameof(model.GroupId), "الوحدة لا تتبع الشعبة المحددة");
                }
            }

            if (!ModelState.IsValid)
            {
                var invalidModel = await BuildResponseViewModelAsync(request, user?.Email ?? string.Empty, identify?.FullName ?? string.Empty, model);
                return View(invalidModel);
            }

            var requiredLevel = request.ManagementLevel;

            if (requiredLevel == "Division" && !model.DivisionId.HasValue)
                ModelState.AddModelError(nameof(model.DivisionId), "يجب اختيار القسم لهذا النوع من التكليف");

            if (requiredLevel == "Section" && !model.SectionId.HasValue)
                ModelState.AddModelError(nameof(model.SectionId), "يجب اختيار الشعبة لهذا النوع من التكليف");

            if (requiredLevel == "Group" && !model.GroupId.HasValue)
                ModelState.AddModelError(nameof(model.GroupId), "يجب اختيار الوحدة لهذا النوع من التكليف");

            if (!ModelState.IsValid)
            {
                var invalidModel = await BuildResponseViewModelAsync(request, user?.Email ?? string.Empty, identify?.FullName ?? string.Empty, model);
                return View(invalidModel);
            }

            request.AffiliationEntityId = model.AffiliationEntityId;
            request.DivisionId = model.DivisionId;
            request.SectionId = model.SectionId;
            request.GroupId = model.GroupId;
            request.UserNotes = model.UserNotes;
            request.Status = "SubmittedToSuperAdmin";
            request.UserRespondedAt = DateTime.Now;

            _context.ManagementAssignmentRequests.Update(request);
            await _context.SaveChangesAsync();

            try
            {
                var levelArabic = GetArabicAssignmentName(request.ManagementLevel, request.AssignmentRole);
                var managedName = await GetManagedEntityDisplayNameAsync(
                    request.ManagementLevel,
                    request.AffiliationEntityId,
                    request.DivisionId,
                    request.SectionId,
                    request.GroupId);

                var message = string.IsNullOrWhiteSpace(managedName)
                    ? $"قام المستخدم {identify?.FullName ?? "مستخدم"} بإرسال استمارة التكليف بانتظار موافقتك"
                    : $"قام المستخدم {identify?.FullName ?? "مستخدم"} بإرسال استمارة {levelArabic}: {managedName} بانتظار موافقتك";

                await _notificationService.CreateNotification(
                    "📨 استمارة تكليف بانتظار المراجعة",
                    message,
                    request.RequestedByUserId,
                    "bi-person-check-fill",
                    $"/ManagementAssignments/ReviewRequest/{request.Id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في إرسال إشعار مراجعة الاستمارة");
            }

            TempData["SuccessMessage"] = "✅ تم إرسال الاستمارة إلى السوبر أدمن بانتظار الموافقة";
            return RedirectToAction("Index", "Home");
        }

        [Authorize(Roles = clsRoles.SuperAdmin)]
        [HttpGet]
        public async Task<IActionResult> ReviewRequest(int id)
        {
            var request = await _context.ManagementAssignmentRequests.FirstOrDefaultAsync(x => x.Id == id);
            if (request == null)
            {
                TempData["ErrorMessage"] = "الطلب غير موجود";
                return RedirectToUsersPage();
            }

            if (request.Status != "SubmittedToSuperAdmin")
            {
                TempData["WarningMessage"] = "هذا الطلب ليس بانتظار موافقة السوبر أدمن";
                return RedirectToUsersPage();
            }

            var model = await BuildReviewViewModelAsync(request);
            return View(model);
        }

        [Authorize(Roles = clsRoles.SuperAdmin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveRequest(int id, string? superAdminNotes)
        {
            var request = await _context.ManagementAssignmentRequests.FirstOrDefaultAsync(x => x.Id == id);
            if (request == null)
            {
                TempData["ErrorMessage"] = "الطلب غير موجود";
                return RedirectToUsersPage();
            }

            if (request.Status != "SubmittedToSuperAdmin")
            {
                TempData["ErrorMessage"] = "الطلب ليس جاهزًا للموافقة";
                return RedirectToUsersPage();
            }

            var alreadyExists = false;
            if (request.AssignmentRole == "Manager")
            {
                alreadyExists = await _context.ManagementAssignments.AnyAsync(x =>
                    x.Governorate == request.Governorate &&
                    x.AffiliationEntityId == request.AffiliationEntityId &&
                    x.DivisionId == request.DivisionId &&
                    x.SectionId == request.SectionId &&
                    x.GroupId == request.GroupId &&
                    x.ManagementLevel == request.ManagementLevel &&
                    x.AssignmentRole == "Manager");
            }

            if (alreadyExists)
            {
                TempData["ErrorMessage"] = "يوجد مسؤول مسبقًا لهذا المستوى";
                return RedirectToAction("ReviewRequest", new { id });
            }

            var sameUserAssignmentExists = await _context.ManagementAssignments.AnyAsync(x =>
                x.UserId == request.UserId &&
                x.Governorate == request.Governorate &&
                x.AffiliationEntityId == request.AffiliationEntityId &&
                x.DivisionId == request.DivisionId &&
                x.SectionId == request.SectionId &&
                x.GroupId == request.GroupId &&
                x.ManagementLevel == request.ManagementLevel &&
                x.AssignmentRole == request.AssignmentRole);

            if (sameUserAssignmentExists)
            {
                TempData["ErrorMessage"] = "هذا التكليف موجود للمستخدم مسبقًا";
                return RedirectToAction("ReviewRequest", new { id });
            }

            var assignment = new ManagementAssignment
            {
                UserId = request.UserId,
                Governorate = request.Governorate,
                AffiliationEntityId = request.AffiliationEntityId,
                DivisionId = request.DivisionId,
                SectionId = request.SectionId,
                GroupId = request.GroupId,
                ManagementLevel = request.ManagementLevel,
                AssignmentRole = request.AssignmentRole,
                CreatedAt = DateTime.Now
            };

            _context.ManagementAssignments.Add(assignment);
            request.Status = "Approved";
            request.SuperAdminNotes = superAdminNotes;
            request.SuperAdminReviewedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user != null)
            {
                var roleName = request.AssignmentRole == "Assistant" ? clsRoles.AssistantManager : clsRoles.Manager;
                if (!await _userManager.IsInRoleAsync(user, roleName))
                    await _userManager.AddToRoleAsync(user, roleName);
            }

            try
            {
                var levelArabic = GetArabicAssignmentName(request.ManagementLevel, request.AssignmentRole);
                var managedName = await GetManagedEntityDisplayNameAsync(
                    request.ManagementLevel,
                    request.AffiliationEntityId,
                    request.DivisionId,
                    request.SectionId,
                    request.GroupId);

                var message = string.IsNullOrWhiteSpace(managedName)
                    ? $"تمت الموافقة على طلب تكليفك كـ {levelArabic}"
                    : $"تمت الموافقة على طلب تكليفك كـ {levelArabic}: {managedName}";

                await _notificationService.CreateNotification(
                    "✅ تمت الموافقة على التكليف الإداري",
                    message,
                    request.UserId,
                    "bi-patch-check-fill",
                    "/Register/ProfileDetails");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في إرسال إشعار الموافقة");
            }

            TempData["SuccessMessage"] = "✅ تمت الموافقة على الطلب وتفعيل التكليف";
            return RedirectToUsersPage();
        }

        [Authorize(Roles = clsRoles.SuperAdmin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectRequest(int id, string? superAdminNotes)
        {
            var request = await _context.ManagementAssignmentRequests.FirstOrDefaultAsync(x => x.Id == id);
            if (request == null)
            {
                TempData["ErrorMessage"] = "الطلب غير موجود";
                return RedirectToUsersPage();
            }

            if (request.Status != "SubmittedToSuperAdmin")
            {
                TempData["ErrorMessage"] = "الطلب ليس جاهزًا للرفض";
                return RedirectToUsersPage();
            }

            request.Status = "RejectedBySuperAdmin";
            request.SuperAdminNotes = superAdminNotes;
            request.SuperAdminReviewedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            try
            {
                await _notificationService.CreateNotification(
                    "❌ تم رفض طلب التكليف الإداري",
                    string.IsNullOrWhiteSpace(superAdminNotes)
                        ? "تم رفض طلب التكليف الإداري الخاص بك."
                        : $"تم رفض طلب التكليف الإداري الخاص بك. السبب: {superAdminNotes}",
                    request.UserId,
                    "bi-x-circle-fill",
                    "/Register/ProfileDetails");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في إرسال إشعار الرفض");
            }

            TempData["SuccessMessage"] = "✅ تم رفض الطلب";
            return RedirectToUsersPage();
        }

        // =========================================
        // إلغاء تكليف نهائي قائم
        // =========================================
        [Authorize(Roles = clsRoles.SuperAdmin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveAssignment([FromBody] RemoveAssignmentRequest request)
        {
            try
            {
                if (request == null || request.AssignmentId <= 0)
                    return Json(new { success = false, message = "معرف التكليف غير صالح" });

                var assignment = await _context.ManagementAssignments
                    .FirstOrDefaultAsync(x => x.Id == request.AssignmentId);

                if (assignment == null)
                    return Json(new { success = false, message = "التكليف غير موجود" });

                var adminGovernorate = await GetCurrentAdminGovernorate();
                var isSuperAdmin = await IsCurrentUserSuperAdmin();

                if (!isSuperAdmin && !string.IsNullOrEmpty(adminGovernorate))
                {
                    if (!IsGovernorateInManagedScope(assignment.Governorate, adminGovernorate))
                    {
                        return Json(new { success = false, message = "لا يمكنك إلغاء تكليف خارج محافظتك" });
                    }
                }

                var userId = assignment.UserId;
                var removedRole = assignment.AssignmentRole;
                var removedLevel = assignment.ManagementLevel;
                var removedGovernorate = assignment.Governorate;
                var removedEntityId = assignment.AffiliationEntityId;
                var removedDivisionId = assignment.DivisionId;
                var removedSectionId = assignment.SectionId;
                var removedGroupId = assignment.GroupId;

                _context.ManagementAssignments.Remove(assignment);
                await _context.SaveChangesAsync();

                var user = await _userManager.FindByIdAsync(userId);
                if (user != null)
                {
                    if (removedRole == "Assistant")
                    {
                        var hasOtherAssistantAssignments = await _context.ManagementAssignments
                            .AnyAsync(x => x.UserId == userId && x.AssignmentRole == "Assistant");

                        if (!hasOtherAssistantAssignments &&
                            await _userManager.IsInRoleAsync(user, clsRoles.AssistantManager))
                        {
                            await _userManager.RemoveFromRoleAsync(user, clsRoles.AssistantManager);
                        }
                    }
                    else if (removedRole == "Manager")
                    {
                        var hasOtherManagerAssignments = await _context.ManagementAssignments
                            .AnyAsync(x => x.UserId == userId && x.AssignmentRole == "Manager");

                        if (!hasOtherManagerAssignments &&
                            await _userManager.IsInRoleAsync(user, clsRoles.Manager))
                        {
                            await _userManager.RemoveFromRoleAsync(user, clsRoles.Manager);
                        }
                    }
                }

                try
                {
                    var levelArabic = GetArabicAssignmentName(removedLevel, removedRole);
                    var managedName = await GetManagedEntityDisplayNameAsync(
                        removedLevel,
                        removedEntityId,
                        removedDivisionId,
                        removedSectionId,
                        removedGroupId);

                    var message = string.IsNullOrWhiteSpace(managedName)
                        ? $"تم إلغاء تكليفك كـ {levelArabic} في محافظة {removedGovernorate}"
                        : $"تم إلغاء تكليفك كـ {levelArabic}: {managedName} في محافظة {removedGovernorate}";

                    await _notificationService.CreateNotification(
                        "تم إلغاء التكليف الإداري",
                        message,
                        userId,
                        "bi-trash-fill",
                        "/Register/ProfileDetails");
                }
                catch (Exception notificationEx)
                {
                    _logger.LogError(notificationEx, "خطأ في إرسال إشعار إلغاء التكليف");
                }

                return Json(new { success = true, message = "✅ تم إلغاء التكليف بنجاح" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في إلغاء التكليف");
                return Json(new { success = false, message = $"❌ حدث خطأ: {ex.Message}" });
            }
        }

        private List<string> BuildAvailableLevels(AffiliationInfo affiliation)
        {
            var levels = new List<string>();

            if (affiliation.AffiliationEntityId.HasValue)
                levels.Add("Entity");

            if (affiliation.DivisionId.HasValue)
                levels.Add("Division");

            if (affiliation.SectionId.HasValue)
                levels.Add("Section");

            if (affiliation.GroupId.HasValue)
                levels.Add("Group");

            return levels;
        }

        private async Task FillNames(AssignManagerViewModel model, AffiliationInfo affiliation)
        {
            var entity = affiliation.AffiliationEntityId.HasValue
                ? await _context.AffiliationEntities.FirstOrDefaultAsync(e => e.Id == affiliation.AffiliationEntityId.Value)
                : null;

            var division = affiliation.DivisionId.HasValue
                ? await _context.Divisions.FirstOrDefaultAsync(d => d.Id == affiliation.DivisionId.Value)
                : null;

            var section = affiliation.SectionId.HasValue
                ? await _context.Sections.FirstOrDefaultAsync(s => s.Id == affiliation.SectionId.Value)
                : null;

            var group = affiliation.GroupId.HasValue
                ? await _context.Groups.FirstOrDefaultAsync(g => g.Id == affiliation.GroupId.Value)
                : null;

            model.AffiliationEntityId = affiliation.AffiliationEntityId;
            model.DivisionId = affiliation.DivisionId;
            model.SectionId = affiliation.SectionId;
            model.GroupId = affiliation.GroupId;

            model.AffiliationEntityName = entity?.Name ?? string.Empty;
            model.DivisionName = division?.Name ?? string.Empty;
            model.SectionName = section?.Name ?? string.Empty;
            model.GroupName = group?.Name ?? string.Empty;
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

        private string GetArabicAssignmentName(string level, string assignmentRole)
        {
            var prefix = assignmentRole == "Assistant" ? "معاون" : "مسؤول";

            return level switch
            {
                "Entity" => $"{prefix} جهة",
                "Division" => $"{prefix} قسم",
                "Section" => $"{prefix} شعبة",
                "Group" => $"{prefix} وحدة",
                _ => prefix
            };
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetDivisionsByEntity(int entityId)
        {
            var divisions = await _context.Divisions
                .Where(x => x.AffiliationEntityId == entityId)
                .OrderBy(x => x.Name)
                .Select(x => new { id = x.Id, name = x.Name })
                .ToListAsync();

            return Json(divisions);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetSectionsByDivision(int divisionId)
        {
            var sections = await _context.Sections
                .Where(x => x.DivisionId == divisionId)
                .OrderBy(x => x.Name)
                .Select(x => new { id = x.Id, name = x.Name })
                .ToListAsync();

            return Json(sections);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetGroupsBySection(int sectionId)
        {
            var groups = await _context.Groups
                .Where(x => x.SectionId == sectionId)
                .OrderBy(x => x.Name)
                .Select(x => new { id = x.Id, name = x.Name })
                .ToListAsync();

            return Json(groups);
        }

        private async Task<ManagementAssignmentRequestResponseViewModel> BuildResponseViewModelAsync(
            ManagementAssignmentRequest request,
            string email,
            string fullName,
            ManagementAssignmentRequestResponseViewModel? postedModel = null)
        {
            var model = postedModel ?? new ManagementAssignmentRequestResponseViewModel();
            model.RequestId = request.Id;
            model.UserId = request.UserId;
            model.Email = email;
            model.FullName = fullName;
            model.Governorate = request.Governorate;
            model.AssignmentRole = request.AssignmentRole;
            model.ManagementLevel = request.ManagementLevel;
            model.AffiliationEntityId ??= request.AffiliationEntityId;
            model.DivisionId ??= request.DivisionId;
            model.SectionId ??= request.SectionId;
            model.GroupId ??= request.GroupId;
            model.UserNotes ??= request.UserNotes;

            model.AffiliationEntities = await _context.AffiliationEntities
                .OrderBy(x => x.Name)
                .Select(x => new SelectListItem
                {
                    Value = x.Id.ToString(),
                    Text = x.Name
                })
                .ToListAsync();

            if (model.AffiliationEntityId.HasValue)
            {
                model.Divisions = await _context.Divisions
                    .Where(x => x.AffiliationEntityId == model.AffiliationEntityId.Value)
                    .OrderBy(x => x.Name)
                    .Select(x => new SelectListItem
                    {
                        Value = x.Id.ToString(),
                        Text = x.Name
                    })
                    .ToListAsync();
            }

            if (model.DivisionId.HasValue)
            {
                model.Sections = await _context.Sections
                    .Where(x => x.DivisionId == model.DivisionId.Value)
                    .OrderBy(x => x.Name)
                    .Select(x => new SelectListItem
                    {
                        Value = x.Id.ToString(),
                        Text = x.Name
                    })
                    .ToListAsync();
            }

            if (model.SectionId.HasValue)
            {
                model.Groups = await _context.Groups
                    .Where(x => x.SectionId == model.SectionId.Value)
                    .OrderBy(x => x.Name)
                    .Select(x => new SelectListItem
                    {
                        Value = x.Id.ToString(),
                        Text = x.Name
                    })
                    .ToListAsync();
            }

            return model;
        }

        private async Task<ManagementAssignmentReviewViewModel> BuildReviewViewModelAsync(ManagementAssignmentRequest request)
        {
            var user = await _userManager.FindByIdAsync(request.UserId);
            var identify = await _context.Identifies.FirstOrDefaultAsync(x => x.UserId == request.UserId);

            var entityName = request.AffiliationEntityId.HasValue
                ? await _context.AffiliationEntities.Where(x => x.Id == request.AffiliationEntityId.Value).Select(x => x.Name).FirstOrDefaultAsync() ?? string.Empty
                : string.Empty;
            var divisionName = request.DivisionId.HasValue
                ? await _context.Divisions.Where(x => x.Id == request.DivisionId.Value).Select(x => x.Name).FirstOrDefaultAsync() ?? string.Empty
                : string.Empty;
            var sectionName = request.SectionId.HasValue
                ? await _context.Sections.Where(x => x.Id == request.SectionId.Value).Select(x => x.Name).FirstOrDefaultAsync() ?? string.Empty
                : string.Empty;
            var groupName = request.GroupId.HasValue
                ? await _context.Groups.Where(x => x.Id == request.GroupId.Value).Select(x => x.Name).FirstOrDefaultAsync() ?? string.Empty
                : string.Empty;

            return new ManagementAssignmentReviewViewModel
            {
                RequestId = request.Id,
                UserId = request.UserId,
                FullName = identify?.FullName ?? string.Empty,
                Email = user?.Email ?? string.Empty,
                Governorate = request.Governorate,
                AssignmentRole = request.AssignmentRole,
                ManagementLevel = request.ManagementLevel,
                ManagementLevelArabic = GetArabicAssignmentName(request.ManagementLevel, request.AssignmentRole),
                AffiliationEntityName = entityName,
                DivisionName = divisionName,
                SectionName = sectionName,
                GroupName = groupName,
                UserNotes = request.UserNotes,
                SuperAdminNotes = request.SuperAdminNotes,
                RequestedByUserId = request.RequestedByUserId,
                CreatedAt = request.CreatedAt,
                UserRespondedAt = request.UserRespondedAt
            };
        }

        private static string DetermineManagementLevel(int? divisionId, int? sectionId, int? groupId)
        {
            if (groupId.HasValue) return "Group";
            if (sectionId.HasValue) return "Section";
            if (divisionId.HasValue) return "Division";
            return "Entity";
        }

        public class RemoveAssignmentRequest
        {
            public int AssignmentId { get; set; }
        }
    }
}
