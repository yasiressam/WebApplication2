using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using WebApplication2.Data;

using WebApplication2.Models;
using WebApplication2.Models.Profile;
using WebApplication2.Services;

namespace WebApplication2.Controllers
{
    [Authorize(Roles = clsRoles.SuperAdmin)]
    public class SuperAdminController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly INotificationService _notificationService;
        private readonly ILogger<SuperAdminController> _logger;
        private readonly ApplicationDbContext _context;
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
            ILogger<SuperAdminController> logger)
        {
            _userManager = userManager;
            _context = context;
            _notificationService = notificationService;
            _logger = logger;
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
            var allIdentifies = await _context.Identifies.ToListAsync();
            var identifiesList = allIdentifies.ToList();

            ViewBag.TotalUsers = await _userManager.Users.CountAsync();
            ViewBag.TotalMembers = identifiesList.Count(i =>
                !string.IsNullOrWhiteSpace(i.Education) &&
                (i.AccountType == "فرد" || i.IsPromoted));
            ViewBag.PendingRequests = identifiesList.Count(i => i.RequestedPromotion);
            ViewBag.NewThisWeek = identifiesList.Count(i => i.CreatedAt > DateTime.UtcNow.AddDays(-7));

            return View();
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
            int pageSize = 10)
        {
            pageSize = NormalizeUserManagementPageSize(pageSize);
            page = Math.Max(1, page);

            return await UsersPagedFromDatabaseAsync(administrativeOnly, viewName, page, search, role, residenceGovernorate, workGovernorate, gender, status, managerLevel, education, pageSize);

            var priorityRoles = new[]
            {
                clsRoles.SuperAdmin,
                clsRoles.Admin,
                clsRoles.DistrictAdmin,
                clsRoles.Manager,
                clsRoles.AssistantManager
            };

            var priorityRoleUsers = await _context.UserRoles
                .Join(
                    _context.Roles,
                    userRole => userRole.RoleId,
                    role => role.Id,
                    (userRole, role) => new { userRole.UserId, RoleName = role.Name ?? string.Empty })
                .Where(x => priorityRoles.Contains(x.RoleName))
                .ToListAsync();

            var superAdminUserIds = priorityRoleUsers
                .Where(x => x.RoleName == clsRoles.SuperAdmin)
                .Select(x => x.UserId)
                .ToList();
            var adminUserIds = priorityRoleUsers
                .Where(x => x.RoleName == clsRoles.Admin)
                .Select(x => x.UserId)
                .ToList();
            var districtAdminUserIds = priorityRoleUsers
                .Where(x => x.RoleName == clsRoles.DistrictAdmin)
                .Select(x => x.UserId)
                .ToList();
            var managerRoleUserIds = priorityRoleUsers
                .Where(x => x.RoleName == clsRoles.Manager || x.RoleName == clsRoles.AssistantManager)
                .Select(x => x.UserId)
                .ToList();
            var assignmentUserIds = await _context.ManagementAssignments
                .AsNoTracking()
                .Select(x => x.UserId)
                .Distinct()
                .ToListAsync();

            var usersQuery = _userManager.Users
                .OrderByDescending(u => superAdminUserIds.Contains(u.Id))
                .ThenByDescending(u => adminUserIds.Contains(u.Id))
                .ThenByDescending(u => districtAdminUserIds.Contains(u.Id))
                .ThenByDescending(u => managerRoleUserIds.Contains(u.Id) || assignmentUserIds.Contains(u.Id))
                .ThenBy(u => u.Email);
            var users = await usersQuery.ToListAsync();

            var userIds = users.Select(u => u.Id).ToHashSet();

            var profilesByUserId = await _context.Identifies
                .AsNoTracking()
                .Where(i => userIds.Contains(i.UserId))
                .ToDictionaryAsync(i => i.UserId, i => i);

            var profileIds = profilesByUserId.Values.Select(p => p.Id).ToHashSet();

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

            var assignmentsByUserId = await _context.ManagementAssignments
                .AsNoTracking()
                .Where(x => userIds.Contains(x.UserId))
                .GroupBy(x => x.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.ToList());

            var affiliationInfosByUserId = await _context.AffiliationInfos
                .AsNoTracking()
                .Where(x => userIds.Contains(x.UserId))
                .GroupBy(x => x.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.First());

            var rolesByUserId = await _context.UserRoles
                .Where(ur => userIds.Contains(ur.UserId))
                .Join(
                    _context.Roles,
                    userRole => userRole.RoleId,
                    role => role.Id,
                    (userRole, role) => new { userRole.UserId, RoleName = role.Name ?? string.Empty })
                .GroupBy(x => x.UserId)
                .ToDictionaryAsync(g => g.Key, g => (IList<string>)g.Select(x => x.RoleName).ToList());

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

            var list = new List<SuperAdminUserVM>();

            string GetEffectiveGovernorateFast(Identify? profile)
            {
                var workLocation = profile != null && workLocationsByProfileId.TryGetValue(profile.Id, out var wl)
                    ? wl
                    : profile?.WorkLocation;

                return !string.IsNullOrWhiteSpace(workLocation?.Governorate)
                    ? workLocation.Governorate
                    : !string.IsNullOrWhiteSpace(profile?.WorkGovernorate)
                    ? profile.WorkGovernorate
                    : string.Empty;
            }

            string GetEffectiveDistrictFast(Identify? profile)
            {
                var workLocation = profile != null && workLocationsByProfileId.TryGetValue(profile.Id, out var wl)
                    ? wl
                    : profile?.WorkLocation;

                if (!string.IsNullOrWhiteSpace(workLocation?.Governorate) && workLocation.Governorate == "بغداد")
                    return workLocation.District ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(profile?.WorkGovernorate) && profile.WorkGovernorate == "بغداد")
                    return profile.WorkDistrict ?? string.Empty;

                return string.Empty;
            }

            string GetManagedEntityDisplayName(ManagementAssignment assignment)
            {
                return assignment.ManagementLevel switch
                {
                    "Entity" when assignment.AffiliationEntityId.HasValue =>
                        affiliationEntityNames.TryGetValue(assignment.AffiliationEntityId.Value, out var name) ? name : string.Empty,
                    "Division" when assignment.DivisionId.HasValue =>
                        divisionNames.TryGetValue(assignment.DivisionId.Value, out var name) ? name : string.Empty,
                    "Section" when assignment.SectionId.HasValue =>
                        sectionNames.TryGetValue(assignment.SectionId.Value, out var name) ? name : string.Empty,
                    "Group" when assignment.GroupId.HasValue =>
                        groupNames.TryGetValue(assignment.GroupId.Value, out var name) ? name : string.Empty,
                    _ => string.Empty
                };
            }

            static string NormalizeSearchText(string? value)
            {
                return (value ?? string.Empty).Trim().ToLowerInvariant();
            }

            static string NormalizeSearchDigits(string? value)
            {
                return new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
            }

            string GetAffiliationSearchText(string userId)
            {
                if (!affiliationInfosByUserId.TryGetValue(userId, out var affiliationInfo))
                    return string.Empty;

                var parts = new List<string>();

                if (affiliationInfo.AffiliationEntityId.HasValue &&
                    affiliationEntityNames.TryGetValue(affiliationInfo.AffiliationEntityId.Value, out var entityName))
                    parts.Add(entityName);

                if (affiliationInfo.DivisionId.HasValue &&
                    divisionNames.TryGetValue(affiliationInfo.DivisionId.Value, out var divisionName))
                    parts.Add(divisionName);

                if (affiliationInfo.SectionId.HasValue &&
                    sectionNames.TryGetValue(affiliationInfo.SectionId.Value, out var sectionName))
                    parts.Add(sectionName);

                if (affiliationInfo.GroupId.HasValue &&
                    groupNames.TryGetValue(affiliationInfo.GroupId.Value, out var groupName))
                    parts.Add(groupName);

                if (!string.IsNullOrWhiteSpace(affiliationInfo.BadgeNumber))
                    parts.Add(affiliationInfo.BadgeNumber);

                return string.Join(" ", parts);
            }

            string BuildUserSearchText(IdentityUser user, Identify? profile, IEnumerable<ManagementAssignment> assignments)
            {
                var assignmentText = string.Join(" ", assignments.Select(GetManagedEntityDisplayName));
                return NormalizeSearchText(string.Join(" ", new[]
                {
                    profile?.FullName,
                    user.Email,
                    user.PhoneNumber,
                    profile?.PhoneNumber,
                    profile?.WhatsAppNumber,
                    profile == null ? string.Empty : GetAffiliationSearchText(profile.UserId),
                    assignmentText
                }));
            }

            foreach (var user in users)
            {
                var roles = rolesByUserId.TryGetValue(user.Id, out var userRoles)
                    ? userRoles
                    : new List<string>();
                profilesByUserId.TryGetValue(user.Id, out var userProfile);
                addressesByUserId.TryGetValue(user.Id, out var userAddress);
                affiliationInfosByUserId.TryGetValue(user.Id, out var affiliationInfo);
                var managementAssignments = assignmentsByUserId.TryGetValue(user.Id, out var userAssignments)
                    ? userAssignments
                    : new List<ManagementAssignment>();

                string managementLevel = "";
                string managementLevelArabic = "";
                string managedEntityName = "";
                bool isManager = managementAssignments.Any();

                var primaryAssignment = managementAssignments.FirstOrDefault();

                if (primaryAssignment != null)
                {
                    managementLevel = primaryAssignment.ManagementLevel;
                    managementLevelArabic = GetArabicLevelName(
                        primaryAssignment.ManagementLevel,
                        primaryAssignment.AssignmentRole
                    );

                    managedEntityName = GetManagedEntityDisplayName(primaryAssignment);
                }
            
                

                string governorate = "غير محدد";
                string managedGovernorate = userProfile?.ManagedGovernorate;
                string managedDistrict = userProfile?.ManagedDistrict;

                string roleDisplay = string.Join(", ", roles);

                if (userProfile?.AccountType == "فرد" && !roles.Contains("فرد"))
                {
                    if (string.IsNullOrEmpty(roleDisplay))
                        roleDisplay = "فرد";
                    else
                        roleDisplay += ", فرد";
                }

                string fullName = userProfile?.FullName ?? "غير مكتمل";

                string promotionStatus = "";
                if (userProfile?.RequestedPromotion == true)
                    promotionStatus = "⏳ قيد المراجعة";
                else if (userProfile?.RejectionReason != null)
                    promotionStatus = "❌ مرفوض";

                if (roles.Contains(clsRoles.SuperAdmin))
                {
                    governorate = "👑 السوبر أدمن - يدير الكل";
                }
                else if (roles.Contains(clsRoles.Admin))
                {
                    if (!string.IsNullOrEmpty(managedGovernorate) && !string.IsNullOrEmpty(managedDistrict))
                    {
                        governorate = $"🔷 مدير محافظة: {managedGovernorate} - القضاء: {managedDistrict}";
                    }
                    else if (!string.IsNullOrEmpty(managedGovernorate))
                    {
                        governorate = $"🔷 مدير محافظة: {managedGovernorate}";
                    }
                    else
                    {
                        governorate = "🔷 أدمن (لم تحدد المحافظة)";
                    }
                }
                else if (roles.Contains(clsRoles.MapViewer))
                {
                    if (string.IsNullOrEmpty(managedGovernorate))
                    {
                        governorate = "🗺️ مشاهد خريطة مركزي";
                    }
                    else if (managedGovernorate == "بغداد" && !string.IsNullOrEmpty(managedDistrict))
                    {
                        governorate = $"🗺️ مشاهد خريطة: بغداد - {managedDistrict}";
                    }
                    else
                    {
                        governorate = $"🗺️ مشاهد خريطة: {managedGovernorate}";
                    }
                }
                else
                {
                    governorate = GetEffectiveGovernorateFast(userProfile) ?? "غير محدد";
                }

                list.Add(new SuperAdminUserVM
                {
                    Id = user.Id,
                    Email = user.Email,
                    Roles = roleDisplay,
                    ResidenceGovernorate = userAddress?.Governorate ?? "غير محدد",
                    WorkGovernorate = !string.IsNullOrWhiteSpace(managedGovernorate)
                        ? managedGovernorate
                        : GetEffectiveGovernorateFast(userProfile),
                    WorkDistrict = !string.IsNullOrWhiteSpace(userProfile?.ManagedDistrict)
                        ? userProfile.ManagedDistrict
                        : GetEffectiveDistrictFast(userProfile),
                    Gender = userProfile?.Gender ?? "غير محدد",
                    Governorate = governorate,
                    ManagedGovernorate = managedGovernorate,
                    ManagedDistrict = userProfile?.ManagedDistrict,
                    IsActive = user.EmailConfirmed,
                    FullName = fullName,
                    CoverImage = userProfile?.CoverImage,
                    PromotionStatus = promotionStatus,
                    RequestedPromotion = userProfile?.RequestedPromotion ?? false,
                    RejectionReason = userProfile?.RejectionReason,
                    HasCompleteProfile = IsProfileComplete(userProfile, userAddress, null, null),
                    CompletionPercentage = CalculateCompletionPercentage(userProfile, userAddress, null),
                    AccountType = userProfile?.AccountType ?? "عادي",
                    ProfileId = userProfile?.Id,
                    IsManager = isManager,
                    ManagementLevel = managementLevel,
                    ManagementLevelArabic = managementLevelArabic,
                    AssignmentRole = primaryAssignment?.AssignmentRole ?? "",
                    AdministrativeResponsibilityDisplay = managementLevelArabic,
                    ManagedEntityName = managedEntityName,
                    SearchText = BuildUserSearchText(user, userProfile, managementAssignments),
                    IsPromoted = userProfile?.IsPromoted ?? false,
                    BadgeNumber = affiliationInfo?.BadgeNumber ?? "",
                    Education = userProfile?.Education ?? "---",
                    StudyStage = userProfile?.StudyStage ?? "---"
                });
            }

            list = list.OrderByDescending(u => u.RequestedPromotion)
                       .ThenByDescending(u => u.Roles.Contains("SuperAdmin"))
                       .ThenByDescending(u => u.Roles.Contains("Admin"))
                       .ThenByDescending(u => u.IsManager)
                       .ThenBy(u => u.Email)
                       .ToList();

            var unfilteredUsersCount = list.Count;

            if (!string.IsNullOrWhiteSpace(search))
            {
                var normalizedSearch = NormalizeSearchText(search);
                var normalizedSearchDigits = NormalizeSearchDigits(search);
                list = list
                    .Where(u =>
                        (u.SearchText ?? string.Empty).Contains(normalizedSearch) ||
                        (!string.IsNullOrWhiteSpace(normalizedSearchDigits) &&
                         NormalizeSearchDigits(u.SearchText).Contains(normalizedSearchDigits)))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(role))
            {
                var normalizedRole = role.Trim().ToLower();
                list = list
                    .Where(u =>
                    {
                        var roles = (u.Roles ?? string.Empty)
                            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                        return normalizedRole == "member"
                            ? ((u.AccountType == "فرد") || roles.Contains("فرد"))
                            : roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
                    })
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(residenceGovernorate))
                list = list.Where(u => string.Equals(u.ResidenceGovernorate, residenceGovernorate, StringComparison.OrdinalIgnoreCase)).ToList();

            if (!string.IsNullOrWhiteSpace(workGovernorate))
                list = list.Where(u => string.Equals(u.WorkGovernorate, workGovernorate, StringComparison.OrdinalIgnoreCase)).ToList();

            if (!string.IsNullOrWhiteSpace(gender))
                list = list.Where(u => string.Equals(u.Gender, gender, StringComparison.OrdinalIgnoreCase)).ToList();

            if (status == "active")
                list = list.Where(u => u.IsActive).ToList();
            else if (status == "inactive")
                list = list.Where(u => !u.IsActive).ToList();

            if (!string.IsNullOrWhiteSpace(managerLevel))
            {
                var parts = managerLevel.Split('-', 2);
                if (parts.Length == 2)
                {
                    list = list
                        .Where(u =>
                            string.Equals(u.AssignmentRole, parts[0], StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(u.ManagementLevel, parts[1], StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }
            }

            if (!string.IsNullOrWhiteSpace(education))
                list = list.Where(u => string.Equals(u.Education, education, StringComparison.OrdinalIgnoreCase)).ToList();

            if (administrativeOnly)
            {
                list = list
                    .Where(u =>
                        string.Equals(u.AssignmentRole, "Manager", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(u.AssignmentRole, "Assistant", StringComparison.OrdinalIgnoreCase) ||
                        u.Roles.Contains(clsRoles.Manager, StringComparison.OrdinalIgnoreCase) ||
                        u.Roles.Contains(clsRoles.AssistantManager, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            var totalUsersCount = list.Count;
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalUsersCount / (double)pageSize));

            if (!administrativeOnly)
            {
                page = Math.Min(page, totalPages);
                list = list
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();
            }

            ViewBag.AdministrativeOnly = administrativeOnly;
            ViewBag.CurrentPage = administrativeOnly ? 1 : page;
            ViewBag.PageSize = administrativeOnly ? Math.Max(list.Count, 1) : pageSize;
            ViewBag.TotalPages = administrativeOnly ? 1 : totalPages;
            ViewBag.TotalUsers = administrativeOnly ? unfilteredUsersCount : unfilteredUsersCount;
            ViewBag.FilteredUsers = administrativeOnly ? totalUsersCount : totalUsersCount;
            ViewBag.Search = search;
            ViewBag.RoleFilter = role;
            ViewBag.ResidenceGovernorateFilter = residenceGovernorate;
            ViewBag.WorkGovernorateFilter = workGovernorate;
            ViewBag.GenderFilter = gender;
            ViewBag.StatusFilter = status;
            ViewBag.ManagerLevelFilter = managerLevel;
            ViewBag.EducationFilter = education;
            ViewBag.ActiveUsers = administrativeOnly
                ? list.Count(u => u.IsActive)
                : await _userManager.Users.CountAsync(u => u.EmailConfirmed);
            ViewBag.Admins = administrativeOnly
                ? list.Count(u => u.Roles != null && u.Roles.Contains(clsRoles.Admin) && !u.Roles.Contains(clsRoles.SuperAdmin))
                : await _context.UserRoles
                    .Join(_context.Roles, userRole => userRole.RoleId, role => role.Id, (userRole, role) => role.Name)
                    .CountAsync(roleName => roleName == clsRoles.Admin);
            ViewBag.SuperAdmins = administrativeOnly
                ? list.Count(u => u.Roles != null && u.Roles.Contains(clsRoles.SuperAdmin))
                : await _context.UserRoles
                    .Join(_context.Roles, userRole => userRole.RoleId, role => role.Id, (userRole, role) => role.Name)
                    .CountAsync(roleName => roleName == clsRoles.SuperAdmin);
            ViewBag.DistrictAdmins = administrativeOnly
                ? list.Count(u => u.Roles != null && u.Roles.Contains(clsRoles.DistrictAdmin))
                : await _context.UserRoles
                    .Join(_context.Roles, userRole => userRole.RoleId, role => role.Id, (userRole, role) => role.Name)
                    .CountAsync(roleName => roleName == clsRoles.DistrictAdmin);
            ViewBag.AdministrativeManagers = administrativeOnly
                ? list.Count(u => u.IsManager && u.AssignmentRole == "Manager")
                : await _context.ManagementAssignments.AsNoTracking().CountAsync(x => x.AssignmentRole == "Manager");
            ViewBag.AdministrativeAssistants = administrativeOnly
                ? list.Count(u => u.IsManager && u.AssignmentRole == "Assistant")
                : await _context.ManagementAssignments.AsNoTracking().CountAsync(x => x.AssignmentRole == "Assistant");
            ViewBag.TotalIndividuals = administrativeOnly
                ? list.Count(u =>
                    !string.IsNullOrWhiteSpace(u.Education) &&
                    u.Education != "---" &&
                    ((u.AccountType != null && u.AccountType == "فرد") ||
                     (u.Roles != null && u.Roles.Contains("فرد"))))
                : await _context.Identifies.AsNoTracking().CountAsync(i =>
                    !string.IsNullOrWhiteSpace(i.Education) &&
                    (i.AccountType == "فرد" || i.IsPromoted));

            viewName = viewName == "AdministrativeManagers" ? viewName : "Users";
            return View(viewName, list);
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
            int pageSize)
        {
            var query = _context.Users.AsNoTracking().AsQueryable();

            if (administrativeOnly)
            {
                query = query.Where(u =>
                    _context.ManagementAssignments.Any(a => a.UserId == u.Id) ||
                    _context.UserRoles
                        .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, RoleName = r.Name ?? string.Empty })
                        .Any(r => r.UserId == u.Id &&
                                  (r.RoleName == clsRoles.Manager || r.RoleName == clsRoles.AssistantManager)));
            }

            var unfilteredUsersCount = await query.CountAsync();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(u =>
                    (u.Email != null && u.Email.Contains(term)) ||
                    (u.PhoneNumber != null && u.PhoneNumber.Contains(term)) ||
                    _context.Identifies.Any(i =>
                        i.UserId == u.Id &&
                        ((i.FullName != null && i.FullName.Contains(term)) ||
                         (i.PhoneNumber != null && i.PhoneNumber.Contains(term)) ||
                         (i.WhatsAppNumber != null && i.WhatsAppNumber.Contains(term)))) ||
                    _context.AffiliationInfos.Any(a =>
                        a.UserId == u.Id &&
                        ((a.BadgeNumber != null && a.BadgeNumber.Contains(term)) ||
                         _context.AffiliationEntities.Any(e => a.AffiliationEntityId == e.Id && e.Name.Contains(term)) ||
                         _context.Divisions.Any(d => a.DivisionId == d.Id && d.Name.Contains(term)) ||
                         _context.Sections.Any(s => a.SectionId == s.Id && s.Name.Contains(term)) ||
                         _context.Groups.Any(g => a.GroupId == g.Id && g.Name.Contains(term)))) ||
                    _context.ManagementAssignments.Any(a =>
                        a.UserId == u.Id &&
                        (_context.AffiliationEntities.Any(e => a.AffiliationEntityId == e.Id && e.Name.Contains(term)) ||
                         _context.Divisions.Any(d => a.DivisionId == d.Id && d.Name.Contains(term)) ||
                         _context.Sections.Any(s => a.SectionId == s.Id && s.Name.Contains(term)) ||
                         _context.Groups.Any(g => a.GroupId == g.Id && g.Name.Contains(term)))));
            }

            if (!string.IsNullOrWhiteSpace(role))
            {
                var normalizedRole = role.Trim();
                query = normalizedRole.Equals("member", StringComparison.OrdinalIgnoreCase)
                    ? query.Where(u =>
                        _context.Identifies.Any(i => i.UserId == u.Id && i.AccountType == "فرد") ||
                        _context.UserRoles
                            .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, RoleName = r.Name ?? string.Empty })
                            .Any(r => r.UserId == u.Id && r.RoleName == "فرد"))
                    : query.Where(u => _context.UserRoles
                        .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, RoleName = r.Name ?? string.Empty })
                        .Any(r => r.UserId == u.Id && r.RoleName == normalizedRole));
            }

            if (!string.IsNullOrWhiteSpace(residenceGovernorate))
                query = query.Where(u => _context.Addresses.Any(a => a.UserId == u.Id && a.Governorate == residenceGovernorate));

            if (!string.IsNullOrWhiteSpace(workGovernorate))
                query = query.Where(u => _context.Identifies.Any(i =>
                    i.UserId == u.Id &&
                    (i.ManagedGovernorate == workGovernorate ||
                     i.WorkGovernorate == workGovernorate ||
                     _context.WorkLocations.Any(w => w.IdentifyId == i.Id && w.Governorate == workGovernorate))));

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

            var totalUsersCount = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalUsersCount / (double)pageSize));
            page = Math.Min(page, totalPages);

            var users = await query
                .OrderByDescending(u => _context.UserRoles
                    .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, RoleName = r.Name ?? string.Empty })
                    .Any(r => r.UserId == u.Id && r.RoleName == clsRoles.SuperAdmin))
                .ThenByDescending(u => _context.UserRoles
                    .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, RoleName = r.Name ?? string.Empty })
                    .Any(r => r.UserId == u.Id && r.RoleName == clsRoles.Admin))
                .ThenByDescending(u => _context.UserRoles
                    .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, RoleName = r.Name ?? string.Empty })
                    .Any(r => r.UserId == u.Id && r.RoleName == clsRoles.DistrictAdmin))
                .ThenByDescending(u =>
                    _context.ManagementAssignments.Any(a => a.UserId == u.Id) ||
                    _context.UserRoles
                        .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, RoleName = r.Name ?? string.Empty })
                        .Any(r => r.UserId == u.Id && (r.RoleName == clsRoles.Manager || r.RoleName == clsRoles.AssistantManager)))
                .ThenBy(u => u.Email)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
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

            var affiliationEntityNames = await _context.AffiliationEntities.AsNoTracking().ToDictionaryAsync(e => e.Id, e => e.Name);
            var divisionNames = await _context.Divisions.AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.Name);
            var sectionNames = await _context.Sections.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name);
            var groupNames = await _context.Groups.AsNoTracking().ToDictionaryAsync(g => g.Id, g => g.Name);

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

                var governorateDisplay = roles.Contains(clsRoles.SuperAdmin)
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
            ViewBag.ActiveUsers = await _context.Users.CountAsync(u => u.EmailConfirmed);
            ViewBag.Admins = await _context.UserRoles.Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name).CountAsync(roleName => roleName == clsRoles.Admin);
            ViewBag.SuperAdmins = await _context.UserRoles.Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name).CountAsync(roleName => roleName == clsRoles.SuperAdmin);
            ViewBag.DistrictAdmins = await _context.UserRoles.Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name).CountAsync(roleName => roleName == clsRoles.DistrictAdmin);
            ViewBag.AdministrativeManagers = await _context.ManagementAssignments.AsNoTracking().CountAsync(x => x.AssignmentRole == "Manager");
            ViewBag.AdministrativeAssistants = await _context.ManagementAssignments.AsNoTracking().CountAsync(x => x.AssignmentRole == "Assistant");
            ViewBag.TotalIndividuals = await _context.Identifies.AsNoTracking().CountAsync(i => i.Education != null && i.Education != "" && (i.AccountType == "فرد" || i.IsPromoted));

            viewName = viewName == "AdministrativeManagers" ? viewName : "Users";
            return View(viewName, list);
        }

        private static int NormalizeUserManagementPageSize(int pageSize)
        {
            int[] allowedPageSizes = [10, 25, 50, 100];
            return allowedPageSizes.Contains(pageSize) ? pageSize : 10;
        }

        public async Task<IActionResult> AdministrativeManagers()
        {
            return await Users(administrativeOnly: true, viewName: "AdministrativeManagers");
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

            var totalRequests = await requestsQuery.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalRequests / (double)pageSize));
            page = Math.Max(1, Math.Min(page, totalPages));

            var pagedRequests = await requestsQuery
                .OrderByDescending(i => i.CreatedAt)
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
                    RequestDate = p.CreatedAt,
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
                    ? i.CreatedAt
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
                        ? identify.CreatedAt
                        : identify.RequestedPromotionDate ?? identify.CreatedAt,
                    ProcessedAt = normalizedType == "basic"
                        ? identify.BasicInfoApprovalDate
                        : identify.PromotionDate,
                    ProcessedBy = normalizedType == "basic"
                        ? identify.BasicInfoApprovedBy
                        : identify.PromotedBy,
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
                    (i.WorkGovernorate != null && i.WorkGovernorate.Contains(governorate)) ||
                    _context.WorkLocations.Any(w => w.IdentifyId == i.Id && w.Governorate != null && w.Governorate.Contains(governorate)));
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
                identify.PromotedBy = User.Identity?.Name ?? "System";
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
                    await _notificationService.CreateNotification(
                        "🎉 تهانينا! تمت الموافقة على طلب الترقية",
                        "تمت ترقية حسابك إلى 'فرد' بنجاح. يمكنك الآن الاستفادة من جميع الخدمات.",
                        identify.UserId,
                        "bi-star-fill",
                        "/Register/ProfileDetails"
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
                identify.PromotedBy = User.Identity?.Name ?? "System";

                _context.Identifies.Update(identify);
                await _context.SaveChangesAsync();

                try
                {
                    await _notificationService.CreateNotification(
                        "❌ عذراً، لم يتم الموافقة على طلبك",
                        $"سبب الرفض: {reason}",
                        identify.UserId,
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
                identify.BasicInfoApprovedBy = User.Identity?.Name ?? "System";
                identify.BasicInfoApprovalDate = DateTime.Now;

                _context.Identifies.Update(identify);
                await _context.SaveChangesAsync();

                try
                {
                    await _notificationService.CreateNotification(
                        "✅ تمت الموافقة على بياناتك الأساسية",
                        "يمكنك الآن إكمال البيانات الإضافية",
                        identify.UserId,
                        "bi-check-circle",
                        "/Register/AdditionalInfo"
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
                identify.BasicInfoApprovedBy = User.Identity?.Name ?? "System";
                identify.BasicInfoApprovalDate = null;

                _context.Identifies.Update(identify);
                await _context.SaveChangesAsync();

                try
                {
                    await _notificationService.CreateNotification(
                        "❌ لم يتم الموافقة على بياناتك الأساسية",
                        $"عذراً، لم تتم الموافقة على بياناتك الأساسية.\nسبب الرفض: {reason}",
                        identify.UserId,
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
                    PromotedBy = userProfile?.PromotedBy,
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
        public async Task<IActionResult> SendNotification(string? userId = null)
        {
            var users = await _userManager.Users.ToListAsync();
            ViewBag.Users = new List<object>();

            foreach (var u in users)
            {
                var profile = await _context.Identifies
                    .FirstOrDefaultAsync(i => i.UserId == u.Id);
                var fullName = profile?.FullName ?? u.Email;

                ((List<object>)ViewBag.Users).Add(new { u.Id, u.Email, FullName = fullName });
            }

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
                }
            }

            return View();
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

                var notification = await _notificationService.CreateNotification(
                    model.Title, model.Message, model.TargetUserId,
                    model.Icon ?? "bi-bell", model.ClickUrl);

                bool oneSignalResult = false;
                string oneSignalMessage = "";

                try
                {
                    if (!string.IsNullOrEmpty(model.TargetUserId))
                    {
                        var playerIds = await _context.UserDevices
                            .Where(d => d.UserId == model.TargetUserId && d.IsSubscribed)
                            .Select(d => d.PlayerId)
                            .ToListAsync();

                        if (playerIds.Any())
                        {
                            oneSignalResult = await _notificationService.SendToOneSignal(notification, playerIds);
                            oneSignalMessage = $"تم الإرسال إلى {playerIds.Count} جهاز";
                        }
                        else
                        {
                            oneSignalResult = await _notificationService.SendToOneSignal(notification, null, model.TargetUserId);
                            oneSignalMessage = "تم الإرسال باستخدام معرف المستخدم";
                        }
                    }
                    else
                    {
                        oneSignalResult = await _notificationService.SendToOneSignal(notification);
                        oneSignalMessage = "تم الإرسال لجميع المستخدمين";
                    }

                    if (oneSignalResult)
                        TempData["SuccessMessage"] = $"✅ تم إرسال الإشعار بنجاح. {oneSignalMessage}";
                    else
                        TempData["WarningMessage"] = "⚠️ تم حفظ الإشعار ولكن فشل الإرسال عبر OneSignal";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ خطأ في إرسال OneSignal");
                    TempData["WarningMessage"] = $"⚠️ تم حفظ الإشعار ولكن حدث خطأ: {ex.Message}";
                }

                return RedirectToAction("Users");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ عام في إرسال الإشعار");
                TempData["ErrorMessage"] = $"❌ حدث خطأ: {ex.Message}";
                return RedirectToAction("SendNotification");
            }
        }

        // ========== تحديث أدوار المستخدم ==========
        [HttpGet]
        public async Task<IActionResult> GetUserRoles(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                    return Json(new { success = false, message = "معرف المستخدم مطلوب" });

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                    return Json(new { success = false, message = "المستخدم غير موجود" });

                var userRoles = await _userManager.GetRolesAsync(user);
                var allRoles = new List<string> { "User", "Admin", "SuperAdmin", "NewsEditor", "MapViewer" };

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
                if (request == null || string.IsNullOrEmpty(request.UserId))
                    return Json(new { success = false, message = "بيانات غير صالحة" });

                var user = await _userManager.FindByIdAsync(request.UserId);
                if (user == null)
                    return Json(new { success = false, message = "المستخدم غير موجود" });

                var currentRoles = await _userManager.GetRolesAsync(user);
                if (currentRoles.Any())
                {
                    var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
                    if (!removeResult.Succeeded)
                        return Json(new { success = false, message = "فشل في إزالة الأدوار الحالية" });
                }

                if (request.SelectedRoles != null)
                {
                    request.SelectedRoles = request.SelectedRoles
                        .Select(role => role == clsRoles.DistrictAdmin ? clsRoles.Admin : role)
                        .Distinct()
                        .ToList();
                }

                if (request.SelectedRoles != null && request.SelectedRoles.Any())
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
                        BasicInfoApprovedBy = User.Identity?.Name ?? "System"
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
                    if (request.SelectedRoles.Contains("SuperAdmin"))
                        userProfile.AccountType = "سوبر أدمن";
                    else if (request.SelectedRoles.Contains("Admin"))
                        userProfile.AccountType = "أدمن";
                    else if (request.SelectedRoles.Contains("فرد"))
                    {
                        userProfile.AccountType = "فرد";
                        userProfile.IsPromoted = true;
                        userProfile.PromotionDate ??= DateTime.Now;
                        userProfile.PromotedBy ??= User.Identity?.Name ?? "System";
                        userProfile.RequestedPromotion = false;
                        userProfile.RequestedPromotionDate = null;
                        userProfile.RejectionReason = null;
                        userProfile.IsBasicInfoApproved = true;
                        userProfile.BasicInfoApprovalDate ??= DateTime.Now;
                        userProfile.BasicInfoApprovedBy ??= User.Identity?.Name ?? "System";
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
                            await _notificationService.CreateNotification("👑 تمت ترقيتك إلى سوبر أدمن", "تمت ترقية حسابك إلى سوبر أدمن", request.UserId, "bi-crown-fill", "/SuperAdmin/Users");
                        else if (request.SelectedRoles.Contains("Admin"))
                            await _notificationService.CreateNotification("🛡️ تمت ترقيتك إلى أدمن", "تمت ترقية حسابك إلى أدمن", request.UserId, "bi-shield-fill", "/Admin/Users");
                        else if (request.SelectedRoles.Contains("فرد"))
                            await _notificationService.CreateNotification("⭐ تمت ترقيتك إلى فرد", "تمت ترقية حسابك إلى فرد", request.UserId, "bi-star-fill", "/Register/ProfileDetails");
                        else if (request.SelectedRoles.Contains("NewsEditor"))
                            await _notificationService.CreateNotification("📝 تم تعيينك كمحرر أخبار", "يمكنك الآن إدارة الأخبار", request.UserId, "bi-newspaper", "/News/Index");
                        else if (request.SelectedRoles.Contains("MapViewer"))
                            await _notificationService.CreateNotification("🗺️ تم تعيينك كمشاهد خريطة", "يمكنك الآن مشاهدة الخريطة", request.UserId, "bi-map", "/MapDashboard/Index");
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

        // ========== حذف مستخدم واحد ==========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser([FromBody] DeleteUserRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.UserId))
                    return Json(new { success = false, message = "معرف المستخدم مطلوب" });

                var user = await _userManager.FindByIdAsync(request.UserId);
                if (user == null)
                    return Json(new { success = false, message = "المستخدم غير موجود" });

                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains(clsRoles.SuperAdmin))
                    return Json(new { success = false, message = "لا يمكن حذف حساب سوبر أدمن" });

                // حذف جميع البيانات المرتبطة
                var address = await GetUserAddressAsync(request.UserId);
                if (address != null) _context.Addresses.Remove(address);

                var affiliation = await GetUserAffiliationInfoAsync(request.UserId);
                if (affiliation != null) _context.AffiliationInfos.Remove(affiliation);

                var voterCard = await GetUserVoterCardAsync(request.UserId);
                if (voterCard != null) _context.VoterCards.Remove(voterCard);

                var union = await GetUserUnionAsync(request.UserId);
                if (union != null) _context.UnionMemberships.Remove(union);

                var federation = await GetUserFederationAsync(request.UserId);
                if (federation != null) _context.FederationMemberships.Remove(federation);

                var association = await GetUserAssociationAsync(request.UserId);
                if (association != null) _context.AssociationMemberships.Remove(association);

                var ngo = await GetUserNgoAsync(request.UserId);
                if (ngo != null) _context.NgoMemberships.Remove(ngo);

                var notifications = await _context.Notifications.Where(n => n.TargetUserId == request.UserId).ToListAsync();
                if (notifications.Any()) _context.Notifications.RemoveRange(notifications);

                var devices = await _context.UserDevices.Where(d => d.UserId == request.UserId).ToListAsync();
                if (devices.Any()) _context.UserDevices.RemoveRange(devices);

                var userProfile = await _context.Identifies.FirstOrDefaultAsync(i => i.UserId == request.UserId);
                if (userProfile != null) _context.Identifies.Remove(userProfile);

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
                        if (roles.Contains(clsRoles.SuperAdmin)) { failCount++; continue; }

                        // حذف جميع البيانات المرتبطة
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
                    PromotedBy = profile?.PromotedBy
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
                    : string.Equals(model.WorkLocation.Governorate, "بغداد مركزي", StringComparison.OrdinalIgnoreCase)
                    ? "بغداد مركزي"
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
                    await _notificationService.CreateNotification("📝 تم تعديل بياناتك الشخصية", "للاطلاع قم بزيارة ملفك الشخصي", model.UserId, "bi-pencil-square", "/Register/ProfileDetails");
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
                        values.Add("بغداد مركزي");
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
                string.Equals(normalizedSelectedValue, "بغداد مركزي", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(normalizedValue, "بغداد مركزي", StringComparison.OrdinalIgnoreCase) ||
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
                ? "بغداد مركزي"
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
                var allUsers = await _userManager.Users.ToListAsync();
                var adminUsers = new List<IdentityUser>();

                foreach (var user in allUsers)
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    if (roles.Contains(clsRoles.Admin) || roles.Contains(clsRoles.SuperAdmin))
                    {
                        adminUsers.Add(user);
                    }
                }

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

            int counter = 1;
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var userProfile = await _context.Identifies.FirstOrDefaultAsync(i => i.UserId == user.Id);
                var address = userProfile != null ? await GetUserAddressAsync(user.Id) : null;
                var workLocation = userProfile != null
                    ? await _context.WorkLocations.AsNoTracking().FirstOrDefaultAsync(w => w.IdentifyId == userProfile.Id)
                    : null;
                var voterCard = userProfile != null ? await GetUserVoterCardAsync(user.Id) : null;
                var union = userProfile != null ? await GetUserUnionAsync(user.Id) : null;
                var federation = userProfile != null ? await GetUserFederationAsync(user.Id) : null;
                var association = userProfile != null ? await GetUserAssociationAsync(user.Id) : null;
                var ngo = userProfile != null ? await GetUserNgoAsync(user.Id) : null;
                var affiliationInfo = userProfile != null ? await GetUserAffiliationInfoAsync(user.Id) : null;

                // أسماء الكيانات للانتساب
                var affiliationEntityName = await GetAffiliationEntityNameAsync(affiliationInfo?.AffiliationEntityId);
                var divisionName = await GetDivisionNameAsync(affiliationInfo?.DivisionId);
                var sectionName = await GetSectionNameAsync(affiliationInfo?.SectionId);
                var groupName = await GetGroupNameAsync(affiliationInfo?.GroupId);

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
                var managementAssignments = await _context.ManagementAssignments
                    .Where(x => x.UserId == user.Id)
                    .ToListAsync();

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
                        var entity = await _context.AffiliationEntities
                            .FirstOrDefaultAsync(e => e.Id == assignment.AffiliationEntityId.Value);
                        entityName = entity?.Name ?? "";
                    }
                    else if (assignment.ManagementLevel == "Division" && assignment.DivisionId.HasValue)
                    {
                        var division = await _context.Divisions
                            .FirstOrDefaultAsync(d => d.Id == assignment.DivisionId.Value);
                        entityName = division?.Name ?? "";
                    }
                    else if (assignment.ManagementLevel == "Section" && assignment.SectionId.HasValue)
                    {
                        var section = await _context.Sections
                            .FirstOrDefaultAsync(s => s.Id == assignment.SectionId.Value);
                        entityName = section?.Name ?? "";
                    }
                    else if (assignment.ManagementLevel == "Group" && assignment.GroupId.HasValue)
                    {
                        var group = await _context.Groups
                            .FirstOrDefaultAsync(g => g.Id == assignment.GroupId.Value);
                        entityName = group?.Name ?? "";
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
            string.Join(", ", roles),
            userProfile?.AccountType ?? "عادي",
            userProfile?.IsPromoted == true ? "مصعد" : "غير مصعد",
            userProfile?.PromotionDate?.ToString("yyyy-MM-dd") ?? "",
            userProfile?.PromotedBy ?? "",
            userProfile?.CreatedAt.ToString("yyyy-MM-dd") ?? "",
            user.EmailConfirmed ? "نشط" : "غير نشط",

            // ===== معلومات إضافية =====
            userProfile?.RequestedPromotion == true ? "نعم" : "لا",
            userProfile?.RequestedPromotionDate?.ToString("yyyy-MM-dd") ?? "",
            userProfile?.RejectionReason ?? "",
            managementDisplay,
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
                        worksheet.Cell(i + 1, j + 1).Value = data[i][j]?.ToString() ?? "";

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

                memberList.Add(new { member.Id, member.UserId, UserEmail = user?.Email ?? "", member.FullName, member.PhoneNumber, Governorate = GetEffectiveGovernorate(member, address), District = GetEffectiveDistrict(member, address), member.IdentityCardN, member.Date, member.Gender, member.Education, member.EmploymentStatus, member.Work, member.Position, HasUnion = union != null, UnionName = union?.UnionName, HasFederation = federation != null, FederationName = GetFederationFullName(federation), HasAssociation = association != null, AssociationName = association?.AssociationName, HasNgo = ngo != null, NgoName = ngo?.NgoName, AffiliationEntity = affiliationEntityName, Division = divisionName, Section = sectionName, Group = groupName, member.PromotionDate, member.PromotedBy, member.CreatedAt });
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
        public class UpdateRolesRequest
        {
            public string UserId { get; set; } = string.Empty;
            public List<string> SelectedRoles { get; set; } = new();
            public string? ManagedGovernorate { get; set; }  // ✅ أضف هذا
            public string? ManagedDistrict { get; set; }     // ✅ أضف هذا
        }
        public class SendNotificationViewModel { public string Title { get; set; } = string.Empty; public string Message { get; set; } = string.Empty; public string? TargetUserId { get; set; } public string? Icon { get; set; } public string? ClickUrl { get; set; } }
    }
}
