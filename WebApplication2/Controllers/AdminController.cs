using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebApplication2.Data;
using WebApplication2.Models;
using WebApplication2.Models.Profile;
using WebApplication2.Services;

namespace WebApplication2.Controllers
{
    [Authorize(Roles = clsRoles.Admin + "," + clsRoles.DistrictAdmin)]
    public class AdminController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly INotificationService _notificationService;
        private readonly ILogger<AdminController> _logger;
        private readonly ApplicationDbContext _context;

        public AdminController(
            UserManager<IdentityUser> userManager,
            ApplicationDbContext context,
            INotificationService notificationService,
            ILogger<AdminController> logger)
        {
            _userManager = userManager;
            _context = context;
            _notificationService = notificationService;
            _logger = logger;
        }

        // ===== دوال مساعدة لجلب البيانات المرتبطة =====
        private async Task<Address?> GetUserAddressAsync(string userId)
        {
            return await _context.Addresses.FirstOrDefaultAsync(a => a.UserId == userId);
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

        private async Task<VoterCard?> GetUserVoterCardAsync(string userId)
        {
            return await _context.VoterCards.FirstOrDefaultAsync(v => v.UserId == userId);
        }

        private async Task<UnionMembership?> GetUserUnionAsync(string userId)
        {
            return await _context.UnionMemberships.FirstOrDefaultAsync(u => u.UserId == userId);
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
            return await _context.AssociationMemberships.FirstOrDefaultAsync(a => a.UserId == userId);
        }

        private async Task<NgoMembership?> GetUserNgoAsync(string userId)
        {
            return await _context.NgoMemberships.FirstOrDefaultAsync(n => n.UserId == userId);
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

        private async Task<Dictionary<string, IdentityUser>> GetUsersByIdsAsync(IEnumerable<string> userIds)
        {
            var ids = userIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();

            if (ids.Count == 0)
            {
                return new Dictionary<string, IdentityUser>();
            }

            return await _userManager.Users
                .AsNoTracking()
                .Where(u => ids.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id);
        }

        private async Task<Dictionary<string, Address>> GetAddressesByUserIdsAsync(IEnumerable<string> userIds)
        {
            var ids = userIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();

            if (ids.Count == 0)
            {
                return new Dictionary<string, Address>();
            }

            return await _context.Addresses
                .AsNoTracking()
                .Where(a => ids.Contains(a.UserId))
                .GroupBy(a => a.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.First());
        }

        private async Task<Dictionary<string, VoterCard>> GetVoterCardsByUserIdsAsync(IEnumerable<string> userIds)
        {
            var ids = userIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();

            if (ids.Count == 0)
            {
                return new Dictionary<string, VoterCard>();
            }

            return await _context.VoterCards
                .AsNoTracking()
                .Where(v => ids.Contains(v.UserId))
                .GroupBy(v => v.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.First());
        }

        private async Task<Dictionary<string, AffiliationInfo>> GetAffiliationsByUserIdsAsync(IEnumerable<string> userIds)
        {
            var ids = userIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();

            if (ids.Count == 0)
            {
                return new Dictionary<string, AffiliationInfo>();
            }

            return await _context.AffiliationInfos
                .AsNoTracking()
                .Include(a => a.AffiliationEntity)
                .Include(a => a.Division)
                .Include(a => a.Section)
                .Include(a => a.Group)
                .Where(a => ids.Contains(a.UserId))
                .GroupBy(a => a.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.First());
        }

        // ===== دوال مساعدة للحصول على أسماء الكيانات =====
        private async Task<string?> GetAffiliationEntityNameAsync(int? id)
        {
            if (!id.HasValue) return null;
            var entity = await _context.AffiliationEntities.FirstOrDefaultAsync(e => e.Id == id.Value);
            return entity?.Name;
        }

        private async Task<string?> GetDivisionNameAsync(int? id)
        {
            if (!id.HasValue) return null;
            var division = await _context.Divisions.FirstOrDefaultAsync(d => d.Id == id.Value);
            return division?.Name;
        }

        private async Task<string?> GetSectionNameAsync(int? id)
        {
            if (!id.HasValue) return null;
            var section = await _context.Sections.FirstOrDefaultAsync(s => s.Id == id.Value);
            return section?.Name;
        }

        private async Task<string?> GetGroupNameAsync(int? id)
        {
            if (!id.HasValue) return null;
            var group = await _context.Groups.FirstOrDefaultAsync(g => g.Id == id.Value);
            return group?.Name;
        }

        private string GetFederationFullName(FederationMembership? federation)
        {
            if (federation == null) return "";
            string fullName = "";
            if (federation.Federation != null) fullName = federation.Federation.Name;
            if (federation.FederationDivision != null) fullName += " - " + federation.FederationDivision.Name;
            if (federation.FederationSection != null) fullName += " - " + federation.FederationSection.Name;
            if (federation.FederationGroup != null) fullName += " - " + federation.FederationGroup.Name;
            return fullName;
        }
        // ===== الحصول على محافظة الأدمن الحالي =====
        private async Task<string?> GetAdminGovernorateAsync()
        {
            var currentUserId = _userManager.GetUserId(User);
            var adminProfile = await _context.Identifies.FirstOrDefaultAsync(i => i.UserId == currentUserId);
            return adminProfile?.ManagedGovernorate;
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

        // ===== الحصول على قضاء الأدمن الحالي إذا كان نطاقه بغداد/كرخ أو رصافة =====
        private async Task<string?> GetAdminDistrictAsync()
        {
            await Task.CompletedTask;
            return null;
        }

        // ===== التحقق من أن المستخدم داخل محافظة وقضاء الأدمن =====
        private async Task<bool> IsUserInAdminGovernorateAsync(string userId)
        {
            var adminGovernorate = await GetAdminGovernorateAsync();
            var adminDistrict = await GetAdminDistrictAsync();

            if (string.IsNullOrEmpty(adminGovernorate)) return false;

            var userProfile = await GetUserProfileAsync(userId);
            var userAddress = await GetUserAddressAsync(userId);
            if (userProfile == null && userAddress == null) return false;

            if (!IsGovernorateInManagedScope(GetEffectiveGovernorate(userProfile, userAddress), adminGovernorate)) return false;

            if (!string.IsNullOrEmpty(adminDistrict))
            {
                return GetEffectiveDistrict(userProfile, userAddress) == adminDistrict;
            }

            return true;
        }

        // ===== الحصول على معرفات المستخدمين في محافظة وقضاء الأدمن =====
        private async Task<List<string>> GetUserIdsInAdminGovernorateAsync()
        {
            var adminGovernorate = await GetAdminGovernorateAsync();
            var adminDistrict = await GetAdminDistrictAsync();
            var hasDistrictScope = !string.IsNullOrEmpty(adminDistrict);

            if (string.IsNullOrEmpty(adminGovernorate)) return new List<string>();

            var allProfiles = await _context.Identifies
                .AsNoTracking()
                .Include(i => i.WorkLocation)
                .ToListAsync();
            var allUserIds = allProfiles
                .Where(i => !string.IsNullOrWhiteSpace(i.UserId))
                .Select(i => i.UserId)
                .Distinct()
                .ToList();
            var profilesByUserId = allProfiles
                .Where(i => !string.IsNullOrWhiteSpace(i.UserId))
                .GroupBy(i => i.UserId)
                .ToDictionary(g => g.Key, g => g.First());
            var rolesByUserId = await _context.UserRoles
                .AsNoTracking()
                .Join(_context.Roles,
                    userRole => userRole.RoleId,
                    role => role.Id,
                    (userRole, role) => new { userRole.UserId, RoleName = role.Name ?? string.Empty })
                .Where(x => allUserIds.Contains(x.UserId))
                .GroupBy(x => x.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.Select(x => x.RoleName).ToHashSet());
            var userIds = new List<string>();

            foreach (var userId in allUserIds)
            {
                var userRoles = rolesByUserId.TryGetValue(userId, out var roles)
                    ? roles
                    : new HashSet<string>();

                // ❌ لا نضيف SuperAdmin أبداً
                if (userRoles.Contains(clsRoles.SuperAdmin))
                {
                    continue; // تخطي السوبر أدمن
                }

                // ✅ نضيف DistrictAdmin (مسؤول القضاء) دائماً
                if (userRoles.Contains(clsRoles.DistrictAdmin))
                {
                    profilesByUserId.TryGetValue(userId, out var districtAdminProfile);

                    if (districtAdminProfile != null &&
                        IsGovernorateInManagedScope(districtAdminProfile.ManagedGovernorate, adminGovernorate))
                    {
                        if (hasDistrictScope)
                        {
                            if (districtAdminProfile.ManagedDistrict == adminDistrict)
                                userIds.Add(userId);
                        }
                        else
                        {
                            userIds.Add(userId);
                        }
                    }

                    continue;
                }

                // ✅ نضيف Admin (مسؤول محافظة)؟ 
                // إذا كنت لا تريد عرضهم أيضاً، يمكنك تخطيهم
                if (userRoles.Contains(clsRoles.Admin))
                {
                    // إذا كنت تريد عرض مسؤولي المحافظات الآخرين، أضفهم
                    // userIds.Add(user.Id);
                    // continue;

                    // إذا كنت لا تريد عرضهم، استمر
                    continue;
                }

                profilesByUserId.TryGetValue(userId, out var userProfile);
                if (userProfile != null &&
                    IsGovernorateInManagedScope(GetEffectiveGovernorate(userProfile, null), adminGovernorate))
                {
                    if (hasDistrictScope)
                    {
                        if (GetEffectiveDistrict(userProfile, null) == adminDistrict)
                            userIds.Add(userId);
                    }
                    else
                    {
                        userIds.Add(userId);
                    }
                }
            }

            return userIds;
        }

        // ===== الصفحة الرئيسية (Dashboard) =====
        public async Task<IActionResult> Index()
        {
            var adminGovernorate = await GetAdminGovernorateAsync();
            if (string.IsNullOrEmpty(adminGovernorate))
            {
                ViewBag.ErrorMessage = "❌ لم يتم تعيين محافظة لك.";
                return View();
            }

            // ✅ جلب بيانات الأدمن الحالي ونوع صلاحيته
            var currentUserId = _userManager.GetUserId(User);
            var adminProfile = await _context.Identifies
                .FirstOrDefaultAsync(i => i.UserId == currentUserId);

            string adminTypeMessage = "";
            string adminTypeIcon = "";
            string adminTypeColor = "";

            if (adminGovernorate == "بغداد عامة")
            {
                adminTypeMessage = "أنت تدير بغداد بالكامل (الكرخ والرصافة)";
                adminTypeIcon = "🌍";
                adminTypeColor = "info";
            }
            else if (adminGovernorate == "بغداد")
            {
                if (string.IsNullOrEmpty(adminProfile?.ManagedDistrict))
                {
                    adminTypeMessage = "أنت تدير بغداد بالكامل (الكرخ والرصافة)";
                    adminTypeIcon = "🌍";
                    adminTypeColor = "info";
                }
                else if (adminProfile?.ManagedDistrict == "الكرخ")
                {
                    adminTypeMessage = "أنت تدير قضاء الكرخ فقط";
                    adminTypeIcon = "🏘️";
                    adminTypeColor = "success";
                }
                else if (adminProfile?.ManagedDistrict == "الرصافة")
                {
                    adminTypeMessage = "أنت تدير قضاء الرصافة فقط";
                    adminTypeIcon = "🏘️";
                    adminTypeColor = "success";
                }
                else
                {
                    adminTypeMessage = $"أنت تدير محافظة: {adminGovernorate}";
                    adminTypeIcon = "📍";
                    adminTypeColor = "primary";
                }
            }
            else
            {
                adminTypeMessage = $"أنت تدير محافظة: {adminGovernorate}";
                adminTypeIcon = "📍";
                adminTypeColor = "primary";
            }

            ViewBag.AdminTypeMessage = adminTypeMessage;
            ViewBag.AdminTypeIcon = adminTypeIcon;
            ViewBag.AdminTypeColor = adminTypeColor;

            var userIdsInGovernorate = await GetUserIdsInAdminGovernorateAsync();
            var scopedProfiles = _context.Identifies.AsNoTracking().Where(i => userIdsInGovernorate.Contains(i.UserId));

            ViewBag.TotalUsers = await scopedProfiles.CountAsync();
            ViewBag.TotalMembers = await scopedProfiles.CountAsync(i =>
                !string.IsNullOrWhiteSpace(i.Education) &&
                (i.AccountType == "فرد" || i.IsPromoted));
            ViewBag.PendingRequests = await scopedProfiles.CountAsync(i => i.RequestedPromotion && !i.IsPromoted);
            ViewBag.NewThisWeek = await scopedProfiles.CountAsync(i => i.CreatedAt > DateTime.UtcNow.AddDays(-7));
            ViewBag.ManagedGovernorate = adminGovernorate;

            return View();
        }

        // ===== عرض جميع المستخدمين في المحافظة =====
        public async Task<IActionResult> Users(
            bool administrativeOnly = false,
            string viewName = "Users",
            int page = 1,
            string? search = null,
            string? role = null,
            string? status = null,
            string? managerLevel = null,
            string? education = null,
            string? profileStage = null,
            int pageSize = 10)
        {
            try
            {
                pageSize = NormalizeUserManagementPageSize(pageSize);
                page = Math.Max(1, page);

                return await UsersPagedFromDatabaseAsync(administrativeOnly, viewName, page, search, role, status, managerLevel, education, profileStage, pageSize);

                if (administrativeOnly && !User.IsInRole(clsRoles.Admin))
                {
                    return Forbid();
                }

                var adminGovernorate = await GetAdminGovernorateAsync();
                if (string.IsNullOrEmpty(adminGovernorate))
                {
                    ViewBag.ErrorMessage = "❌ لم يتم تعيين محافظة لك.";
                    viewName = viewName == "AdministrativeManagers" ? viewName : "Users";
                    return View(viewName, new List<AdminUserVM>());
                }

                var currentUserId = _userManager.GetUserId(User);
                var adminDistrict = await GetAdminDistrictAsync();
                var hasDistrictScope = !string.IsNullOrEmpty(adminDistrict);

                var allIdentifies = await _context.Identifies
                    .AsNoTracking()
                    .ToListAsync();
                var allUserIds = allIdentifies.Select(i => i.UserId).ToHashSet();
                var usersById = await _userManager.Users
                    .AsNoTracking()
                    .Where(u => allUserIds.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id);

                var rolesByUserId = await _context.UserRoles
                    .Where(ur => allUserIds.Contains(ur.UserId))
                    .Join(
                        _context.Roles,
                        userRole => userRole.RoleId,
                        role => role.Id,
                        (userRole, role) => new { userRole.UserId, RoleName = role.Name ?? string.Empty })
                    .GroupBy(x => x.UserId)
                    .ToDictionaryAsync(g => g.Key, g => (IList<string>)g.Select(x => x.RoleName).ToList());

                var profilesByUserId = allIdentifies
                    .GroupBy(i => i.UserId)
                    .ToDictionary(g => g.Key, g => g.First());

                var profileIds = allIdentifies.Select(i => i.Id).ToHashSet();

                var addressesByUserId = await _context.Addresses
                    .AsNoTracking()
                    .Where(a => allUserIds.Contains(a.UserId))
                    .GroupBy(a => a.UserId)
                    .ToDictionaryAsync(g => g.Key, g => g.First());

                var voterCardsByUserId = await _context.VoterCards
                    .AsNoTracking()
                    .Where(v => allUserIds.Contains(v.UserId))
                    .GroupBy(v => v.UserId)
                    .ToDictionaryAsync(g => g.Key, g => g.First());

                var workLocationsByProfileId = await _context.WorkLocations
                    .AsNoTracking()
                    .Where(w => profileIds.Contains(w.IdentifyId))
                    .GroupBy(w => w.IdentifyId)
                    .ToDictionaryAsync(g => g.Key, g => g.First());

                var assignmentsByUserId = await _context.ManagementAssignments
                    .AsNoTracking()
                    .Where(x => allUserIds.Contains(x.UserId))
                    .GroupBy(x => x.UserId)
                    .ToDictionaryAsync(g => g.Key, g => g.ToList());

                var affiliationInfosByUserId = await _context.AffiliationInfos
                    .AsNoTracking()
                    .Where(x => allUserIds.Contains(x.UserId))
                    .GroupBy(x => x.UserId)
                    .ToDictionaryAsync(g => g.Key, g => g.First());

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

                string BuildUserSearchText(Identify profile, IdentityUser? user, IEnumerable<ManagementAssignment> assignments)
                {
                    var assignmentText = string.Join(" ", assignments.Select(GetManagedEntityDisplayName));
                    return NormalizeSearchText(string.Join(" ", new[]
                    {
                        profile.FullName,
                        user?.Email,
                        user?.PhoneNumber,
                        profile.PhoneNumber,
                        profile.WhatsAppNumber,
                        GetAffiliationSearchText(profile.UserId),
                        assignmentText
                    }));
                }

                bool IsAllowedInManagedArea(Identify profile)
                {
                    var roles = rolesByUserId.TryGetValue(profile.UserId, out var userRoles)
                        ? userRoles
                        : new List<string>();

                    if (roles.Contains(clsRoles.SuperAdmin))
                        return false;

                    if (roles.Contains(clsRoles.Admin))
                        return false;

                    if (roles.Contains(clsRoles.DistrictAdmin))
                    {
                        if (!IsGovernorateInManagedScope(profile.ManagedGovernorate, adminGovernorate))
                            return false;

                        return !hasDistrictScope || profile.ManagedDistrict == adminDistrict;
                    }

                    if (!IsGovernorateInManagedScope(GetEffectiveGovernorateFast(profile), adminGovernorate))
                        return false;

                    return !hasDistrictScope || GetEffectiveDistrictFast(profile) == adminDistrict;
                }

                var usersInGovernorate = allIdentifies
                    .Where(IsAllowedInManagedArea)
                    .ToList();

                // ترتيب أصحاب الصلاحيات والمسؤوليات أولاً ثم الباقي حسب الاسم
                usersInGovernorate = usersInGovernorate
                    .OrderByDescending(i => i.UserId == currentUserId)
                    .ThenByDescending(i =>
                        rolesByUserId.TryGetValue(i.UserId, out var roles) &&
                        roles.Contains(clsRoles.DistrictAdmin))
                    .ThenByDescending(i =>
                        assignmentsByUserId.ContainsKey(i.UserId) ||
                        (rolesByUserId.TryGetValue(i.UserId, out var roles) &&
                         (roles.Contains(clsRoles.Manager) || roles.Contains(clsRoles.AssistantManager))))
                    .ThenBy(i => i.FullName)
                    .ToList();

                var unfilteredUsersCount = usersInGovernorate.Count;
                var unfilteredActiveUsersCount = usersInGovernorate.Count(i =>
                    usersById.TryGetValue(i.UserId, out var user) && user.EmailConfirmed);
                var unfilteredPromotedUsersCount = usersInGovernorate.Count(i =>
                    !string.IsNullOrWhiteSpace(i.Education) &&
                    (i.AccountType == "فرد" ||
                     i.IsPromoted ||
                     (rolesByUserId.TryGetValue(i.UserId, out var roles) && roles.Contains("فرد"))));

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var normalizedSearch = NormalizeSearchText(search);
                    var normalizedSearchDigits = NormalizeSearchDigits(search);
                    usersInGovernorate = usersInGovernorate
                        .Where(i =>
                        {
                            usersById.TryGetValue(i.UserId, out var user);
                            var assignments = assignmentsByUserId.TryGetValue(i.UserId, out var userAssignments)
                                ? userAssignments
                                : new List<ManagementAssignment>();
                            var searchText = BuildUserSearchText(i, user, assignments);
                            var phoneDigits = NormalizeSearchDigits($"{user?.PhoneNumber} {i.PhoneNumber} {i.WhatsAppNumber}");

                            return searchText.Contains(normalizedSearch) ||
                                   (!string.IsNullOrWhiteSpace(normalizedSearchDigits) && phoneDigits.Contains(normalizedSearchDigits));
                        })
                        .ToList();
                }

                if (!string.IsNullOrWhiteSpace(role))
                {
                    var normalizedRole = role.Trim().ToLower();
                    usersInGovernorate = usersInGovernorate
                        .Where(i =>
                        {
                            var roles = rolesByUserId.TryGetValue(i.UserId, out var userRoles)
                                ? userRoles
                                : new List<string>();

                            return normalizedRole == "member" || normalizedRole == "فرد"
                                ? i.AccountType == "فرد" || roles.Contains("فرد")
                                : roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
                        })
                        .ToList();
                }

                if (status == "active")
                {
                    usersInGovernorate = usersInGovernorate
                        .Where(i => usersById.TryGetValue(i.UserId, out var user) && user.EmailConfirmed)
                        .ToList();
                }
                else if (status == "inactive")
                {
                    usersInGovernorate = usersInGovernorate
                        .Where(i => !usersById.TryGetValue(i.UserId, out var user) || !user.EmailConfirmed)
                        .ToList();
                }

                if (!string.IsNullOrWhiteSpace(managerLevel))
                {
                    var parts = managerLevel.Split('-', 2);
                    if (parts.Length == 2)
                    {
                        usersInGovernorate = usersInGovernorate
                            .Where(i =>
                                assignmentsByUserId.TryGetValue(i.UserId, out var assignments) &&
                                assignments.Any(a =>
                                    string.Equals(a.AssignmentRole, parts[0], StringComparison.OrdinalIgnoreCase) &&
                                    string.Equals(a.ManagementLevel, parts[1], StringComparison.OrdinalIgnoreCase)))
                            .ToList();
                    }
                }

                if (!string.IsNullOrWhiteSpace(education))
                    usersInGovernorate = usersInGovernorate.Where(i => string.Equals(i.Education, education, StringComparison.OrdinalIgnoreCase)).ToList();

                var totalUsersCount = usersInGovernorate.Count;
                var totalPages = Math.Max(1, (int)Math.Ceiling(totalUsersCount / (double)pageSize));

                if (!administrativeOnly)
                    page = Math.Min(page, totalPages);

                var profilesForPage = administrativeOnly
                    ? usersInGovernorate
                    : usersInGovernorate
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .ToList();

                var pageUserIds = profilesForPage.Select(p => p.UserId).ToHashSet();

                var list = new List<AdminUserVM>();

                foreach (var profile in profilesForPage)
                {
                    usersById.TryGetValue(profile.UserId, out var user);
                    var roles = rolesByUserId.TryGetValue(profile.UserId, out var userRoles)
                        ? userRoles
                        : new List<string>();
                    addressesByUserId.TryGetValue(profile.UserId, out var address);
                    voterCardsByUserId.TryGetValue(profile.UserId, out var voterCard);
                    affiliationInfosByUserId.TryGetValue(profile.UserId, out var affiliationInfo);
                    var managementAssignments = assignmentsByUserId.TryGetValue(profile.UserId, out var userAssignments)
                        ? userAssignments
                        : new List<ManagementAssignment>();

                    string managementLevel = "";
                    string managementLevelArabic = "";
                    string managedEntityName = "";
                    string assignmentRole = "";
                    bool isManager = managementAssignments.Any();

                    if (isManager)
                    {
                        var primaryAssignment = managementAssignments.FirstOrDefault();
                        if (primaryAssignment != null)
                        {
                            managementLevel = primaryAssignment.ManagementLevel;
                            assignmentRole = primaryAssignment.AssignmentRole;
                            managementLevelArabic = GetArabicLevelName(
     primaryAssignment.ManagementLevel,
     primaryAssignment.AssignmentRole
 );
                            managedEntityName = GetManagedEntityDisplayName(primaryAssignment);
                        }
                    }

                    string roleDisplay = string.Join(", ", roles);
                    if (profile.AccountType == "فرد" && !roles.Contains("فرد"))
                    {
                        roleDisplay = string.IsNullOrEmpty(roleDisplay) ? "فرد" : roleDisplay + ", فرد";
                    }

                    string promotionStatus = "";
                    if (profile.RequestedPromotion == true && !profile.IsPromoted)
                        promotionStatus = "⏳ قيد المراجعة";
                    else if (profile.RejectionReason != null)
                        promotionStatus = "❌ مرفوض";
                    else if (profile.IsPromoted)
                        promotionStatus = "✅ مصعد";

                    list.Add(new AdminUserVM
                    {
                        Id = profile.Id,
                        UserId = profile.UserId,
                        Email = user?.Email ?? "",
                        FullName = profile.FullName ?? "غير مكتمل",
                        CoverImage = profile.CoverImage,
                        PhoneNumber = profile.PhoneNumber ?? "",
                        Roles = roleDisplay,
                        ResidenceGovernorate = address?.Governorate ?? "غير محدد",
                        WorkGovernorate = GetEffectiveGovernorateFast(profile) ?? "غير محدد",
                        WorkDistrict = GetEffectiveDistrictFast(profile) ?? "---",
                        Governorate = GetEffectiveGovernorateFast(profile) ?? "غير محدد",
                        District = GetEffectiveDistrictFast(profile) ?? "---",
                        AccountType = profile.AccountType ?? "عادي",
                        PromotionStatus = promotionStatus,
                        IsBasicInfoApproved = profile.IsBasicInfoApproved,
                        RequestedPromotion = profile.RequestedPromotion,
                        IsPromoted = profile.IsPromoted,
                        IsActive = user?.EmailConfirmed ?? false,
                        HasCompleteProfile = IsProfileComplete(profile, address, voterCard, affiliationInfo),
                        CompletionPercentage = CalculateCompletionPercentage(profile, address, voterCard),
                        CreatedAt = profile.CreatedAt,
                        Education = profile.Education ?? "---",
                        StudyStage = profile.StudyStage ?? "---",
                        BadgeNumber = affiliationInfo?.BadgeNumber ?? "",
                        RejectionReason = profile.RejectionReason ?? "---",
                         IsManager = isManager,
                        ManagementLevel = managementLevel,
                        ManagementLevelArabic = managementLevelArabic,
                        ManagedEntityName = managedEntityName,
                        AssignmentRole = assignmentRole,
                        SearchText = BuildUserSearchText(profile, user, managementAssignments)


                    });
                }

                list = list.OrderByDescending(u => u.RequestedPromotion)
                           .ThenByDescending(u => u.IsPromoted)
                           .ThenBy(u => u.FullName)
                           .ToList();

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

                ViewBag.ManagedGovernorate = adminGovernorate;
                ViewBag.AdministrativeOnly = administrativeOnly;
                ViewBag.UserCount = list.Count;
                ViewBag.ActiveUsersCount = list.Count(u => u.IsActive);
                ViewBag.CurrentPage = administrativeOnly ? 1 : page;
                ViewBag.PageSize = administrativeOnly ? Math.Max(list.Count, 1) : pageSize;
                ViewBag.TotalPages = administrativeOnly ? 1 : totalPages;
                ViewBag.TotalUsers = administrativeOnly ? unfilteredUsersCount : unfilteredUsersCount;
                ViewBag.FilteredUsers = administrativeOnly ? totalUsersCount : totalUsersCount;
                ViewBag.ActiveUsers = administrativeOnly ? unfilteredActiveUsersCount : unfilteredActiveUsersCount;
                ViewBag.PromotedUsers = administrativeOnly ? unfilteredPromotedUsersCount : unfilteredPromotedUsersCount;
                ViewBag.Search = search;
                ViewBag.RoleFilter = role;
                ViewBag.StatusFilter = status;
                ViewBag.ManagerLevelFilter = managerLevel;
                ViewBag.EducationFilter = education;
                ViewBag.ProfileStageFilter = profileStage;

                viewName = viewName == "AdministrativeManagers" ? viewName : "Users";
                return View(viewName, list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في عرض المستخدمين");
                ViewBag.ErrorMessage = $"حدث خطأ: {ex.Message}";
                viewName = viewName == "AdministrativeManagers" ? viewName : "Users";
                return View(viewName, new List<AdminUserVM>());
            }
        }

        private async Task<IActionResult> UsersPagedFromDatabaseAsync(
            bool administrativeOnly,
            string viewName,
            int page,
            string? search,
            string? role,
            string? status,
            string? managerLevel,
            string? education,
            string? profileStage,
            int pageSize)
        {
            if (administrativeOnly && !User.IsInRole(clsRoles.Admin))
                return Forbid();

            var adminGovernorate = await GetAdminGovernorateAsync();
            if (string.IsNullOrEmpty(adminGovernorate))
            {
                ViewBag.ErrorMessage = "❌ لم يتم تعيين محافظة لك.";
                viewName = viewName == "AdministrativeManagers" ? viewName : "Users";
                return View(viewName, new List<AdminUserVM>());
            }

            var currentUserId = _userManager.GetUserId(User);
            var isBaghdadCentralScope = string.Equals(adminGovernorate, "بغداد عامة", StringComparison.OrdinalIgnoreCase);
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
            var activeUserIdsQuery = _context.Users
                .AsNoTracking()
                .Where(u => u.EmailConfirmed)
                .Select(u => u.Id);

            var assignedManagerUserIdsQuery = _context.ManagementAssignments
                .AsNoTracking()
                .Select(a => a.UserId)
                .Distinct();

            var scopedQuery = _context.Identifies.AsNoTracking().Where(i =>
                !superAdminUserIdsQuery.Contains(i.UserId) &&
                !adminUserIdsQuery.Contains(i.UserId) &&
                (
                    (
                        districtAdminUserIdsQuery.Contains(i.UserId) &&
                        (i.ManagedGovernorate == adminGovernorate ||
                         (isBaghdadCentralScope && i.ManagedGovernorate != null &&
                          (i.ManagedGovernorate == "بغداد" || i.ManagedGovernorate.StartsWith("بغداد -"))))
                    ) ||
                    (
                        !districtAdminUserIdsQuery.Contains(i.UserId) &&
                        (i.WorkGovernorate == adminGovernorate ||
                         (isBaghdadCentralScope && i.WorkGovernorate != null &&
                          (i.WorkGovernorate == "بغداد" || i.WorkGovernorate.StartsWith("بغداد -"))) ||
                         _context.WorkLocations.Any(w =>
                             w.IdentifyId == i.Id &&
                             (w.Governorate == adminGovernorate ||
                              (isBaghdadCentralScope && w.Governorate != null &&
                               (w.Governorate == "بغداد" || w.Governorate.StartsWith("بغداد -"))))))
                    )
                ));

            if (administrativeOnly)
            {
                scopedQuery = scopedQuery.Where(i =>
                    assignedManagerUserIdsQuery.Contains(i.UserId) ||
                    managerRoleUserIdsQuery.Contains(i.UserId));
            }

            var unfilteredUsersCount = await scopedQuery.CountAsync();
            var unfilteredActiveUsersCount = await scopedQuery.CountAsync(i =>
                activeUserIdsQuery.Contains(i.UserId));
            var unfilteredPromotedUsersCount = await scopedQuery.CountAsync(i =>
                !string.IsNullOrWhiteSpace(i.Education) &&
                (i.AccountType == "فرد" || i.IsPromoted || memberRoleUserIdsQuery.Contains(i.UserId)));

            var query = scopedQuery;

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(i =>
                    (i.FullName != null && i.FullName.Contains(term)) ||
                    (i.PhoneNumber != null && i.PhoneNumber.Contains(term)) ||
                    (i.WhatsAppNumber != null && i.WhatsAppNumber.Contains(term)) ||
                    _context.Users.Any(u => u.Id == i.UserId &&
                                            ((u.Email != null && u.Email.Contains(term)) ||
                                             (u.PhoneNumber != null && u.PhoneNumber.Contains(term)))) ||
                    _context.AffiliationInfos.Any(a =>
                        a.UserId == i.UserId &&
                        ((a.BadgeNumber != null && a.BadgeNumber.Contains(term)) ||
                         _context.AffiliationEntities.Any(e => a.AffiliationEntityId == e.Id && e.Name.Contains(term)) ||
                         _context.Divisions.Any(d => a.DivisionId == d.Id && d.Name.Contains(term)) ||
                         _context.Sections.Any(s => a.SectionId == s.Id && s.Name.Contains(term)) ||
                         _context.Groups.Any(g => a.GroupId == g.Id && g.Name.Contains(term)))) ||
                    _context.ManagementAssignments.Any(a =>
                        a.UserId == i.UserId &&
                        (_context.AffiliationEntities.Any(e => a.AffiliationEntityId == e.Id && e.Name.Contains(term)) ||
                         _context.Divisions.Any(d => a.DivisionId == d.Id && d.Name.Contains(term)) ||
                         _context.Sections.Any(s => a.SectionId == s.Id && s.Name.Contains(term)) ||
                         _context.Groups.Any(g => a.GroupId == g.Id && g.Name.Contains(term)))));
            }

            if (!string.IsNullOrWhiteSpace(role))
            {
                var normalizedRole = role.Trim();
                query = normalizedRole.Equals("member", StringComparison.OrdinalIgnoreCase) || normalizedRole == "فرد"
                    ? query.Where(i => i.AccountType == "فرد" || memberRoleUserIdsQuery.Contains(i.UserId))
                    : query.Where(i => roleMembershipQuery
                        .Where(r => r.RoleName == normalizedRole)
                        .Select(r => r.UserId)
                        .Contains(i.UserId));
            }

            if (status == "active")
                query = query.Where(i => activeUserIdsQuery.Contains(i.UserId));
            else if (status == "inactive")
                query = query.Where(i => !activeUserIdsQuery.Contains(i.UserId));

            if (!string.IsNullOrWhiteSpace(managerLevel))
            {
                var parts = managerLevel.Split('-', 2);
                if (parts.Length == 2)
                {
                    var assignmentRole = parts[0];
                    var level = parts[1];
                    query = query.Where(i => _context.ManagementAssignments.Any(a =>
                        a.UserId == i.UserId &&
                        a.AssignmentRole == assignmentRole &&
                        a.ManagementLevel == level));
                }
            }

            if (!string.IsNullOrWhiteSpace(education))
                query = query.Where(i => i.Education == education);

            if (!string.IsNullOrWhiteSpace(profileStage))
            {
                var normalizedProfileStage = profileStage.Trim().ToLowerInvariant();
                var completeBasicInfoIds = query
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
                    .Select(i => i.Id);

                query = normalizedProfileStage switch
                {
                    "incomplete" => query.Where(i => !completeBasicInfoIds.Contains(i.Id)),
                    "basic-pending" => query.Where(i => completeBasicInfoIds.Contains(i.Id) && !i.IsBasicInfoApproved),
                    "needs-additional" => query.Where(i =>
                        completeBasicInfoIds.Contains(i.Id) &&
                        i.IsBasicInfoApproved &&
                        !i.RequestedPromotion &&
                        !i.IsPromoted &&
                        i.AccountType != "فرد" &&
                        !memberRoleUserIdsQuery.Contains(i.UserId)),
                    "promotion-pending" => query.Where(i =>
                        i.RequestedPromotion &&
                        !i.IsPromoted &&
                        i.AccountType != "فرد" &&
                        !memberRoleUserIdsQuery.Contains(i.UserId)),
                    "promoted" => query.Where(i =>
                        i.IsPromoted ||
                        i.AccountType == "فرد" ||
                        memberRoleUserIdsQuery.Contains(i.UserId)),
                    _ => query
                };
            }

            var totalUsersCount = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalUsersCount / (double)pageSize));
            page = Math.Min(page, totalPages);

            var profilesForPage = await query
                .OrderByDescending(i => i.UserId == currentUserId)
                .ThenByDescending(i => districtAdminUserIdsQuery.Contains(i.UserId))
                .ThenByDescending(i =>
                    assignedManagerUserIdsQuery.Contains(i.UserId) ||
                    managerRoleUserIdsQuery.Contains(i.UserId))
                .ThenBy(i => i.FullName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var pageUserIds = profilesForPage.Select(p => p.UserId).ToHashSet();
            var pageProfileIds = profilesForPage.Select(p => p.Id).ToHashSet();

            var usersById = await _context.Users.AsNoTracking().Where(u => pageUserIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id);
            var rolesByUserId = await _context.UserRoles
                .Where(ur => pageUserIds.Contains(ur.UserId))
                .Join(_context.Roles, userRole => userRole.RoleId, roleRow => roleRow.Id,
                    (userRole, roleRow) => new { userRole.UserId, RoleName = roleRow.Name ?? string.Empty })
                .GroupBy(x => x.UserId)
                .ToDictionaryAsync(g => g.Key, g => (IList<string>)g.Select(x => x.RoleName).ToList());
            var addressesByUserId = await _context.Addresses.AsNoTracking().Where(a => pageUserIds.Contains(a.UserId)).GroupBy(a => a.UserId).ToDictionaryAsync(g => g.Key, g => g.First());
            var voterCardsByUserId = await _context.VoterCards.AsNoTracking().Where(v => pageUserIds.Contains(v.UserId)).GroupBy(v => v.UserId).ToDictionaryAsync(g => g.Key, g => g.First());
            var workLocationsByProfileId = await _context.WorkLocations.AsNoTracking().Where(w => pageProfileIds.Contains(w.IdentifyId)).GroupBy(w => w.IdentifyId).ToDictionaryAsync(g => g.Key, g => g.First());
            var assignmentsByUserId = await _context.ManagementAssignments.AsNoTracking().Where(x => pageUserIds.Contains(x.UserId)).GroupBy(x => x.UserId).ToDictionaryAsync(g => g.Key, g => g.ToList());
            var affiliationInfosByUserId = await _context.AffiliationInfos.AsNoTracking().Where(x => pageUserIds.Contains(x.UserId)).GroupBy(x => x.UserId).ToDictionaryAsync(g => g.Key, g => g.First());
            var affiliationEntityIds = affiliationInfosByUserId.Values
                .Where(x => x.AffiliationEntityId.HasValue)
                .Select(x => x.AffiliationEntityId!.Value)
                .Concat(assignmentsByUserId.Values.SelectMany(x => x).Where(x => x.AffiliationEntityId.HasValue).Select(x => x.AffiliationEntityId!.Value))
                .Distinct()
                .ToList();
            var divisionIds = affiliationInfosByUserId.Values
                .Where(x => x.DivisionId.HasValue)
                .Select(x => x.DivisionId!.Value)
                .Concat(assignmentsByUserId.Values.SelectMany(x => x).Where(x => x.DivisionId.HasValue).Select(x => x.DivisionId!.Value))
                .Distinct()
                .ToList();
            var sectionIds = affiliationInfosByUserId.Values
                .Where(x => x.SectionId.HasValue)
                .Select(x => x.SectionId!.Value)
                .Concat(assignmentsByUserId.Values.SelectMany(x => x).Where(x => x.SectionId.HasValue).Select(x => x.SectionId!.Value))
                .Distinct()
                .ToList();
            var groupIds = affiliationInfosByUserId.Values
                .Where(x => x.GroupId.HasValue)
                .Select(x => x.GroupId!.Value)
                .Concat(assignmentsByUserId.Values.SelectMany(x => x).Where(x => x.GroupId.HasValue).Select(x => x.GroupId!.Value))
                .Distinct()
                .ToList();

            var affiliationEntityNames = await _context.AffiliationEntities.AsNoTracking()
                .Where(e => affiliationEntityIds.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, e => e.Name);
            var divisionNames = await _context.Divisions.AsNoTracking()
                .Where(d => divisionIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, d => d.Name);
            var sectionNames = await _context.Sections.AsNoTracking()
                .Where(s => sectionIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.Name);
            var groupNames = await _context.Groups.AsNoTracking()
                .Where(g => groupIds.Contains(g.Id))
                .ToDictionaryAsync(g => g.Id, g => g.Name);

            string GetEffectiveGovernorateFast(Identify profile)
            {
                var workLocation = workLocationsByProfileId.TryGetValue(profile.Id, out var wl) ? wl : profile.WorkLocation;
                return !string.IsNullOrWhiteSpace(workLocation?.Governorate) ? workLocation.Governorate : profile.WorkGovernorate ?? string.Empty;
            }

            string GetEffectiveDistrictFast(Identify profile)
            {
                var workLocation = workLocationsByProfileId.TryGetValue(profile.Id, out var wl) ? wl : profile.WorkLocation;
                if (!string.IsNullOrWhiteSpace(workLocation?.Governorate) && workLocation.Governorate == "بغداد") return workLocation.District ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(profile.WorkGovernorate) && profile.WorkGovernorate == "بغداد") return profile.WorkDistrict ?? string.Empty;
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

            var list = new List<AdminUserVM>();
            foreach (var profile in profilesForPage)
            {
                usersById.TryGetValue(profile.UserId, out var user);
                var roles = rolesByUserId.TryGetValue(profile.UserId, out var userRoles) ? userRoles : new List<string>();
                addressesByUserId.TryGetValue(profile.UserId, out var address);
                voterCardsByUserId.TryGetValue(profile.UserId, out var voterCard);
                affiliationInfosByUserId.TryGetValue(profile.UserId, out var affiliationInfo);
                var managementAssignments = assignmentsByUserId.TryGetValue(profile.UserId, out var userAssignments) ? userAssignments : new List<ManagementAssignment>();
                var primaryAssignment = managementAssignments.FirstOrDefault();

                var roleDisplay = string.Join(", ", roles);
                if (profile.AccountType == "فرد" && !roles.Contains("فرد"))
                    roleDisplay = string.IsNullOrEmpty(roleDisplay) ? "فرد" : roleDisplay + ", فرد";

                var promotionStatus = "";
                if (profile.RequestedPromotion && !profile.IsPromoted) promotionStatus = "⏳ قيد المراجعة";
                else if (profile.RejectionReason != null) promotionStatus = "❌ مرفوض";
                else if (profile.IsPromoted) promotionStatus = "✅ مصعد";

                var effectiveGovernorate = GetEffectiveGovernorateFast(profile);
                var effectiveDistrict = GetEffectiveDistrictFast(profile);

                list.Add(new AdminUserVM
                {
                    Id = profile.Id,
                    UserId = profile.UserId,
                    Email = user?.Email ?? "",
                    FullName = profile.FullName ?? "غير مكتمل",
                    CoverImage = profile.CoverImage,
                    PhoneNumber = profile.PhoneNumber ?? "",
                    Roles = roleDisplay,
                    ResidenceGovernorate = address?.Governorate ?? "غير محدد",
                    WorkGovernorate = effectiveGovernorate ?? "غير محدد",
                    WorkDistrict = effectiveDistrict ?? "---",
                    Governorate = effectiveGovernorate ?? "غير محدد",
                    District = effectiveDistrict ?? "---",
                    AccountType = profile.AccountType ?? "عادي",
                    PromotionStatus = promotionStatus,
                    RequestedPromotion = profile.RequestedPromotion,
                    IsPromoted = profile.IsPromoted,
                    IsActive = user?.EmailConfirmed ?? false,
                    CompletionPercentage = CalculateCompletionPercentage(profile, address, voterCard),
                    CreatedAt = profile.CreatedAt,
                    Education = profile.Education ?? "---",
                    StudyStage = profile.StudyStage ?? "---",
                    BadgeNumber = affiliationInfo?.BadgeNumber ?? "",
                    RejectionReason = profile.RejectionReason ?? "---",
                    IsManager = primaryAssignment != null,
                    ManagementLevel = primaryAssignment?.ManagementLevel ?? "",
                    ManagementLevelArabic = primaryAssignment != null ? GetArabicLevelName(primaryAssignment.ManagementLevel, primaryAssignment.AssignmentRole) : "",
                    ManagedEntityName = primaryAssignment != null ? GetManagedEntityDisplayName(primaryAssignment) : "",
                    AssignmentRole = primaryAssignment?.AssignmentRole ?? "",
                    SearchText = string.Empty
                });
            }

            ViewBag.ManagedGovernorate = adminGovernorate;
            ViewBag.AdministrativeOnly = administrativeOnly;
            ViewBag.UserCount = list.Count;
            ViewBag.ActiveUsersCount = list.Count(u => u.IsActive);
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalUsers = unfilteredUsersCount;
            ViewBag.FilteredUsers = totalUsersCount;
            ViewBag.ActiveUsers = unfilteredActiveUsersCount;
            ViewBag.PromotedUsers = unfilteredPromotedUsersCount;
            ViewBag.Search = search;
            ViewBag.RoleFilter = role;
            ViewBag.StatusFilter = status;
            ViewBag.ManagerLevelFilter = managerLevel;
            ViewBag.EducationFilter = education;
            ViewBag.ProfileStageFilter = profileStage;

            viewName = viewName == "AdministrativeManagers" ? viewName : "Users";
            return View(viewName, list);
        }

        private static int NormalizeUserManagementPageSize(int pageSize)
        {
            int[] allowedPageSizes = [10, 25, 50, 100];
            return allowedPageSizes.Contains(pageSize) ? pageSize : 10;
        }

        [Authorize(Roles = clsRoles.Admin)]
        public async Task<IActionResult> AdministrativeManagers(
            int page = 1,
            string? search = null,
            string? role = null,
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
                status: status,
                managerLevel: managerLevel,
                education: education,
                pageSize: pageSize);
        }

        // ===== عرض طلبات الترقية =====
        [HttpGet]
        public async Task<IActionResult> PromotionRequests(int page = 1)
        {
            try
            {
                const int pageSize = 10;
                var adminGovernorate = await GetAdminGovernorateAsync();
                if (string.IsNullOrEmpty(adminGovernorate))
                {
                    TempData["ErrorMessage"] = "❌ لم يتم تعيين محافظة لك.";
                    return RedirectToAction(nameof(Users));
                }

                var userIdsInGovernorate = await GetUserIdsInAdminGovernorateAsync();
                var requestsQuery = _context.Identifies
                    .AsNoTracking()
                    .Include(i => i.WorkLocation)
                    .Where(i => userIdsInGovernorate.Contains(i.UserId) &&
                                i.RequestedPromotion == true &&
                                i.IsPromoted == false &&
                                string.IsNullOrEmpty(i.RejectionReason));

                var totalRequests = await requestsQuery.CountAsync();
                var totalPages = Math.Max(1, (int)Math.Ceiling(totalRequests / (double)pageSize));
                page = Math.Max(1, Math.Min(page, totalPages));

                var promotionRequests = await requestsQuery
                    .OrderByDescending(i => i.RequestedPromotionDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var userIds = promotionRequests.Select(r => r.UserId).ToList();
                var usersById = await GetUsersByIdsAsync(userIds);
                var addressesByUserId = await GetAddressesByUserIdsAsync(userIds);
                var voterCardsByUserId = await GetVoterCardsByUserIdsAsync(userIds);
                var affiliationsByUserId = await GetAffiliationsByUserIdsAsync(userIds);

                var viewModel = new List<PromotionRequestViewModel>();
                foreach (var request in promotionRequests)
                {
                    usersById.TryGetValue(request.UserId, out var user);
                    addressesByUserId.TryGetValue(request.UserId, out var userAddress);
                    voterCardsByUserId.TryGetValue(request.UserId, out var voterCard);
                    affiliationsByUserId.TryGetValue(request.UserId, out var affiliationInfo);

                    viewModel.Add(new PromotionRequestViewModel
                    {
                        Id = request.Id,
                        UserId = request.UserId,
                        UserEmail = user?.Email ?? "",
                        FullName = request.FullName ?? "",
                        PhoneNumber = request.PhoneNumber ?? "",
                        Governorate = GetEffectiveGovernorate(request, userAddress),
                        IdentityCardN = request.IdentityCardN,
                        AffiliationEntity = affiliationInfo?.AffiliationEntity?.Name,
                        Division = affiliationInfo?.Division?.Name,
                        Section = affiliationInfo?.Section?.Name,
                        Group = affiliationInfo?.Group?.Name,
                        RequestDate = request.RequestedPromotionDate ?? request.CreatedAt,
                        AccountType = request.AccountType ?? "عادي",
                        CoverImage = request.CoverImage,
                        CompletionPercentage = CalculateCompletionPercentage(request, userAddress, voterCard),
                        HasCompleteProfile = IsProfileComplete(request, userAddress, voterCard, null),
                        RejectionReason = request.RejectionReason
                    });
                }

                ViewBag.ManagedGovernorate = adminGovernorate;
                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = totalPages;
                ViewBag.TotalRequests = totalRequests;
                ViewBag.PageSize = pageSize;
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في عرض طلبات الترقية");
                TempData["ErrorMessage"] = "حدث خطأ في تحميل البيانات";
                return View(new List<PromotionRequestViewModel>());
            }
        }

        // ===== الموافقة على طلب الترقية =====
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApprovePromotion(int id)
        {
            try
            {
                var identify = await _context.Identifies.FindAsync(id);
                if (identify == null)
                    return Json(new { success = false, message = "المستخدم غير موجود" });

                if (!await IsUserInAdminGovernorateAsync(identify.UserId))
                    return Json(new { success = false, message = "❌ لا يمكنك الموافقة على مستخدم خارج محافظتك" });

                identify.AccountType = "فرد";
                identify.IsPromoted = true;
                identify.PromotionDate = DateTime.Now;
                identify.PromotedBy = await GetCurrentActorDisplayNameAsync("Admin");
                identify.RequestedPromotion = false;
                identify.RejectionReason = null;

                _context.Identifies.Update(identify);
                await _context.SaveChangesAsync();

                var user = await _userManager.FindByIdAsync(identify.UserId);
                if (user != null)
                {
                    if (await _userManager.IsInRoleAsync(user, clsRoles.User))
                        await _userManager.RemoveFromRoleAsync(user, clsRoles.User);
                    if (!await _userManager.IsInRoleAsync(user, clsRoles.Member))
                        await _userManager.AddToRoleAsync(user, clsRoles.Member);
                }

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

        // ===== رفض طلب الترقية =====
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

                if (!await IsUserInAdminGovernorateAsync(identify.UserId))
                    return Json(new { success = false, message = "❌ لا يمكنك رفض مستخدم خارج محافظتك" });

                identify.RequestedPromotion = false;
                identify.RejectionReason = reason;
                identify.PromotionDate = null;
                identify.PromotedBy = await GetCurrentActorDisplayNameAsync("Admin");

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

        // ===== عرض طلبات المراجعة (البيانات الأساسية) =====
        [HttpGet]
        public async Task<IActionResult> PendingBasicInfo(int page = 1)
        {
            try
            {
                const int pageSize = 10;
                var adminGovernorate = await GetAdminGovernorateAsync();
                if (string.IsNullOrEmpty(adminGovernorate))
                {
                    TempData["ErrorMessage"] = "❌ لم يتم تعيين محافظة لك.";
                    return RedirectToAction(nameof(Users));
                }

                var userIdsInGovernorate = await GetUserIdsInAdminGovernorateAsync();
                var requestsQuery = _context.Identifies
                    .AsNoTracking()
                    .Include(i => i.WorkLocation)
                    .Where(i => userIdsInGovernorate.Contains(i.UserId) &&
                                i.IsBasicInfoApproved == false &&
                                string.IsNullOrEmpty(i.BasicInfoRejectionReason) &&
                                !string.IsNullOrEmpty(i.FullName) &&
                                !string.IsNullOrEmpty(i.IdentityCardN) && i.IdentityCardN.Length >= 12);

                var totalRequests = await requestsQuery.CountAsync();
                var totalPages = Math.Max(1, (int)Math.Ceiling(totalRequests / (double)pageSize));
                page = Math.Max(1, Math.Min(page, totalPages));

                var pagedRequests = await requestsQuery
                    .OrderByDescending(i => i.BasicInfoRequestedAt ?? i.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var userIds = pagedRequests.Select(r => r.UserId).ToList();
                var usersById = await GetUsersByIdsAsync(userIds);
                var addressesByUserId = await GetAddressesByUserIdsAsync(userIds);

                var viewModel = new List<PromotionRequestViewModel>();
                foreach (var p in pagedRequests)
                {
                    usersById.TryGetValue(p.UserId, out var user);
                    addressesByUserId.TryGetValue(p.UserId, out var userAddress);

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

                ViewBag.ManagedGovernorate = adminGovernorate;
                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = totalPages;
                ViewBag.TotalRequests = totalRequests;
                ViewBag.PageSize = pageSize;
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في عرض طلبات المراجعة");
                TempData["ErrorMessage"] = "حدث خطأ في تحميل البيانات";
                return View(new List<PromotionRequestViewModel>());
            }
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
            var adminGovernorate = await GetAdminGovernorateAsync();
            if (string.IsNullOrEmpty(adminGovernorate))
            {
                TempData["ErrorMessage"] = "❌ لم يتم تعيين محافظة لك.";
                return RedirectToAction(nameof(Users));
            }

            var normalizedType = string.Equals(type, "basic", StringComparison.OrdinalIgnoreCase)
                ? "basic"
                : "promotion";

            var userIdsInGovernorate = await GetUserIdsInAdminGovernorateAsync();
            var query = _context.Identifies
                .AsNoTracking()
                .Include(i => i.WorkLocation)
                .Where(i => userIdsInGovernorate.Contains(i.UserId));

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

            var userIds = identifies.Select(i => i.UserId).ToList();
            var usersById = await GetUsersByIdsAsync(userIds);
            var addressesByUserId = await GetAddressesByUserIdsAsync(userIds);
            var processedKeys = identifies
                .Select(i => normalizedType == "basic" ? i.BasicInfoApprovedBy : i.PromotedBy)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct()
                .ToList();
            var processedByDisplay = new Dictionary<string, string?>();
            foreach (var key in processedKeys)
            {
                processedByDisplay[key!] = await ResolveActorDisplayNameAsync(key);
            }

            var items = new List<RequestHistoryItemViewModel>();

            foreach (var identify in identifies)
            {
                usersById.TryGetValue(identify.UserId, out var user);
                addressesByUserId.TryGetValue(identify.UserId, out var address);
                var isRejected = normalizedType == "basic"
                    ? !string.IsNullOrWhiteSpace(identify.BasicInfoRejectionReason)
                    : !string.IsNullOrWhiteSpace(identify.RejectionReason);
                var processedByKey = normalizedType == "basic"
                    ? identify.BasicInfoApprovedBy
                    : identify.PromotedBy;

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
                    ProcessedBy = !string.IsNullOrWhiteSpace(processedByKey) &&
                                  processedByDisplay.TryGetValue(processedByKey, out var displayName)
                        ? displayName
                        : "",
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
                BackController = "Admin",
                DetailsAction = nameof(Details),
                DetailsController = "Admin",
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

            ViewBag.ManagedGovernorate = adminGovernorate;
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

        // ===== الموافقة على البيانات الأساسية =====
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveBasicInfo(int id)
        {
            try
            {
                var identify = await _context.Identifies.FindAsync(id);
                if (identify == null)
                    return Json(new { success = false, message = "المستخدم غير موجود" });

                if (!await IsUserInAdminGovernorateAsync(identify.UserId))
                    return Json(new { success = false, message = "❌ لا يمكنك الموافقة على مستخدم خارج محافظتك" });

                identify.IsBasicInfoApproved = true;
                identify.BasicInfoApprovedBy = await GetCurrentActorDisplayNameAsync("Admin");
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

        // ===== رفض البيانات الأساسية =====
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

                if (!await IsUserInAdminGovernorateAsync(identify.UserId))
                    return Json(new { success = false, message = "❌ لا يمكنك رفض مستخدم خارج محافظتك" });

                identify.BasicInfoRejectionReason = reason;
                identify.IsBasicInfoApproved = false;
                identify.BasicInfoApprovedBy = await GetCurrentActorDisplayNameAsync("Admin");
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

        // ===== عرض تفاصيل المستخدم =====
        public async Task<IActionResult> Details(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    TempData["ErrorMessage"] = "معرف المستخدم مطلوب";
                    return RedirectToAction(nameof(Users));
                }

                if (!await IsUserInAdminGovernorateAsync(id))
                {
                    TempData["ErrorMessage"] = "❌ لا يمكنك عرض مستخدم خارج محافظتك";
                    return RedirectToAction(nameof(Users));
                }

                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "المستخدم غير موجود";
                    return RedirectToAction(nameof(Users));
                }

                var userProfile = await GetUserProfileAsync(id);
                var roles = await _userManager.GetRolesAsync(user);
                var address = await GetUserAddressAsync(id);
                var voterCard = await GetUserVoterCardAsync(id);
                var union = await GetUserUnionAsync(id);
                var federation = await GetUserFederationAsync(id);
                var association = await GetUserAssociationAsync(id);
                var ngo = await GetUserNgoAsync(id);
                var affiliationInfo = await GetUserAffiliationInfoAsync(id);

                var rolesList = roles.ToList();
                if (userProfile?.AccountType == "فرد" && !rolesList.Contains("فرد"))
                    rolesList.Add("فرد");

                var affiliationEntityName = await GetAffiliationEntityNameAsync(affiliationInfo?.AffiliationEntityId);
                var divisionName = await GetDivisionNameAsync(affiliationInfo?.DivisionId);
                var sectionName = await GetSectionNameAsync(affiliationInfo?.SectionId);
                var groupName = await GetGroupNameAsync(affiliationInfo?.GroupId);

                // أسماء الاتحاد بشكل منفصل
                string federationName = "";
                string federationDivisionName = "";
                string federationSectionName = "";
                string federationGroupName = "";

                if (federation != null)
                {
                    federationName = federation.Federation?.Name ?? "";
                    federationDivisionName = federation.FederationDivision?.Name ?? "";
                    federationSectionName = federation.FederationSection?.Name ?? "";
                    federationGroupName = federation.FederationGroup?.Name ?? "";
                }
                // ========== جلب المسؤوليات الإدارية ==========
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

                var viewModel = new AdminUserDetailsVM
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
                    CoverImage = userProfile?.CoverImage,
                    CreatedAt = userProfile?.CreatedAt ?? DateTime.MinValue,
                    AffiliationDate = userProfile?.AffiliationDate,
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
                    FederationName = federationName,
                    FederationDivisionName = federationDivisionName,
                    FederationSectionName = federationSectionName,
                    FederationGroupName = federationGroupName,
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

        // ===== تعديل بيانات المستخدم =====
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

                if (!await IsUserInAdminGovernorateAsync(id))
                {
                    TempData["ErrorMessage"] = "❌ لا يمكنك تعديل مستخدم خارج محافظتك";
                    return RedirectToAction(nameof(Users));
                }

                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "المستخدم غير موجود";
                    return RedirectToAction(nameof(Users));
                }

                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains(clsRoles.Admin) || roles.Contains(clsRoles.SuperAdmin))
                {
                    TempData["ErrorMessage"] = "❌ لا يمكنك تعديل بيانات مشرف آخر";
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
                        StudyStage = profile?.StudyStage ?? ""
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
                    // ✅ أضف هذه الأسطر هنا (بعد Memberships وقبل IdentityCardN)
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
                await LoadDropdownLists(viewModel);
                ViewBag.TargetUserName = profile?.FullName ?? user.Email;
                ViewBag.IsEditing = true;
                await LoadDropdownLists(viewModel);
                viewModel.BaghdadDistricts = new List<string> { "الكرخ", "الرصافة" };  // ✅ أضف هذا السطر
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
                await LoadDropdownLists(model);
                if (model.Address?.Governorate == "بغداد" && string.IsNullOrWhiteSpace(model.Address.District))
                {
                    ModelState.AddModelError("Address.District", "القضاء مطلوب عند اختيار محافظة السكن بغداد");
                }

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("❌ النموذج غير صالح للتعديل");
                    return View(model);
                }

                if (!await IsUserInAdminGovernorateAsync(model.UserId))
                {
                    TempData["ErrorMessage"] = "❌ لا يمكنك تعديل مستخدم خارج محافظتك";
                    return RedirectToAction(nameof(Users));
                }

                var user = await _userManager.FindByIdAsync(model.UserId);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "المستخدم غير موجود";
                    return RedirectToAction(nameof(Users));
                }

                string? coverImagePath = null;
                if (model.CoverImageFile != null && model.CoverImageFile.Length > 0)
                    coverImagePath = await SaveCoverImage(model.CoverImageFile);

                var profile = await GetUserProfileAsync(model.UserId);
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
                profile.WorkGovernorate = model.WorkLocation.Governorate;
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

                await UpdateOrCreateAddress(model.UserId, model.Address);
                await UpdateOrCreateVoterCard(model.UserId, model.Documents);
                await UpdateOrCreateWorkLocation(profile.Id, model.WorkLocation);
                // ✅ استخدام المعرفات المحفوظة بدلاً من تحويل الأسماء
                await UpdateOrCreateAffiliationInfoWithIds(model.UserId, model.Affiliation,
                    model.AffiliationEntityId, model.DivisionId, model.SectionId, model.GroupId);
                await UpdateOrCreateUnion(model.UserId, model.Memberships);
                await UpdateOrCreateFederation(model.UserId, model.Memberships);
                await UpdateOrCreateAssociation(model.UserId, model.Memberships);
                await UpdateOrCreateNgo(model.UserId, model.Memberships);
                await _context.SaveChangesAsync();

                try
                {
                    await _notificationService.CreateNotification(
                        "📝 تم تعديل بياناتك الشخصية",
                        "للاطلاع قم بزيارة ملفك الشخصي",
                        model.UserId,
                        "bi-pencil-square",
                        "/Register/ProfileDetails"
                    );
                }
                catch (Exception ex) { _logger.LogError(ex, "خطأ في إرسال الإشعار"); }

                TempData["SuccessMessage"] = "✅ تم تحديث بيانات المستخدم بنجاح!";
                return RedirectToAction(nameof(Details), new { id = model.UserId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ في تعديل بيانات المستخدم");
                TempData["ErrorMessage"] = $"حدث خطأ: {ex.Message}";
                return View(model);
            }
        }

        // ===== حذف مستخدم =====
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser([FromBody] DeleteUserRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.UserId))
                    return Json(new { success = false, message = "معرف المستخدم مطلوب" });

                if (!await IsUserInAdminGovernorateAsync(request.UserId))
                    return Json(new { success = false, message = "لا يمكنك حذف مستخدم خارج محافظتك" });

                var user = await _userManager.FindByIdAsync(request.UserId);
                if (user == null)
                    return Json(new { success = false, message = "المستخدم غير موجود" });

                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains(clsRoles.Admin) || roles.Contains(clsRoles.SuperAdmin))
                    return Json(new { success = false, message = "لا يمكن حذف مشرف" });

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

        // ===== الحذف المتعدد =====
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
                        if (!await IsUserInAdminGovernorateAsync(userId))
                        {
                            failCount++;
                            continue;
                        }

                        var user = await _userManager.FindByIdAsync(userId);
                        if (user == null) { failCount++; continue; }

                        var roles = await _userManager.GetRolesAsync(user);
                        if (roles.Contains(clsRoles.Admin) || roles.Contains(clsRoles.SuperAdmin))
                        {
                            failCount++;
                            continue;
                        }

                        // حذف البيانات المرتبطة
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

        // ===== تفعيل/تعطيل المستخدم =====
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUserStatus([FromBody] ToggleStatusRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.UserId))
                    return Json(new { success = false, message = "معرف المستخدم مطلوب" });

                if (!await IsUserInAdminGovernorateAsync(request.UserId))
                    return Json(new { success = false, message = "لا يمكنك تعديل مستخدم خارج محافظتك" });

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

        // ===== إرسال إشعار =====
        [HttpGet]
        public async Task<IActionResult> SendNotification(string? userId = null)
        {
            var adminGovernorate = await GetAdminGovernorateAsync();
            if (string.IsNullOrEmpty(adminGovernorate))
            {
                TempData["ErrorMessage"] = "❌ لم يتم تعيين محافظة لك.";
                return RedirectToAction(nameof(Users));
            }

            var userIdsInGovernorate = await GetUserIdsInAdminGovernorateAsync();
            var usersInGovernorate = await _context.Identifies
                .Where(i => userIdsInGovernorate.Contains(i.UserId))
                .Select(i => new { i.UserId, i.FullName })
                .ToListAsync();

            ViewBag.Users = usersInGovernorate.Select(u => new { u.UserId, FullName = u.FullName }).ToList();

            if (!string.IsNullOrEmpty(userId))
            {
                var targetUser = await _userManager.FindByIdAsync(userId);
                if (targetUser != null)
                {
                    var targetProfile = await GetUserProfileAsync(userId);
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
                        // إرسال لجميع المستخدمين في المحافظة
                        var userIdsInGovernorate = await GetUserIdsInAdminGovernorateAsync();
                        var usersToNotify = await _context.Identifies
                            .Where(i => userIdsInGovernorate.Contains(i.UserId))
                            .Select(i => i.UserId)
                            .ToListAsync();

                        oneSignalResult = true;
                        oneSignalMessage = $"تم الإرسال إلى {usersToNotify.Count} مستخدم في المحافظة";
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

        // ===== تصدير جميع المستخدمين إلى Excel (كامل التفاصيل) =====
        [HttpGet]
        public async Task<IActionResult> ExportAllUsersToExcel()
        {
            try
            {
                var adminGovernorate = await GetAdminGovernorateAsync();
                if (string.IsNullOrEmpty(adminGovernorate))
                {
                    TempData["ErrorMessage"] = "❌ لم يتم تعيين محافظة لك";
                    return RedirectToAction(nameof(Users));
                }

                var userIdsInGovernorate = await GetUserIdsInAdminGovernorateAsync();
                var users = await _userManager.Users
                    .Where(u => userIdsInGovernorate.Contains(u.Id))
                    .OrderBy(u => u.Email)
                    .ToListAsync();

                var data = await BuildFullUsersExcelData(users);
                byte[] fileContent = GenerateExcelFile(data);
                return File(fileContent, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"All_Users_{adminGovernorate}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
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

                var adminGovernorate = await GetAdminGovernorateAsync();
                if (string.IsNullOrEmpty(adminGovernorate))
                    return BadRequest(new { success = false, message = "لم يتم تعيين محافظة لك" });

                var allowedUserIds = (await GetUserIdsInAdminGovernorateAsync()).ToHashSet();
                var validUserIds = request.UserIds
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct()
                    .Where(id => allowedUserIds.Contains(id))
                    .ToList();

                if (!validUserIds.Any())
                    return BadRequest(new { success = false, message = "لا يوجد مستخدمون ضمن صلاحياتك لتصديرهم" });

                var users = await _userManager.Users
                    .Where(u => validUserIds.Contains(u.Id))
                    .OrderBy(u => u.Email)
                    .ToListAsync();

                var data = await BuildFullUsersExcelData(users);
                byte[] fileContent = GenerateExcelFile(data);
                return File(fileContent, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Selected_Users_{adminGovernorate}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في تصدير Excel للمستخدمين المحددين");
                return StatusCode(500, new { success = false, message = $"❌ حدث خطأ: {ex.Message}" });
            }
        }

        // ===== تصدير الأفراد فقط إلى Excel =====
        [HttpGet]
        public async Task<IActionResult> ExportMembersOnlyToExcel()
        {
            try
            {
                var adminGovernorate = await GetAdminGovernorateAsync();
                if (string.IsNullOrEmpty(adminGovernorate))
                {
                    TempData["ErrorMessage"] = "❌ لم يتم تعيين محافظة لك";
                    return RedirectToAction(nameof(Users));
                }

                var userIdsInGovernorate = await GetUserIdsInAdminGovernorateAsync();
                var memberUserIds = await _context.Identifies.AsNoTracking()
                    .Where(i => userIdsInGovernorate.Contains(i.UserId) &&
                                !string.IsNullOrWhiteSpace(i.Education) &&
                                (i.AccountType == "فرد" || i.IsPromoted == true))
                    .Select(i => i.UserId)
                    .ToListAsync();

                var users = await _userManager.Users
                    .Where(u => memberUserIds.Contains(u.Id))
                    .OrderBy(u => u.Email)
                    .ToListAsync();

                var data = await BuildFullUsersExcelData(users);
                byte[] fileContent = GenerateExcelFile(data);
                return File(fileContent, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Members_Only_{adminGovernorate}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في تصدير Excel للأفراد");
                TempData["ErrorMessage"] = $"❌ حدث خطأ: {ex.Message}";
                return RedirectToAction(nameof(Users));
            }
        }

        // ===== تصدير طلبات الترقية إلى Excel =====
        [HttpGet]
        public async Task<IActionResult> ExportPromotionRequestsToExcel()
        {
            try
            {
                var adminGovernorate = await GetAdminGovernorateAsync();
                if (string.IsNullOrEmpty(adminGovernorate))
                {
                    TempData["ErrorMessage"] = "❌ لم يتم تعيين محافظة لك";
                    return RedirectToAction(nameof(PromotionRequests));
                }

                var userIdsInGovernorate = await GetUserIdsInAdminGovernorateAsync();
                var requests = await _context.Identifies.AsNoTracking()
                    .Where(i => userIdsInGovernorate.Contains(i.UserId) &&
                               i.RequestedPromotion && !i.IsPromoted)
                    .OrderByDescending(i => i.RequestedPromotionDate)
                    .ToListAsync();

                var requestUserIds = requests.Select(i => i.UserId).ToList();
                var usersById = await GetUsersByIdsAsync(requestUserIds);
                var addressesByUserId = await GetAddressesByUserIdsAsync(requestUserIds);
                var voterCardsByUserId = await GetVoterCardsByUserIdsAsync(requestUserIds);

                var data = new List<object[]>();
                data.Add(new object[] { "رقم", "البريد الإلكتروني", "الاسم الرباعي", "رقم الهاتف", "المحافظة", "رقم البطاقة", "تاريخ الطلب", "نسبة الإكمال", "تاريخ التسجيل" });

                int counter = 1;
                foreach (var request in requests)
                {
                    usersById.TryGetValue(request.UserId, out var user);
                    addressesByUserId.TryGetValue(request.UserId, out var address);
                    voterCardsByUserId.TryGetValue(request.UserId, out var voterCard);

                    data.Add(new object[] {
                        counter++,
                        user?.Email ?? "",
                        request.FullName ?? "",
                        request.PhoneNumber ?? "",
                        GetEffectiveGovernorate(request, address),
                        request.IdentityCardN ?? "",
                        request.RequestedPromotionDate?.ToString("yyyy-MM-dd HH:mm") ?? "",
                        CalculateCompletionPercentage(request, address, voterCard) + "%",
                        request.CreatedAt.ToString("yyyy-MM-dd") ?? ""
                    });
                }

                byte[] fileContent = GenerateExcelFile(data);
                return File(fileContent, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"PromotionRequests_{adminGovernorate}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في تصدير Excel للطلبات");
                TempData["ErrorMessage"] = $"❌ حدث خطأ: {ex.Message}";
                return RedirectToAction(nameof(PromotionRequests));
            }
        }
        // ==================== ✅ أضف الدوال الجديدة هنا ====================

        // ===== تصدير الطلاب الجامعيين فقط =====
        [HttpGet]
        public async Task<IActionResult> ExportStudentsOnlyToExcel()
        {
            try
            {
                var adminGovernorate = await GetAdminGovernorateAsync();
                if (string.IsNullOrEmpty(adminGovernorate))
                {
                    TempData["ErrorMessage"] = "❌ لم يتم تعيين محافظة لك";
                    return RedirectToAction(nameof(Users));
                }

                var userIdsInGovernorate = await GetUserIdsInAdminGovernorateAsync();
                var studentUserIds = await _context.Identifies.AsNoTracking()
                    .Where(i => userIdsInGovernorate.Contains(i.UserId) &&
                               (i.Education == "طالب جامعي" ||
                                (!string.IsNullOrEmpty(i.StudyStage) && i.StudyStage != "---") ||
                                i.StudyType == "انتظام" ||
                                i.StudyType == "مسائي"))
                    .Select(i => i.UserId)
                    .ToListAsync();

                var users = await _userManager.Users
                    .Where(u => studentUserIds.Contains(u.Id))
                    .OrderBy(u => u.Email)
                    .ToListAsync();

                var data = await BuildFullUsersExcelData(users);
                byte[] fileContent = GenerateExcelFile(data);
                return File(fileContent, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Students_Only_{adminGovernorate}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في تصدير Excel للطلاب");
                TempData["ErrorMessage"] = $"❌ حدث خطأ: {ex.Message}";
                return RedirectToAction(nameof(Users));
            }
        }

        // ===== تصدير المسؤولين الإداريين فقط =====
        [HttpGet]
        public async Task<IActionResult> ExportAdministrativeManagersOnlyToExcel()
        {
            try
            {
                var adminGovernorate = await GetAdminGovernorateAsync();
                if (string.IsNullOrEmpty(adminGovernorate))
                {
                    TempData["ErrorMessage"] = "❌ لم يتم تعيين محافظة لك";
                    return RedirectToAction(nameof(Users));
                }

                var userIdsInGovernorate = await GetUserIdsInAdminGovernorateAsync();

                // جلب المستخدمين الذين لديهم مسؤوليات إدارية وهم في نفس المحافظة
                var managersAssignments = await _context.ManagementAssignments
                    .Where(m => userIdsInGovernorate.Contains(m.UserId))
                    .Select(m => m.UserId)
                    .Distinct()
                    .ToListAsync();

                if (!managersAssignments.Any())
                {
                    TempData["WarningMessage"] = "⚠️ لا يوجد مسؤولين إداريين في محافظتك";
                    return RedirectToAction(nameof(Users));
                }

                var users = await _userManager.Users
                    .Where(u => managersAssignments.Contains(u.Id))
                    .OrderBy(u => u.Email)
                    .ToListAsync();

                var data = await BuildFullUsersExcelData(users);
                byte[] fileContent = GenerateExcelFile(data);
                return File(fileContent, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Administrative_Managers_{adminGovernorate}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في تصدير Excel للمسؤولين الإداريين");
                TempData["ErrorMessage"] = $"❌ حدث خطأ: {ex.Message}";
                return RedirectToAction(nameof(Users));
            }
        }

        // ===== دالة مساعدة لبناء بيانات Excel كاملة (مطابقة لـ SuperAdmin) =====
        private async Task<List<object[]>> BuildFullUsersExcelData(List<IdentityUser> users)
        {
            var data = new List<object[]>();

            // رأس الجدول - مطابق لجميع حقول ProfileDetails
            data.Add(new object[]
            {
        "الصورة الشخصية", "الاسم الرباعي", "اللقب", "اسم الأم", "تاريخ الميلاد",
        "الجنس", "الحالة الاجتماعية", "رقم الهاتف", "التحصيل الدراسي", "الاختصاص",
        "نوع الجامعة", "نوع المؤسسة", "اسم الجامعة/المعهد", "الكلية/القسم", "نوع الدراسة",
        "المرحلة الدراسية", "محافظة العمل التنظيمي", "محافظة السكن", "قضاء السكن",
        "المنطقة", "المحلة", "الزقاق",
        "الدار", "أقرب نقطة دالة", "رقم البطاقة الموحدة", "تاريخ الإصدار",
        "رقم بطاقة الناخب", "رقم مركز الاقتراع", "الحالة الوظيفية", "جهة العمل",
        "الوزارة", "الدائرة", "المنصب", "العنوان الوظيفي", "الدرجة الوظيفية",
        "جهة الانتساب", "القسم", "الشعبة", "الوحدة", "رقم الباج الخاص بك", "اسم المزكي", "رقم هاتف المزكي",
        "تاريخ الانتماء", "اسم النقابة", "المنصب في النقابة",
        "رقم العضوية في النقابة", "تاريخ النفاذ/الانتهاء للنقابة", "اسم الاتحاد",
        "قسم الاتحاد", "شعبة الاتحاد", "وحدة الاتحاد", "المنصب في الاتحاد",
        "رقم العضوية في الاتحاد", "تاريخ النفاذ/الانتهاء للاتحاد", "اسم الجمعية",
        "المنصب في الجمعية", "رقم العضوية في الجمعية", "تاريخ النفاذ/الانتهاء للجمعية",
        "اسم المنظمة", "المنصب في المنظمة", "رقم العضوية في المنظمة",
        "تاريخ النفاذ/الانتهاء للمنظمة", "البريد الإلكتروني", "الأدوار", "نشط؟",
        "نوع الحساب", "مصعد؟", "تاريخ التصعيد", "مصعد بواسطة", "طلب ترقية؟",
        "تاريخ الطلب", "سبب الرفض", "المسؤوليات الإدارية", "المحافظة المُدارة", "القضاء المُدار"
            });

            var userIds = users.Select(u => u.Id).ToList();
            var profiles = await _context.Identifies.AsNoTracking()
                .Where(i => userIds.Contains(i.UserId))
                .ToListAsync();
            var profilesByUserId = profiles.ToDictionary(i => i.UserId, i => i);

            var addressesByUserId = await _context.Addresses.AsNoTracking()
                .Where(a => userIds.Contains(a.UserId))
                .GroupBy(a => a.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.First());

            var voterCardsByUserId = await _context.VoterCards.AsNoTracking()
                .Where(v => userIds.Contains(v.UserId))
                .GroupBy(v => v.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.First());

            var unionsByUserId = await _context.UnionMemberships.AsNoTracking()
                .Where(u => userIds.Contains(u.UserId))
                .GroupBy(u => u.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.First());

            var federationsByUserId = await _context.FederationMemberships.AsNoTracking()
                .Include(f => f.Federation)
                .Include(f => f.FederationDivision)
                .Include(f => f.FederationSection)
                .Include(f => f.FederationGroup)
                .Where(f => userIds.Contains(f.UserId))
                .GroupBy(f => f.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.First());

            var associationsByUserId = await _context.AssociationMemberships.AsNoTracking()
                .Where(a => userIds.Contains(a.UserId))
                .GroupBy(a => a.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.First());

            var ngosByUserId = await _context.NgoMemberships.AsNoTracking()
                .Where(n => userIds.Contains(n.UserId))
                .GroupBy(n => n.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.First());

            var affiliations = await _context.AffiliationInfos.AsNoTracking()
                .Include(a => a.AffiliationEntity)
                .Include(a => a.Division)
                .Include(a => a.Section)
                .Include(a => a.Group)
                .Where(a => userIds.Contains(a.UserId))
                .ToListAsync();
            var affiliationsByUserId = affiliations
                .GroupBy(a => a.UserId)
                .ToDictionary(g => g.Key, g => g.First());

            var profileIds = profiles.Select(p => p.Id).ToList();
            var workLocationsByIdentifyId = await _context.WorkLocations.AsNoTracking()
                .Where(w => profileIds.Contains(w.IdentifyId))
                .ToDictionaryAsync(w => w.IdentifyId);

            var rolesByUserId = await _context.UserRoles
                .Where(ur => userIds.Contains(ur.UserId))
                .Join(_context.Roles,
                    userRole => userRole.RoleId,
                    role => role.Id,
                    (userRole, role) => new { userRole.UserId, RoleName = role.Name ?? string.Empty })
                .GroupBy(x => x.UserId)
                .ToDictionaryAsync(g => g.Key, g => (IList<string>)g.Select(x => x.RoleName).ToList());

            var managementAssignments = await _context.ManagementAssignments.AsNoTracking()
                .Where(x => userIds.Contains(x.UserId))
                .ToListAsync();
            var managementAssignmentsByUserId = managementAssignments
                .GroupBy(x => x.UserId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var assignmentEntityIds = managementAssignments
                .Where(x => x.ManagementLevel == "Entity" && x.AffiliationEntityId.HasValue)
                .Select(x => x.AffiliationEntityId!.Value)
                .Distinct()
                .ToList();
            var assignmentDivisionIds = managementAssignments
                .Where(x => x.ManagementLevel == "Division" && x.DivisionId.HasValue)
                .Select(x => x.DivisionId!.Value)
                .Distinct()
                .ToList();
            var assignmentSectionIds = managementAssignments
                .Where(x => x.ManagementLevel == "Section" && x.SectionId.HasValue)
                .Select(x => x.SectionId!.Value)
                .Distinct()
                .ToList();
            var assignmentGroupIds = managementAssignments
                .Where(x => x.ManagementLevel == "Group" && x.GroupId.HasValue)
                .Select(x => x.GroupId!.Value)
                .Distinct()
                .ToList();

            var assignmentEntitiesById = await _context.AffiliationEntities.AsNoTracking()
                .Where(e => assignmentEntityIds.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, e => e.Name ?? string.Empty);
            var assignmentDivisionsById = await _context.Divisions.AsNoTracking()
                .Where(d => assignmentDivisionIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, d => d.Name ?? string.Empty);
            var assignmentSectionsById = await _context.Sections.AsNoTracking()
                .Where(s => assignmentSectionIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.Name ?? string.Empty);
            var assignmentGroupsById = await _context.Groups.AsNoTracking()
                .Where(g => assignmentGroupIds.Contains(g.Id))
                .ToDictionaryAsync(g => g.Id, g => g.Name ?? string.Empty);

            var actorDisplayCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var user in users)
            {
                var roles = rolesByUserId.TryGetValue(user.Id, out var userRoles)
                    ? userRoles
                    : Array.Empty<string>();
                profilesByUserId.TryGetValue(user.Id, out var userProfile);
                addressesByUserId.TryGetValue(user.Id, out var address);
                var workLocation = userProfile != null && workLocationsByIdentifyId.TryGetValue(userProfile.Id, out var storedWorkLocation)
                    ? storedWorkLocation
                    : null;
                voterCardsByUserId.TryGetValue(user.Id, out var voterCard);
                unionsByUserId.TryGetValue(user.Id, out var union);
                federationsByUserId.TryGetValue(user.Id, out var federation);
                associationsByUserId.TryGetValue(user.Id, out var association);
                ngosByUserId.TryGetValue(user.Id, out var ngo);
                affiliationsByUserId.TryGetValue(user.Id, out var affiliationInfo);

                var residenceGovernorate = address?.Governorate ?? "";
                var residenceDistrict = address?.District ?? "";
                var workGovernorate = workLocation?.Governorate ?? userProfile?.WorkGovernorate ?? "";
                var workDistrict = workGovernorate == "بغداد"
                    ? workLocation?.District ?? userProfile?.WorkDistrict ?? ""
                    : "";

                var federationName = federation?.Federation?.Name ?? "";
                var federationDivisionName = federation?.FederationDivision?.Name ?? "";
                var federationSectionName = federation?.FederationSection?.Name ?? "";
                var federationGroupName = federation?.FederationGroup?.Name ?? "";

                var managementDisplayParts = new List<string>();
                var managedGovernorate = userProfile?.ManagedGovernorate ?? "";
                var managedDistrict = userProfile?.ManagedDistrict ?? "";
                var userAssignments = managementAssignmentsByUserId.TryGetValue(user.Id, out var assignmentList)
                    ? assignmentList
                    : new List<ManagementAssignment>();

                foreach (var assignment in userAssignments)
                {
                    var levelArabic = GetArabicLevelName(assignment.ManagementLevel, assignment.AssignmentRole);
                    var entityName = assignment.ManagementLevel switch
                    {
                        "Entity" when assignment.AffiliationEntityId.HasValue &&
                            assignmentEntitiesById.TryGetValue(assignment.AffiliationEntityId.Value, out var entityLabel) => entityLabel,
                        "Division" when assignment.DivisionId.HasValue &&
                            assignmentDivisionsById.TryGetValue(assignment.DivisionId.Value, out var divisionLabel) => divisionLabel,
                        "Section" when assignment.SectionId.HasValue &&
                            assignmentSectionsById.TryGetValue(assignment.SectionId.Value, out var sectionLabel) => sectionLabel,
                        "Group" when assignment.GroupId.HasValue &&
                            assignmentGroupsById.TryGetValue(assignment.GroupId.Value, out var groupLabel) => groupLabel,
                        _ => string.Empty
                    };

                    managementDisplayParts.Add($"{levelArabic}: {entityName}");

                    if (!string.IsNullOrEmpty(assignment.Governorate) && string.IsNullOrEmpty(managedGovernorate))
                        managedGovernorate = assignment.Governorate;

                    if (assignment.Governorate == "بغداد" &&
                        string.IsNullOrWhiteSpace(managedDistrict) &&
                        !string.IsNullOrWhiteSpace(assignment.BaghdadScope))
                    {
                        managedDistrict = assignment.BaghdadScope;
                    }
                }

                if (managedGovernorate == "بغداد" && string.IsNullOrWhiteSpace(managedDistrict))
                {
                    managedDistrict = userProfile?.ManagedDistrict
                        ?? userProfile?.WorkDistrict
                        ?? workLocation?.District
                        ?? "";
                }

                var promotedByDisplay = string.Empty;
                if (!string.IsNullOrWhiteSpace(userProfile?.PromotedBy))
                {
                    if (!actorDisplayCache.TryGetValue(userProfile.PromotedBy, out promotedByDisplay))
                    {
                        promotedByDisplay = await ResolveActorDisplayNameAsync(userProfile.PromotedBy);
                        actorDisplayCache[userProfile.PromotedBy] = promotedByDisplay;
                    }
                }

                data.Add(new object[]
                {
            userProfile?.CoverImage ?? "",
            userProfile?.FullName ?? "غير مكتمل",
            userProfile?.LastName ?? "",
            userProfile?.MotherName ?? "",
            userProfile?.Date.ToString("yyyy-MM-dd") ?? "",
            userProfile?.Gender ?? "",
            userProfile?.MaritalStatus ?? "",
            userProfile?.PhoneNumber ?? "",
            userProfile?.Education ?? "",
            userProfile?.Specialization ?? "",
            userProfile?.UniversityType ?? "",
            userProfile?.InstitutionType ?? "",
            userProfile?.InstitutionName ?? "",
            userProfile?.FacultyDepartment ?? "",
            userProfile?.StudyType ?? "",
            userProfile?.StudyStage ?? "",
            workGovernorate,
            residenceGovernorate,
            residenceDistrict,
            address?.Area ?? "",
            address?.Alley ?? "",
            address?.Street ?? "",
            address?.House ?? "",
            address?.NearestPoint ?? "",
            userProfile?.IdentityCardN ?? "",
            userProfile?.identityDate.ToString("yyyy-MM-dd") ?? "",
            voterCard?.VoterCardNumber ?? "",
            voterCard?.PollingCenterNumber ?? "",
            userProfile?.EmploymentStatus ?? "",
            userProfile?.Work ?? "",
            userProfile?.Ministry ?? "",
            userProfile?.Department ?? "",
            userProfile?.Position ?? "",
            userProfile?.JobTitle ?? "",
            CleanExcelPlaceholder(userProfile?.JobGrade, "-- اختر الدرجة الوظيفية --"),
            affiliationInfo?.AffiliationEntity?.Name ?? "",
            affiliationInfo?.Division?.Name ?? "",
            affiliationInfo?.Section?.Name ?? "",
            affiliationInfo?.Group?.Name ?? "",
            affiliationInfo?.BadgeNumber ?? "",
            affiliationInfo?.MozakeName ?? "",
            affiliationInfo?.MozakePhoneNumber ?? "",
            affiliationInfo?.AffiliationDate?.ToString("yyyy-MM-dd") ?? "",
            union?.UnionName ?? "",
            union?.Position ?? "",
            union?.IdNumber ?? "",
            union?.AffiliationDate?.ToString("yyyy-MM-dd") ?? "",
            federationName,
            federationDivisionName,
            federationSectionName,
            federationGroupName,
            federation?.Position ?? "",
            federation?.IdNumber ?? "",
            federation?.AffiliationDate?.ToString("yyyy-MM-dd") ?? "",
            association?.AssociationName ?? "",
            association?.Position ?? "",
            association?.IdNumber ?? "",
            association?.AffiliationDate?.ToString("yyyy-MM-dd") ?? "",
            ngo?.NgoName ?? "",
            ngo?.Position ?? "",
            ngo?.IdNumber ?? "",
            ngo?.AffiliationDate?.ToString("yyyy-MM-dd") ?? "",
            user.Email ?? "",
            TranslateRolesForExport(roles),
            user.EmailConfirmed ? "نشط" : "غير نشط",
            TranslateAccountTypeForExport(userProfile?.AccountType),
            userProfile?.IsPromoted == true ? "مصعد" : "غير مصعد",
            userProfile?.PromotionDate?.ToString("yyyy-MM-dd") ?? "",
            promotedByDisplay,
            userProfile?.RequestedPromotion == true ? "نعم" : "لا",
            userProfile?.RequestedPromotionDate?.ToString("yyyy-MM-dd") ?? "",
            userProfile?.RejectionReason ?? "",
            TranslateManagementDisplayForExport(string.Join(", ", managementDisplayParts)),
            managedGovernorate,
            managedDistrict
                });
            }

            return data;
        }

        // ===== دوال AJAX للـ Cascading Dropdown =====
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

        // ===== دوال مساعدة للتعديل =====
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
        private async Task LoadDropdownLists(CompleteProfileViewModel model)
        {
            model.Governorates = GetGovernorates();
            model.Genders = GetGenders();
            model.Educations = GetEducations();
            model.Ministries = GetMinistries();
            model.EmploymentStatuses = GetEmploymentStatuses();
            model.StudyStagesList = GetStudyStages();
            model.JobGradesList = GetJobGrades();  // ✅ أضف هذا السطر
            model.AffiliationEntities = await GetDistinctAffiliationEntitiesAsync();
            model.DivisionsList = await GetDistinctDivisionsAsync();
            model.SectionsList = await GetDistinctSectionsAsync();
            model.GroupsList = await GetDistinctGroupsAsync();
            model.UnionsList = await GetDistinctUnionsAsync();
            model.FederationsList = await GetDistinctFederationsAsync();
            model.AssociationsList = await GetDistinctAssociationsAsync();
            model.NgosList = await GetDistinctNgosAsync();
        }

        private async Task<string?> SaveCoverImage(IFormFile coverImageFile)
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

        private async Task UpdateOrCreateAddress(string userId, AddressViewModel model)
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

        private async Task UpdateOrCreateVoterCard(string userId, DocumentsViewModel model)
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

        private async Task UpdateOrCreateWorkLocation(int identifyId, WorkLocationViewModel model)
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

        private async Task UpdateOrCreateAffiliationInfo(string userId, AffiliationViewModel model)
        {
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
                var divisions = await _context.Divisions.Where(d => d.AffiliationEntityId == affiliationEntityId.Value).ToListAsync();
                var foundDivision = divisions.FirstOrDefault(d => d.Name == model.Division);
                divisionId = foundDivision?.Id;
            }

            if (!string.IsNullOrEmpty(model.Section) && divisionId.HasValue)
            {
                var sections = await _context.Sections.Where(s => s.DivisionId == divisionId.Value).ToListAsync();
                var foundSection = sections.FirstOrDefault(s => s.Name == model.Section);
                sectionId = foundSection?.Id;
            }

            if (!string.IsNullOrEmpty(model.Group) && sectionId.HasValue)
            {
                var groups = await _context.Groups.Where(g => g.SectionId == sectionId.Value).ToListAsync();
                var foundGroup = groups.FirstOrDefault(g => g.Name == model.Group);
                groupId = foundGroup?.Id;
            }

            if (existing != null)
            {
                existing.AffiliationEntityId = affiliationEntityId;
                existing.DivisionId = divisionId;
                existing.SectionId = sectionId;
                existing.GroupId = groupId;
                existing.MozakeName = model.MozakeName;
                existing.MozakePhoneNumber = model.MozakePhoneNumber;
                existing.BadgeNumber = model.BadgeNumber;
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

        private async Task UpdateOrCreateUnion(string userId, MembershipViewModel model)
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

        private async Task UpdateOrCreateFederation(string userId, MembershipViewModel model)
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
                var federationMaster = await _context.Federations.FirstOrDefaultAsync(f => f.Name == federationName);
                federationId = federationMaster?.Id;
            }

            if (federationId.HasValue && !string.IsNullOrEmpty(divisionName))
            {
                var divisions = await _context.FederationDivisions.Where(d => d.FederationId == federationId.Value).ToListAsync();
                var division = divisions.FirstOrDefault(d => d.Name == divisionName);
                divisionId = division?.Id;
            }

            if (divisionId.HasValue && !string.IsNullOrEmpty(sectionName))
            {
                var sections = await _context.FederationSections.Where(s => s.FederationDivisionId == divisionId.Value).ToListAsync();
                var section = sections.FirstOrDefault(s => s.Name == sectionName);
                sectionId = section?.Id;
            }

            if (sectionId.HasValue && !string.IsNullOrEmpty(groupName))
            {
                var groups = await _context.FederationGroups.Where(g => g.FederationSectionId == sectionId.Value).ToListAsync();
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

        private async Task UpdateOrCreateAssociation(string userId, MembershipViewModel model)
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

        private async Task UpdateOrCreateNgo(string userId, MembershipViewModel model)
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

        // ===== قوائم البيانات =====
        private List<string> GetGovernorates()
        {
            return new List<string> { "بغداد", "الأنبار", "بابل", "البصرة", "ذي قار", "القادسية", "ديالى", "دهوك", "أربيل", "كربلاء", "كركوك", "ميسان", "المثنى", "النجف", "نينوى", "صلاح الدين", "السليمانية", "واسط" };
        }

        private List<string> GetGenders() => new List<string> { "ذكر", "أنثى" };

        private List<string> GetEducations() => new List<string> { "آمي", "ابتدائي", "متوسط", "إعدادي", "معهد", "طالب جامعي", "دبلوم", "بكالوريوس", "ماجستير", "دكتوراه" };

        private List<string> GetMinistries() => IraqiGovernmentEntities.GetMinistries();

        private List<string> GetEmploymentStatuses() => new List<string> { "موظف", "كاسب", "متقاعد", "طالب", "قطاع خاص" };

        private List<string> GetStudyStages() => new List<string> { "المرحلة الأولى", "المرحلة الثانية", "المرحلة الثالثة", "المرحلة الرابعة", "المرحلة الخامسة", "المرحلة السادسة" };

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

        // ===== دوال مساعدة عامة =====
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
        private async Task UpdateOrCreateAffiliationInfoWithIds(string userId, AffiliationViewModel model,
    int? entityId, int? divisionId, int? sectionId, int? groupId)
        {
            var existing = await GetUserAffiliationInfoAsync(userId);

            if (existing != null)
            {
                // استخدام المعرفات المحفوظة
                if (entityId.HasValue)
                    existing.AffiliationEntityId = entityId;
                if (divisionId.HasValue)
                    existing.DivisionId = divisionId;
                if (sectionId.HasValue)
                    existing.SectionId = sectionId;
                if (groupId.HasValue)
                    existing.GroupId = groupId;

                // تحديث الحقول النصية
                existing.MozakeName = model.MozakeName ?? "";
                existing.MozakePhoneNumber = model.MozakePhoneNumber ?? "";
                existing.BadgeNumber = model.BadgeNumber ?? "";
                existing.AffiliationDate = model.AffiliationDate;

                _context.AffiliationInfos.Update(existing);
            }
            else if (entityId.HasValue || !string.IsNullOrEmpty(model.MozakeName))
            {
                var affiliationInfo = new AffiliationInfo
                {
                    UserId = userId,
                    AffiliationEntityId = entityId,
                    DivisionId = divisionId,
                    SectionId = sectionId,
                    GroupId = groupId,
                    MozakeName = model.MozakeName ?? "",
                    MozakePhoneNumber = model.MozakePhoneNumber ?? "",
                    BadgeNumber = model.BadgeNumber ?? "",
                    AffiliationDate = model.AffiliationDate
                };
                _context.AffiliationInfos.Add(affiliationInfo);
            }

            await _context.SaveChangesAsync();
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

        private string GetArabicLevelName(string level, string role)
        {
            string levelName = level switch
            {
                "Entity" => "جهة",
                "Division" => "قسم",
                "Section" => "شعبة",
                "Group" => "وحدة",
                _ => level
            };

            string roleName = role switch
            {
                "Manager" => "مسؤول",
                "Assistant" => "مساعد",
                _ => role
            };

            return $"{roleName} {levelName}";
        }

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
    }


    // ===== ViewModels =====
    public class AdminUserVM
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? CoverImage { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public string Roles { get; set; } = string.Empty;
        public string ResidenceGovernorate { get; set; } = string.Empty;
        public string WorkGovernorate { get; set; } = string.Empty;
        public string WorkDistrict { get; set; } = string.Empty;
        public string Governorate { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;  // ✅ أضف هذا السطر

        public string AccountType { get; set; } = string.Empty;
        public string PromotionStatus { get; set; } = string.Empty;
        public bool IsBasicInfoApproved { get; set; }
        public bool RequestedPromotion { get; set; }
        public bool IsPromoted { get; set; }
        public bool IsActive { get; set; }
        public bool HasCompleteProfile { get; set; }
        public int CompletionPercentage { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Education { get; set; } = string.Empty;
        public string StudyStage { get; set; } = string.Empty;
        public string BadgeNumber { get; set; } = string.Empty;
        public string? RejectionReason { get; set; }  // ✅ أضف هذا السطر
                                                      // ✅ أضف هذه الخصائص الجديدة
        public bool IsManager { get; set; }
        public string ManagementLevel { get; set; } = string.Empty;
        public string ManagementLevelArabic { get; set; } = string.Empty;
        public string ManagedEntityName { get; set; } = string.Empty;
        public string AssignmentRole { get; set; } = string.Empty;
        public string SearchText { get; set; } = string.Empty;
    }

    public class AdminUserDetailsVM
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string WhatsAppNumber { get; set; } = string.Empty;
        public bool IsWhatsAppVerified { get; set; }
        public DateTime? WhatsAppVerifiedAt { get; set; }
        public bool IsActive { get; set; }
        public string Roles { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string MotherName { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string MaritalStatus { get; set; } = string.Empty;
        public string Education { get; set; } = string.Empty;
        public string Specialization { get; set; } = string.Empty;
        public string UniversityType { get; set; } = string.Empty;
        public string InstitutionType { get; set; } = string.Empty;
        public string InstitutionName { get; set; } = string.Empty;
        public string FacultyDepartment { get; set; } = string.Empty;
        public string StudyType { get; set; } = string.Empty;
        public string StudyStage { get; set; } = string.Empty;
        public string IdentityCardN { get; set; } = string.Empty;
        public DateTime IdentityDate { get; set; }
        public string JobTitle { get; set; } = string.Empty;
        public string JobGrade { get; set; } = string.Empty;
        public string? WorkGovernorate { get; set; }
        public string? WorkDistrict { get; set; }
        public Address? Address { get; set; }
        public string? CoverImage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? AffiliationDate { get; set; }
        public string? EmploymentStatus { get; set; }
        public string? Work { get; set; }
        public string? Ministry { get; set; }
        public string? Department { get; set; }
        public string? Position { get; set; }
        public bool RequestedPromotion { get; set; }
        public DateTime? RequestedPromotionDate { get; set; }
        public string? RejectionReason { get; set; }
        public string AccountType { get; set; } = string.Empty;
        public bool IsPromoted { get; set; }
        public DateTime? PromotionDate { get; set; }
        public string? PromotedBy { get; set; }
        public string? AffiliationEntity { get; set; }
        public string? Division { get; set; }
        public string? Section { get; set; }
        public string? Group { get; set; }
        public string? AffiliationMozakeName { get; set; }
        public string? MozakePhoneNumber { get; set; }
        public string? BadgeNumber { get; set; }
        public DateTime? AffiliationEntryDate { get; set; }
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
        public string? VoterCardNumber { get; set; }
        public string? PollingCenterNumber { get; set; }
        // ========== المسؤوليات الإدارية ==========
        public List<ManagementAssignmentDisplayVM> ManagementAssignments { get; set; } = new();
        public bool IsManager => ManagementAssignments.Any();
    }
   

    public class BulkDeleteRequest { public List<string> UserIds { get; set; } = new(); }
    public class DeleteUserRequest { public string UserId { get; set; } = string.Empty; }
    public class ToggleStatusRequest { public string UserId { get; set; } = string.Empty; }
    public class SendNotificationViewModel { public string Title { get; set; } = string.Empty; public string Message { get; set; } = string.Empty; public string? TargetUserId { get; set; } public string? Icon { get; set; } public string? ClickUrl { get; set; } }
}
