using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;

namespace WebApplication2.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin,DistrictAdmin,MapViewer,Manager,AssistantManager")]
    public class MapDashboardController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<MapDashboardController> _logger;
        private readonly ApplicationDbContext _context;

        private class MapAccessScope
        {
            public bool IsSuperAdmin { get; set; }
            public bool HasLocationScopeRole { get; set; }
            public bool HasManagerScopeRole { get; set; }
            public string? ManagedGovernorate { get; set; }
            public string? ManagedDistrict { get; set; }
            public List<ManagementAssignment> Assignments { get; set; } = new();
        }

        private const string AllGovernoratesScopeName = "مركزي لكل المحافظات";
        private const string BaghdadGeneralScopeName = "بغداد عامة";

        public MapDashboardController(
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<MapDashboardController> logger,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
            _context = context;
        }

        // ===== دالة مساعدة لجلب المحافظة المدارة للمستخدم الحالي =====
        private async Task<string?> GetCurrentUserManagedGovernorate()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return null;

            var roles = await _userManager.GetRolesAsync(currentUser);

            if (roles.Contains("SuperAdmin"))
                return null;

            if (roles.Contains("Admin") || roles.Contains("DistrictAdmin") || roles.Contains("MapViewer"))
            {
                var identify = await _context.Identifies
                    .FirstOrDefaultAsync(i => i.UserId == currentUser.Id);
                return identify?.ManagedGovernorate;
            }

            return null;
        }

        // ===== ✅ دالة مساعدة لجلب القضاء المُدار للمستخدم الحالي (لـ DistrictAdmin) =====
        private async Task<string?> GetCurrentUserManagedDistrict()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return null;

            var roles = await _userManager.GetRolesAsync(currentUser);

            // DistrictAdmin و MapViewer قد يملكان تحديداً على مستوى الكرخ/الرصافة
            if (!roles.Contains("DistrictAdmin") && !roles.Contains("MapViewer"))
                return null;

            var identify = await _context.Identifies
                .FirstOrDefaultAsync(i => i.UserId == currentUser.Id);
            return identify?.ManagedDistrict;
        }

        // ===== دالة للتحقق إذا كان المستخدم الحالي SuperAdmin =====
        private async Task<bool> IsCurrentUserSuperAdmin()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return false;

            var roles = await _userManager.GetRolesAsync(currentUser);
            return roles.Contains("SuperAdmin");
        }

        private async Task<MapAccessScope> GetCurrentMapAccessScopeAsync()
        {
            var scope = new MapAccessScope();
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return scope;
            }

            var roles = await _userManager.GetRolesAsync(currentUser);
            scope.IsSuperAdmin = roles.Contains("SuperAdmin");
            scope.HasLocationScopeRole = roles.Contains("Admin") || roles.Contains("DistrictAdmin") || roles.Contains("MapViewer");
            scope.HasManagerScopeRole = roles.Contains("Manager") || roles.Contains("AssistantManager");

            var identify = await _context.Identifies
                .FirstOrDefaultAsync(i => i.UserId == currentUser.Id);

            if (!scope.IsSuperAdmin && scope.HasLocationScopeRole)
            {
                scope.ManagedGovernorate = identify?.ManagedGovernorate;
            }

            if (!scope.IsSuperAdmin && (roles.Contains("DistrictAdmin") || roles.Contains("MapViewer")))
            {
                scope.ManagedDistrict = identify?.ManagedDistrict;
            }

            if (!scope.IsSuperAdmin && scope.HasManagerScopeRole)
            {
                scope.Assignments = await _context.ManagementAssignments
                    .Where(x => x.UserId == currentUser.Id)
                    .OrderByDescending(x => x.CreatedAt)
                    .ToListAsync();
            }

            return scope;
        }

        private string GetEffectiveGovernorate(Identify? identify, Address? address)
        {
            var workLocation = identify?.WorkLocation;

            return !string.IsNullOrWhiteSpace(workLocation?.Governorate)
                ? workLocation.Governorate
                : !string.IsNullOrWhiteSpace(identify?.WorkGovernorate)
                ? identify.WorkGovernorate
                : string.Empty;
        }

        private string GetEffectiveDistrict(Identify? identify, Address? address)
        {
            var workLocation = identify?.WorkLocation;

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

        private Dictionary<string, Address> BuildAddressLookup(IEnumerable<Address> addresses)
        {
            return addresses
                .Where(a => !string.IsNullOrWhiteSpace(a.UserId))
                .GroupBy(a => a.UserId)
                .ToDictionary(g => g.Key, g => g.First());
        }

        private static bool IsGovernorateInManagedScope(string? governorate, string? managedGovernorate)
        {
            if (string.IsNullOrWhiteSpace(governorate) || string.IsNullOrWhiteSpace(managedGovernorate))
                return false;

            var current = governorate.Trim();
            var managed = managedGovernorate.Trim();

            if (IsAllGovernoratesScope(managed))
                return true;

            if (string.Equals(current, managed, StringComparison.OrdinalIgnoreCase))
                return true;

            return IsBaghdadGeneralScope(managed) && IsBaghdadGovernorateScope(current);
        }

        private static bool IsGovernorateInStatisticsBucket(string? governorate, string? statisticsGovernorate)
        {
            if (string.IsNullOrWhiteSpace(governorate) || string.IsNullOrWhiteSpace(statisticsGovernorate))
                return false;

            var current = governorate.Trim();
            var target = statisticsGovernorate.Trim();

            if (IsAllGovernoratesScope(target))
                return IsAllGovernoratesScope(current);

            if (IsBaghdadGeneralScope(target))
                return IsBaghdadGeneralScope(current);

            return string.Equals(current, target, StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerable<string> ExpandManagedGovernorateScope(string? managedGovernorate)
        {
            if (string.IsNullOrWhiteSpace(managedGovernorate))
                return Enumerable.Empty<string>();

            var normalizedGovernorate = managedGovernorate.Trim();

            return IsAllGovernoratesScope(normalizedGovernorate)
                ? new[] { AllGovernoratesScopeName }
                : IsBaghdadGeneralScope(normalizedGovernorate)
                ? new[] { BaghdadGeneralScopeName, "بغداد - الكرخ", "بغداد - الرصافة", "بغداد", "بغداد مركزي" }
                : new[] { managedGovernorate.Trim() };
        }

        private static bool IsAllGovernoratesScope(string? governorate)
        {
            if (string.IsNullOrWhiteSpace(governorate))
                return false;

            var value = governorate.Trim();
            return value == AllGovernoratesScopeName ||
                   value == "كل المحافظات" ||
                   value == "مركزي";
        }

        private static bool IsBaghdadGeneralScope(string? governorate)
        {
            if (string.IsNullOrWhiteSpace(governorate))
                return false;

            var value = governorate.Trim();
            return value == BaghdadGeneralScopeName || value == "بغداد مركزي";
        }

        private static bool IsBaghdadGovernorateScope(string? governorate)
        {
            if (string.IsNullOrWhiteSpace(governorate))
                return false;

            var value = governorate.Trim();
            return value == "بغداد" ||
                   value == "بغداد مركزي" ||
                   value == BaghdadGeneralScopeName ||
                   value.StartsWith("بغداد -", StringComparison.OrdinalIgnoreCase);
        }

        private List<Identify> FilterUsersByLocation(
            IEnumerable<Identify> identifies,
            IReadOnlyDictionary<string, Address> addressLookup,
            IEnumerable<string>? allowedGovernorates = null,
            string? exactGovernorate = null,
            string? exactDistrict = null)
        {
            var expandedAllowedGovernorates = allowedGovernorates?.SelectMany(ExpandManagedGovernorateScope).ToList();
            var allowedGovernoratesSet = expandedAllowedGovernorates != null && !expandedAllowedGovernorates.Any(IsAllGovernoratesScope)
                ? new HashSet<string>(expandedAllowedGovernorates)
                : null;

            return identifies
                .Where(identify =>
                {
                    addressLookup.TryGetValue(identify.UserId, out var address);
                    var effectiveGovernorate = GetEffectiveGovernorate(identify, address);

                    if (allowedGovernoratesSet != null && !allowedGovernoratesSet.Contains(effectiveGovernorate))
                        return false;

                    if (!string.IsNullOrWhiteSpace(exactGovernorate) &&
                        !IsGovernorateInStatisticsBucket(effectiveGovernorate, exactGovernorate))
                        return false;

                    if (!string.IsNullOrWhiteSpace(exactDistrict))
                    {
                        var effectiveDistrict = GetEffectiveDistrict(identify, address);
                        var districtMatches = effectiveGovernorate == "بغداد"
                            ? NormalizeBaghdadScope(effectiveDistrict) == NormalizeBaghdadScope(exactDistrict)
                            : effectiveDistrict == exactDistrict;

                        if (!districtMatches)
                            return false;
                    }

                    return true;
                })
                .ToList();
        }

        private List<Identify> FilterUsersByScope(
            IEnumerable<Identify> identifies,
            IReadOnlyDictionary<string, Address> addressLookup,
            IReadOnlyDictionary<string, AffiliationInfo> affiliationLookup,
            MapAccessScope scope,
            IEnumerable<string>? allowedGovernorates = null,
            string? exactGovernorate = null)
        {
            var effectiveAllowedGovernorates = allowedGovernorates;

            if (!scope.IsSuperAdmin && effectiveAllowedGovernorates == null)
            {
                if (!string.IsNullOrWhiteSpace(scope.ManagedGovernorate))
                {
                    effectiveAllowedGovernorates = IsAllGovernoratesScope(scope.ManagedGovernorate)
                        ? null
                        : ExpandManagedGovernorateScope(scope.ManagedGovernorate);
                }
                else if (scope.Assignments.Any())
                {
                    effectiveAllowedGovernorates = scope.Assignments.Any(IsCentralAssignment)
                        ? null
                        : scope.Assignments
                            .Select(a => a.Governorate)
                            .Where(g => !string.IsNullOrWhiteSpace(g))
                            .SelectMany(ExpandManagedGovernorateScope)
                            .Distinct()
                            .ToList();
                }
                else if (scope.HasManagerScopeRole && !scope.HasLocationScopeRole)
                {
                    return new List<Identify>();
                }
            }

            var locationFiltered = FilterUsersByLocation(
                identifies,
                addressLookup,
                effectiveAllowedGovernorates,
                exactGovernorate,
                scope.ManagedDistrict);

            if (scope.IsSuperAdmin || !scope.Assignments.Any())
            {
                return locationFiltered;
            }

            return locationFiltered
                .Where(identify =>
                {
                    addressLookup.TryGetValue(identify.UserId, out var address);
                    affiliationLookup.TryGetValue(identify.UserId, out var affiliationInfo);
                    var governorate = GetEffectiveGovernorate(identify, address);
                    var district = GetEffectiveDistrict(identify, address);

                    return scope.Assignments.Any(assignment =>
                        MatchesAssignmentScope(assignment, governorate, district, affiliationInfo));
                })
                .ToList();
        }

        private static bool IsCentralAssignment(ManagementAssignment assignment)
        {
            return string.IsNullOrWhiteSpace(assignment.Governorate) ||
                   IsAllGovernoratesScope(assignment.Governorate);
        }

        private static string NormalizeBaghdadScope(string? district)
        {
            return string.IsNullOrWhiteSpace(district) ? "مركزي" : district;
        }

        private static bool MatchesBaghdadScope(ManagementAssignment assignment, string governorate, string district)
        {
            if (governorate != "بغداد" || IsCentralAssignment(assignment))
            {
                return true;
            }

            var assignmentScope = NormalizeBaghdadScope(assignment.BaghdadScope);
            return assignmentScope == "مركزي" || assignmentScope == NormalizeBaghdadScope(district);
        }

        private bool MatchesAssignmentScope(ManagementAssignment assignment, string governorate, string district, AffiliationInfo? affiliationInfo)
        {
            if (!IsCentralAssignment(assignment) &&
                (string.IsNullOrWhiteSpace(governorate) ||
                 !IsGovernorateInManagedScope(governorate, assignment.Governorate)))
            {
                return false;
            }

            if (!MatchesBaghdadScope(assignment, governorate, district))
            {
                return false;
            }

            if (affiliationInfo == null)
            {
                return false;
            }

            bool MatchesEntity()
            {
                return assignment.AffiliationEntityId.HasValue &&
                       affiliationInfo.AffiliationEntityId == assignment.AffiliationEntityId;
            }

            bool MatchesDivision()
            {
                return assignment.DivisionId.HasValue &&
                       affiliationInfo.DivisionId == assignment.DivisionId;
            }

            bool MatchesSection()
            {
                return assignment.SectionId.HasValue &&
                       affiliationInfo.SectionId == assignment.SectionId;
            }

            bool MatchesGroup()
            {
                return assignment.GroupId.HasValue &&
                       affiliationInfo.GroupId == assignment.GroupId;
            }

            return assignment.ManagementLevel switch
            {
                "Entity" => MatchesEntity(),
                "Division" => MatchesEntity() && MatchesDivision(),
                "Section" => MatchesEntity() && MatchesDivision() && MatchesSection(),
                "Group" => MatchesEntity() && MatchesDivision() && MatchesSection() && MatchesGroup(),
                _ => false
            };
        }

        private List<string> GetGovernoratesForScope(MapAccessScope scope, bool allowUnscopedLocationRole = true)
        {
            if (scope.IsSuperAdmin)
            {
                return GetGovernoratesList();
            }

            if (!string.IsNullOrWhiteSpace(scope.ManagedGovernorate))
            {
                if (IsAllGovernoratesScope(scope.ManagedGovernorate))
                {
                    return GetGovernoratesList();
                }

                return ExpandManagedGovernorateScope(scope.ManagedGovernorate).ToList();
            }

            if (scope.Assignments.Any())
            {
                if (scope.Assignments.Any(IsCentralAssignment))
                {
                    return GetGovernoratesList();
                }

                return scope.Assignments
                    .Select(a => a.Governorate)
                    .Where(g => !string.IsNullOrWhiteSpace(g))
                    .SelectMany(ExpandManagedGovernorateScope)
                    .Where(g => g != "بغداد" && g != "بغداد مركزي")
                    .Distinct()
                    .ToList();
            }

            return allowUnscopedLocationRole && scope.HasLocationScopeRole
                ? GetGovernoratesList()
                : new List<string>();
        }

        private bool IsManagerScopedOnly(MapAccessScope scope)
        {
            return !scope.IsSuperAdmin &&
                   scope.HasManagerScopeRole &&
                   !scope.HasLocationScopeRole &&
                   scope.Assignments.Any();
        }

        private (List<AffiliationEntity> Entities, List<Division> Divisions, List<Section> Sections, List<Group> Groups)
            FilterAffiliationCatalogByScope(
                MapAccessScope scope,
                List<AffiliationEntity> entities,
                List<Division> divisions,
                List<Section> sections,
                List<Group> groups)
        {
            if (scope.IsSuperAdmin || !scope.Assignments.Any())
            {
                return (entities, divisions, sections, groups);
            }

            var entityIds = new HashSet<int>();
            var divisionIds = new HashSet<int>();
            var sectionIds = new HashSet<int>();
            var groupIds = new HashSet<int>();

            void AddDivisionWithChildren(int divisionId)
            {
                var division = divisions.FirstOrDefault(d => d.Id == divisionId);
                if (division == null) return;

                divisionIds.Add(division.Id);
                entityIds.Add(division.AffiliationEntityId);

                var childSections = sections.Where(s => s.DivisionId == division.Id).ToList();
                foreach (var childSection in childSections)
                {
                    sectionIds.Add(childSection.Id);
                    foreach (var childGroup in groups.Where(g => g.SectionId == childSection.Id))
                    {
                        groupIds.Add(childGroup.Id);
                    }
                }
            }

            void AddSectionWithChildren(int sectionId)
            {
                var section = sections.FirstOrDefault(s => s.Id == sectionId);
                if (section == null) return;

                sectionIds.Add(section.Id);
                var division = divisions.FirstOrDefault(d => d.Id == section.DivisionId);
                if (division != null)
                {
                    divisionIds.Add(division.Id);
                    entityIds.Add(division.AffiliationEntityId);
                }

                foreach (var childGroup in groups.Where(g => g.SectionId == section.Id))
                {
                    groupIds.Add(childGroup.Id);
                }
            }

            void AddGroupOnly(int groupId)
            {
                var group = groups.FirstOrDefault(g => g.Id == groupId);
                if (group == null) return;

                groupIds.Add(group.Id);
                var section = sections.FirstOrDefault(s => s.Id == group.SectionId);
                if (section == null) return;

                sectionIds.Add(section.Id);
                var division = divisions.FirstOrDefault(d => d.Id == section.DivisionId);
                if (division != null)
                {
                    divisionIds.Add(division.Id);
                    entityIds.Add(division.AffiliationEntityId);
                }
            }

            foreach (var assignment in scope.Assignments)
            {
                switch (assignment.ManagementLevel)
                {
                    case "Entity" when assignment.AffiliationEntityId.HasValue:
                        entityIds.Add(assignment.AffiliationEntityId.Value);
                        foreach (var division in divisions.Where(d => d.AffiliationEntityId == assignment.AffiliationEntityId.Value))
                        {
                            AddDivisionWithChildren(division.Id);
                        }
                        break;

                    case "Division" when assignment.DivisionId.HasValue:
                        AddDivisionWithChildren(assignment.DivisionId.Value);
                        break;

                    case "Section" when assignment.SectionId.HasValue:
                        AddSectionWithChildren(assignment.SectionId.Value);
                        break;

                    case "Group" when assignment.GroupId.HasValue:
                        AddGroupOnly(assignment.GroupId.Value);
                        break;
                }
            }

            return (
                entities.Where(e => entityIds.Contains(e.Id)).ToList(),
                divisions.Where(d => divisionIds.Contains(d.Id)).ToList(),
                sections.Where(s => sectionIds.Contains(s.Id)).ToList(),
                groups.Where(g => groupIds.Contains(g.Id)).ToList());
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

        // ✅ دالة مساعدة للحصول على الاسم الكامل لجهة الانتساب من المستويات الأربعة
        private string GetAffiliationFullName(AffiliationInfo? affiliation)
        {
            if (affiliation == null) return "";

            string fullName = "";

            if (affiliation.AffiliationEntity != null)
                fullName = affiliation.AffiliationEntity.Name;

            if (affiliation.Division != null)
                fullName += " - " + affiliation.Division.Name;

            if (affiliation.Section != null)
                fullName += " - " + affiliation.Section.Name;

            if (affiliation.Group != null)
                fullName += " - " + affiliation.Group.Name;

            return fullName;
        }

        private static bool IsIndividualAccount(Identify identify)
        {
            return !string.IsNullOrWhiteSpace(identify.Education) &&
                   (identify.IsPromoted || identify.AccountType == "فرد");
        }

        private static bool IsIndividualAccount(Identify identify, IReadOnlySet<string> memberRoleUserIds)
        {
            return IsIndividualAccount(identify) || memberRoleUserIds.Contains(identify.UserId);
        }

        private async Task<HashSet<string>> GetMemberRoleUserIdsAsync(IEnumerable<string> userIds)
        {
            var userIdSet = userIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet();

            if (!userIdSet.Any())
                return new HashSet<string>();

            var memberUserIds = await _context.UserRoles
                .Where(ur => userIdSet.Contains(ur.UserId))
                .Join(
                    _context.Roles,
                    userRole => userRole.RoleId,
                    role => role.Id,
                    (userRole, role) => new { userRole.UserId, RoleName = role.Name ?? string.Empty })
                .Where(x => x.RoleName == "فرد")
                .Select(x => x.UserId)
                .Distinct()
                .ToListAsync();

            return memberUserIds.ToHashSet();
        }

        private int CountPromotedAffiliationMembersByName(
            IEnumerable<AffiliationInfo> affiliations,
            IReadOnlySet<string> promotedUserIds,
            string name)
        {
            return affiliations
                .Where(a =>
                    promotedUserIds.Contains(a.UserId) &&
                    (
                        a.Division?.Name == name ||
                        a.Section?.Name == name ||
                        a.Group?.Name == name
                    ))
                .Select(a => a.UserId)
                .Distinct()
                .Count();
        }

        // ===== الصفحة الرئيسية =====
        public async Task<IActionResult> Index()
        {
            try
            {
                var model = new MapDashboardViewModel();
                var allGovernorates = GetGovernoratesList();

                var accessScope = await GetCurrentMapAccessScopeAsync();
                var userManagedGovernorate = accessScope.ManagedGovernorate;
                var userManagedDistrict = accessScope.ManagedDistrict;
                bool isSuperAdmin = accessScope.IsSuperAdmin;

                List<string> allowedGovernorates = GetGovernoratesForScope(accessScope);
                if (!allowedGovernorates.Any())
                {
                    TempData["WarningMessage"] = "لا يوجد نطاق إداري مخصص لهذا الحساب حالياً.";
                    return View(model);
                }

                if (!isSuperAdmin && !string.IsNullOrEmpty(userManagedGovernorate))
                {
                    allowedGovernorates = IsAllGovernoratesScope(userManagedGovernorate)
                        ? GetGovernoratesList()
                        : ExpandManagedGovernorateScope(userManagedGovernorate)
                            .Where(g => allGovernorates.Contains(g))
                            .ToList();

                    if (!allowedGovernorates.Any(g => allGovernorates.Contains(g)))
                    {
                        _logger.LogWarning($"Admin {User.Identity?.Name} has invalid managed governorate: {userManagedGovernorate}");
                        TempData["ErrorMessage"] = "المحافظة المخصصة لك غير صحيحة، يرجى مراجعة المدير.";
                        return View(new MapDashboardViewModel());
                    }
                }

                var allUsers = _userManager.Users.ToList();
                var allIdentifies = await _context.Identifies
                    .Include(i => i.WorkLocation)
                    .ToListAsync();
                var allAddresses = await _context.Addresses.ToListAsync();
                var addressLookup = BuildAddressLookup(allAddresses);
                var allAffiliations = await _context.AffiliationInfos
                    .Include(a => a.AffiliationEntity)
                    .Include(a => a.Division)
                    .Include(a => a.Section)
                    .Include(a => a.Group)
                    .ToListAsync();
                var affiliationLookup = allAffiliations
                    .Where(a => !string.IsNullOrWhiteSpace(a.UserId))
                    .GroupBy(a => a.UserId)
                    .ToDictionary(g => g.Key, g => g.First());

                var allUnions = await _context.UnionMemberships.ToListAsync();
                var allFederations = await _context.FederationMemberships
                    .Include(f => f.Federation)
                    .Include(f => f.FederationDivision)
                    .Include(f => f.FederationSection)
                    .Include(f => f.FederationGroup)
                    .ToListAsync();
                var allAssociations = await _context.AssociationMemberships.ToListAsync();
                var allNgos = await _context.NgoMemberships.ToListAsync();

                var allUnionsMaster = await _context.Unions.ToListAsync();
                var allFederationsMaster = await _context.Federations.ToListAsync();
                var allAssociationsMaster = await _context.Associations.ToListAsync();
                var allNgosMaster = await _context.Ngos.ToListAsync();

                var allAffiliationEntities = await _context.AffiliationEntities.ToListAsync();
                var allDivisions = await _context.Divisions.ToListAsync();
                var allSections = await _context.Sections.ToListAsync();
                var allGroups = await _context.Groups.ToListAsync();
                var allManagementAssignments = await _context.ManagementAssignments
                    .AsNoTracking()
                    .ToListAsync();
                var scopedCatalog = FilterAffiliationCatalogByScope(
                    accessScope,
                    allAffiliationEntities,
                    allDivisions,
                    allSections,
                    allGroups);

                if (IsManagerScopedOnly(accessScope))
                {
                    var primaryAssignment = accessScope.Assignments.First();
                    var scopeParts = new List<string>();

                    if (!string.IsNullOrWhiteSpace(primaryAssignment.Governorate))
                    {
                        scopeParts.Add($"محافظة {primaryAssignment.Governorate}");
                    }

                    var assignmentName = primaryAssignment.ManagementLevel switch
                    {
                        "Entity" when primaryAssignment.AffiliationEntityId.HasValue =>
                            allAffiliationEntities.FirstOrDefault(e => e.Id == primaryAssignment.AffiliationEntityId.Value)?.Name,
                        "Division" when primaryAssignment.DivisionId.HasValue =>
                            allDivisions.FirstOrDefault(d => d.Id == primaryAssignment.DivisionId.Value)?.Name,
                        "Section" when primaryAssignment.SectionId.HasValue =>
                            allSections.FirstOrDefault(s => s.Id == primaryAssignment.SectionId.Value)?.Name,
                        "Group" when primaryAssignment.GroupId.HasValue =>
                            allGroups.FirstOrDefault(g => g.Id == primaryAssignment.GroupId.Value)?.Name,
                        _ => null
                    };

                    var levelName = primaryAssignment.ManagementLevel switch
                    {
                        "Entity" => "جهة",
                        "Division" => "قسم",
                        "Section" => "شعبة",
                        "Group" => "وحدة",
                        _ => "نطاق"
                    };

                    var roleName = primaryAssignment.AssignmentRole == "Assistant" ? "معاون" : "مسؤول";

                    model.IsManagerScopedView = true;
                    model.ManagerScopeTitle = string.IsNullOrWhiteSpace(assignmentName)
                        ? $"{roleName} {levelName}"
                        : $"{roleName} {levelName}: {assignmentName}";
                    model.ManagerScopeDescription = scopeParts.Any()
                        ? string.Join(" - ", scopeParts)
                        : "عرض مخصص حسب نطاق التكليف الإداري";
                }

                allAffiliationEntities = scopedCatalog.Entities;
                allDivisions = scopedCatalog.Divisions;
                allSections = scopedCatalog.Sections;
                allGroups = scopedCatalog.Groups;

                var filteredIdentifies = FilterUsersByScope(
                    allIdentifies,
                    addressLookup,
                    affiliationLookup,
                    accessScope);

                var filteredUserIds = filteredIdentifies.Select(i => i.UserId).ToHashSet();
                var usersWithRoles = await _context.UserRoles
                    .Where(ur => filteredUserIds.Contains(ur.UserId))
                    .Join(
                        _context.Roles,
                        userRole => userRole.RoleId,
                        role => role.Id,
                        (userRole, role) => new { userRole.UserId, RoleName = role.Name ?? string.Empty })
                    .GroupBy(x => x.UserId)
                    .ToDictionaryAsync(
                        group => group.Key,
                        group => (IList<string>)group.Select(x => x.RoleName).ToList());
                var memberRoleUserIds = usersWithRoles
                    .Where(x => x.Value.Contains("فرد"))
                    .Select(x => x.Key)
                    .ToHashSet();

                var promotedUsers = filteredIdentifies
                    .Where(i => IsIndividualAccount(i, memberRoleUserIds))
                    .ToList();
                var promotedUserIds = promotedUsers.Select(i => i.UserId).ToHashSet();
                var assignmentsByUserId = allManagementAssignments
                    .Where(a => !string.IsNullOrWhiteSpace(a.UserId))
                    .GroupBy(a => a.UserId)
                    .ToDictionary(g => g.Key, g => g.First());

                string GetStatisticsGovernorate(Identify identify)
                {
                    addressLookup.TryGetValue(identify.UserId, out var address);
                    var governorate = GetEffectiveGovernorate(identify, address);
                    if (!string.IsNullOrWhiteSpace(governorate))
                        return governorate;

                    return assignmentsByUserId.TryGetValue(identify.UserId, out var assignment)
                        ? assignment.Governorate
                        : string.Empty;
                }

                int CountIndividualAffiliationUsers(
                    Func<AffiliationInfo, bool> affiliationPredicate,
                    Func<ManagementAssignment, bool> assignmentPredicate,
                    IReadOnlySet<string>? eligibleUserIds = null)
                {
                    var userIdsToCount = eligibleUserIds ?? promotedUserIds;

                    return allAffiliations
                        .Where(affiliationPredicate)
                        .Select(a => a.UserId)
                        .Concat(allManagementAssignments
                            .Where(assignmentPredicate)
                            .Select(a => a.UserId))
                        .Where(userIdsToCount.Contains)
                        .Distinct()
                        .Count();
                }

                int CountIndividualAffiliationUsersByName(string name, IReadOnlySet<string>? eligibleUserIds = null)
                {
                    var divisionIds = allDivisions
                        .Where(d => d.Name == name)
                        .Select(d => d.Id)
                        .ToHashSet();
                    var sectionIds = allSections
                        .Where(s => s.Name == name)
                        .Select(s => s.Id)
                        .ToHashSet();
                    var groupIds = allGroups
                        .Where(g => g.Name == name)
                        .Select(g => g.Id)
                        .ToHashSet();

                    return CountIndividualAffiliationUsers(
                        a =>
                            (a.DivisionId.HasValue && divisionIds.Contains(a.DivisionId.Value)) ||
                            (a.SectionId.HasValue && sectionIds.Contains(a.SectionId.Value)) ||
                            (a.GroupId.HasValue && groupIds.Contains(a.GroupId.Value)),
                        a =>
                            (a.DivisionId.HasValue && divisionIds.Contains(a.DivisionId.Value)) ||
                            (a.SectionId.HasValue && sectionIds.Contains(a.SectionId.Value)) ||
                            (a.GroupId.HasValue && groupIds.Contains(a.GroupId.Value)),
                        eligibleUserIds);
                }

                model.TotalIndividuals = promotedUsers.Count;
                model.TotalUsers = allUsers.Count(u => filteredUserIds.Contains(u.Id));

                // ✅ إحصائيات الذكور والإناث الكلية
                model.TotalMaleIndividuals = promotedUsers.Count(u => u.Gender == "ذكر");
                model.TotalFemaleIndividuals = promotedUsers.Count(u => u.Gender == "أنثى");

                var uniqueUsersInUnions = allUnions
                    .Where(u => promotedUserIds.Contains(u.UserId))
                    .Select(u => u.UserId)
                    .Distinct()
                    .Count();

                var uniqueUsersInFederations = allFederations
                    .Where(f => promotedUserIds.Contains(f.UserId))
                    .Select(f => f.UserId)
                    .Distinct()
                    .Count();

                var uniqueUsersInAssociations = allAssociations
                    .Where(a => promotedUserIds.Contains(a.UserId))
                    .Select(a => a.UserId)
                    .Distinct()
                    .Count();

                var uniqueUsersInNgos = allNgos
                    .Where(n => promotedUserIds.Contains(n.UserId))
                    .Select(n => n.UserId)
                    .Distinct()
                    .Count();

                model.TotalUnionMembers = uniqueUsersInUnions;
                model.TotalFederationMembers = uniqueUsersInFederations;
                model.TotalAssociationMembers = uniqueUsersInAssociations;
                model.TotalNgoMembers = uniqueUsersInNgos;

                model.TotalUnionTypes = allUnionsMaster.Count;
                model.TotalFederationTypes = allFederationsMaster.Count;
                model.TotalAssociationTypes = allAssociationsMaster.Count;
                model.TotalNgoTypes = allNgosMaster.Count;

                model.TotalSuperAdmins = usersWithRoles.Count(u => u.Value.Contains("SuperAdmin"));
                model.TotalAdmins = usersWithRoles.Count(u => u.Value.Contains("Admin") && !u.Value.Contains("SuperAdmin"));

                var affiliationStats = new List<AffiliationDetail>();
                foreach (var entity in allAffiliationEntities)
                {
                    var entityDivisionIds = allDivisions
                        .Where(d => d.AffiliationEntityId == entity.Id)
                        .Select(d => d.Id)
                        .ToHashSet();
                    var entitySectionIds = allSections
                        .Where(s => entityDivisionIds.Contains(s.DivisionId))
                        .Select(s => s.Id)
                        .ToHashSet();
                    var entityGroupIds = allGroups
                        .Where(g => entitySectionIds.Contains(g.SectionId))
                        .Select(g => g.Id)
                        .ToHashSet();

                    var memberCount = CountIndividualAffiliationUsers(
                        a => a.AffiliationEntityId == entity.Id,
                        a =>
                            a.AffiliationEntityId == entity.Id ||
                            (a.DivisionId.HasValue && entityDivisionIds.Contains(a.DivisionId.Value)) ||
                            (a.SectionId.HasValue && entitySectionIds.Contains(a.SectionId.Value)) ||
                            (a.GroupId.HasValue && entityGroupIds.Contains(a.GroupId.Value)));

                    affiliationStats.Add(new AffiliationDetail
                    {
                        Id = entity.Id,
                        Name = entity.Name,
                        Count = memberCount
                    });
                }

                model.Affiliations = affiliationStats
    .OrderByDescending(a => a.Count)
    .ToList();

                model.AffiliationDivisions = allDivisions
                    .Select(division =>
                    {
                        var divisionSectionIds = allSections
                            .Where(s => s.DivisionId == division.Id)
                            .Select(s => s.Id)
                            .ToHashSet();
                        var divisionGroupIds = allGroups
                            .Where(g => divisionSectionIds.Contains(g.SectionId))
                            .Select(g => g.Id)
                            .ToHashSet();

                        return new AffiliationDivisionDetail
                        {
                            Id = division.Id,
                            Name = division.Name,
                            Count = CountIndividualAffiliationUsers(
                            a => a.DivisionId == division.Id,
                                a =>
                                    a.DivisionId == division.Id ||
                                    (a.SectionId.HasValue && divisionSectionIds.Contains(a.SectionId.Value)) ||
                                    (a.GroupId.HasValue && divisionGroupIds.Contains(a.GroupId.Value)))
                        };
                    })
                    .OrderByDescending(d => d.Count)
                    .ThenBy(d => d.Name)
                    .ToList();

                model.AffiliationSections = allSections
                    .Select(section =>
                    {
                        var sectionGroupIds = allGroups
                            .Where(g => g.SectionId == section.Id)
                            .Select(g => g.Id)
                            .ToHashSet();

                        return new AffiliationSectionDetail
                        {
                            Id = section.Id,
                            Name = section.Name,
                            Count = CountIndividualAffiliationUsers(
                            a => a.SectionId == section.Id,
                                a =>
                                    a.SectionId == section.Id ||
                                    (a.GroupId.HasValue && sectionGroupIds.Contains(a.GroupId.Value)))
                        };
                    })
                    .OrderByDescending(section => section.Count)
                    .ThenBy(section => section.Name)
                    .ToList();

                var specializedGatheringsSectionIds = allSections
                    .Where(section => section.Name == "التجمعات التخصصية")
                    .Select(section => section.Id)
                    .ToHashSet();

                model.AffiliationGroups = allGroups
                    .Where(group => specializedGatheringsSectionIds.Contains(group.SectionId))
                    .Select(group => new AffiliationGroupDetail
                    {
                        Id = group.Id,
                        Name = group.Name,
                        Count = CountIndividualAffiliationUsers(
                            a => a.GroupId == group.Id,
                            a => a.GroupId == group.Id)
                    })
                    .OrderByDescending(g => g.Count)
                    .ThenBy(g => g.Name)
                    .ToList();

                // ✅ إحصائيات الاتحادات (مثل الانتساب)
                var federationsStats = allFederationsMaster.Select(master => new FederationStat
                {
                    Name = master.Name,
                    Count = allFederations
                        .Count(f => f.Federation != null && f.Federation.Name == master.Name && promotedUserIds.Contains(f.UserId))
                }).OrderByDescending(x => x.Count).ToList();

                model.FederationsStats = federationsStats;

                model.TotalGovernorates = allowedGovernorates.Count;
                model.LastUpdated = DateTime.Now;
                int coveredCount = 0;

                foreach (var gov in allowedGovernorates)
                {
                    // ✅ تصفية المستخدمين حسب المحافظة والقضاء
                    var allUserIdsInGov = filteredIdentifies
                        .Where(i =>
                        {
                            return IsGovernorateInStatisticsBucket(GetStatisticsGovernorate(i), gov);
                        })
                        .Select(i => i.UserId)
                        .ToList();

                    var promotedUsersInGov = promotedUsers
                        .Where(i => allUserIdsInGov.Contains(i.UserId))
                        .ToList();

                    var userIds = promotedUsersInGov.Select(u => u.UserId).ToList();

                    if (promotedUsersInGov.Any())
                    {
                        coveredCount++;
                    }

                    var unionCount = allUnions
                        .Where(u => userIds.Contains(u.UserId) && !string.IsNullOrEmpty(u.UnionName))
                        .Select(u => u.UserId)
                        .Distinct()
                        .Count();

                    var federationCount = allFederations
                        .Where(f => userIds.Contains(f.UserId) && f.FederationId.HasValue)
                        .Select(f => f.UserId)
                        .Distinct()
                        .Count();

                    var associationCount = allAssociations
                        .Where(a => userIds.Contains(a.UserId) && !string.IsNullOrEmpty(a.AssociationName))
                        .Select(a => a.UserId)
                        .Distinct()
                        .Count();

                    var ngoCount = allNgos
                        .Where(n => userIds.Contains(n.UserId) && !string.IsNullOrEmpty(n.NgoName))
                        .Select(n => n.UserId)
                        .Distinct()
                        .Count();

                    var affiliatedCount = allAffiliations
                        .Count(a => userIds.Contains(a.UserId) && a.AffiliationEntityId.HasValue);

                    var promotedUserIdsInGov = userIds.ToHashSet();

                    var totalIndividualsSectionCount = CountIndividualAffiliationUsersByName("الأفراد", promotedUserIdsInGov);
                    var iraqStudentsOfficeCount = CountIndividualAffiliationUsersByName("مكتب طلبة العراق", promotedUserIdsInGov);
                    var iraqFemaleStudentsOfficeCount = CountIndividualAffiliationUsersByName("مكتب طالبات العراق", promotedUserIdsInGov);
                    var womenOfficeCount = CountIndividualAffiliationUsersByName("المكتب النسوي", promotedUserIdsInGov);
                    var professionalOfficeCount = CountIndividualAffiliationUsersByName("المكتب المهني", promotedUserIdsInGov);
                    var specializedGatheringsCount = CountIndividualAffiliationUsersByName("التجمعات التخصصية", promotedUserIdsInGov);

                    var maleCount = promotedUsersInGov.Count(u => u.Gender == "ذكر");
                    var femaleCount = promotedUsersInGov.Count(u => u.Gender == "أنثى");
                    var individualCount = promotedUsersInGov.Count;

                    var adminsInGov = 0;
                    var superAdminsInGov = 0;

                    foreach (var userId in userIds)
                    {
                        if (usersWithRoles.TryGetValue(userId, out var roles))
                        {
                            if (roles.Contains("SuperAdmin")) superAdminsInGov++;
                            if (roles.Contains("Admin")) adminsInGov++;
                        }
                    }

                    var govData = new GovernorateDetail
                    {
                        Id = GetGovernorateId(gov),
                        Name = gov,
                        TotalUsers = promotedUsersInGov.Count,
                        MaleCount = maleCount,
                        FemaleCount = femaleCount,
                        IndividualCount = individualCount,
                        AffiliatedCount = affiliatedCount,
                        TotalIndividualsSectionCount = totalIndividualsSectionCount,
                        IraqStudentsOfficeCount = iraqStudentsOfficeCount,
                        IraqFemaleStudentsOfficeCount = iraqFemaleStudentsOfficeCount,
                        WomenOfficeCount = womenOfficeCount,
                        ProfessionalOfficeCount = professionalOfficeCount,
                        SpecializedGatheringsCount = specializedGatheringsCount,
                        UnionCount = unionCount,
                        FederationCount = federationCount,
                        AssociationCount = associationCount,
                        NgoCount = ngoCount,
                        AdminCount = adminsInGov,
                        SuperAdminCount = superAdminsInGov,
                        LastActivity = promotedUsersInGov.Any() ? promotedUsersInGov.Max(u => u.CreatedAt) : null,
                        ColorClass = GetGovernorateColorClass(promotedUsersInGov.Count),
                        CenterX = GetGovernorateCoordinate(gov).Item1,
                        CenterY = GetGovernorateCoordinate(gov).Item2
                    };

                    model.Governorates.Add(govData);
                }

                model.CoveredGovernorates = coveredCount;

                _logger.LogInformation($"""
                    ========== إحصائيات لوحة التحكم ==========
                    المستخدم: {User.Identity?.Name}
                    الدور: {(isSuperAdmin ? "SuperAdmin" : (string.IsNullOrEmpty(userManagedGovernorate) ? "MapViewer" : (string.IsNullOrEmpty(userManagedDistrict) ? "Admin" : "DistrictAdmin")))}
                    المحافظات المسموحة: {string.Join(", ", allowedGovernorates)}
                    القضاء المُدار: {userManagedDistrict ?? "لا يوجد"}
                    إجمالي المصعدين: {model.TotalIndividuals}
                    ==========================================
                    """);

                if (!isSuperAdmin && !string.IsNullOrEmpty(userManagedGovernorate) && model.TotalIndividuals == 0)
                {
                    string locationName = !string.IsNullOrEmpty(userManagedDistrict)
                        ? $"قضاء {userManagedDistrict} في محافظة {userManagedGovernorate}"
                        : $"محافظة {userManagedGovernorate}";
                    TempData["WarningMessage"] = $"لا يوجد أعضاء مصعدون في {locationName} حالياً.";
                }

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في تحميل خريطة العراق");
                TempData["ErrorMessage"] = "حدث خطأ في تحميل البيانات";
                return View(new MapDashboardViewModel());
            }
        }

        // ===== باقي الدوال (GetManagerStatistics, GetGovernorateDetails, GetMembersByAffiliation, 
        // GetManagerStatisticsByGovernorate, GetMembersByMembershipType, GetMembershipStatistics,
        // GetAffiliationHierarchy, GetFederationHierarchy, GetGenderStatistics, GetExportData,
        // GetEducationStatistics, GetEducationDetails, GetAllEducationDetails) تبقى كما هي دون تغيير =====

        private string GetGovernorateColorClass(int userCount)
        {
            return userCount switch
            {
                0 => "governorate-empty",
                > 0 and <= 10 => "governorate-low",
                > 10 and <= 50 => "governorate-medium",
                > 50 and <= 100 => "governorate-high",
                > 100 => "governorate-very-high",
                _ => "governorate-default"
            };
        }

        // ===== جلب إحصائيات المسؤولين الإداريين (جهات - أقسام - شعب - تجمعات) =====
        [HttpGet]
        public async Task<IActionResult> GetManagerStatistics()
        {
            try
            {
                var accessScope = await GetCurrentMapAccessScopeAsync();

                var allAddresses = await _context.Addresses.ToListAsync();
                var allIdentifies = await _context.Identifies.ToListAsync();
                var addressLookup = BuildAddressLookup(allAddresses);
                var allManagementAssignments = await _context.ManagementAssignments.ToListAsync();
                var allAffiliations = await _context.AffiliationInfos.ToListAsync();
                var affiliationLookup = allAffiliations
                    .Where(a => !string.IsNullOrWhiteSpace(a.UserId))
                    .GroupBy(a => a.UserId)
                    .ToDictionary(g => g.Key, g => g.First());

                // تصفية المحافظات المسموحة
                List<string> allowedGovernorates = GetGovernoratesForScope(accessScope);

                var filteredIdentifies = FilterUsersByScope(
                    allIdentifies,
                    addressLookup,
                    affiliationLookup,
                    accessScope,
                    allowedGovernorates);

                var allowedUserIds = filteredIdentifies.Select(i => i.UserId).ToHashSet();

                // تصفية التعيينات حسب المستخدمين المسموحين
                var filteredAssignments = allManagementAssignments
                    .Where(a => allowedUserIds.Contains(a.UserId))
                    .ToList();

                var entityManagers = filteredAssignments.Count(a => a.ManagementLevel == "Entity" && a.AssignmentRole == "Manager");
                var divisionManagers = filteredAssignments.Count(a => a.ManagementLevel == "Division" && a.AssignmentRole == "Manager");
                var sectionManagers = filteredAssignments.Count(a => a.ManagementLevel == "Section" && a.AssignmentRole == "Manager");
                var groupManagers = filteredAssignments.Count(a => a.ManagementLevel == "Group" && a.AssignmentRole == "Manager");

                var entityAssistants = filteredAssignments.Count(a => a.ManagementLevel == "Entity" && a.AssignmentRole == "Assistant");
                var divisionAssistants = filteredAssignments.Count(a => a.ManagementLevel == "Division" && a.AssignmentRole == "Assistant");
                var sectionAssistants = filteredAssignments.Count(a => a.ManagementLevel == "Section" && a.AssignmentRole == "Assistant");
                var groupAssistants = filteredAssignments.Count(a => a.ManagementLevel == "Group" && a.AssignmentRole == "Assistant");

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        entityManagers = entityManagers,
                        divisionManagers = divisionManagers,
                        sectionManagers = sectionManagers,
                        groupManagers = groupManagers,
                        entityAssistants = entityAssistants,
                        divisionAssistants = divisionAssistants,
                        sectionAssistants = sectionAssistants,
                        groupAssistants = groupAssistants
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في جلب إحصائيات المسؤولين");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ===== جلب تفاصيل محافظة محددة =====
        [HttpGet]
        public async Task<IActionResult> GetGovernorateDetails(string governorate)
        {
            try
            {
                if (string.IsNullOrEmpty(governorate))
                {
                    return Json(new { success = false, message = "اسم المحافظة مطلوب" });
                }

                var accessScope = await GetCurrentMapAccessScopeAsync();
                bool isSuperAdmin = accessScope.IsSuperAdmin;
                var allowedGovernorates = GetGovernoratesForScope(accessScope);

                if (!isSuperAdmin && !allowedGovernorates.Any(g => IsGovernorateInManagedScope(governorate, g)))
                {
                    _logger.LogWarning($"User {User.Identity?.Name} attempted to access unauthorized governorate: {governorate}");
                    return Json(new { success = false, message = "غير مصرح لك بعرض بيانات هذه المحافظة" });
                }

                var allUsers = _userManager.Users.ToList();
                var allIdentifies = await _context.Identifies
                    .Include(i => i.WorkLocation)
                    .ToListAsync();
                var allAddresses = await _context.Addresses.ToListAsync();
                var addressLookup = BuildAddressLookup(allAddresses);
                var allAffiliations = await _context.AffiliationInfos
                    .Include(a => a.AffiliationEntity)
                    .Include(a => a.Division)
                    .Include(a => a.Section)
                    .Include(a => a.Group)
                    .ToListAsync();
                var affiliationLookup = allAffiliations
                    .Where(a => !string.IsNullOrWhiteSpace(a.UserId))
                    .GroupBy(a => a.UserId)
                    .ToDictionary(g => g.Key, g => g.First());

                var allUnions = await _context.UnionMemberships.ToListAsync();
                var allFederations = await _context.FederationMemberships
                    .Include(f => f.Federation)
                    .Include(f => f.FederationDivision)
                    .Include(f => f.FederationSection)
                    .Include(f => f.FederationGroup)
                    .ToListAsync();
                var allAssociations = await _context.AssociationMemberships.ToListAsync();
                var allNgos = await _context.NgoMemberships.ToListAsync();

                var allAffiliationEntities = await _context.AffiliationEntities.ToListAsync();
                var allDivisions = await _context.Divisions.ToListAsync();
                var allSections = await _context.Sections.ToListAsync();
                var allGroups = await _context.Groups.ToListAsync();

                var filteredIdentifies = FilterUsersByScope(
                    allIdentifies,
                    addressLookup,
                    affiliationLookup,
                    accessScope,
                    exactGovernorate: governorate);

                var allUserIdsInGov = filteredIdentifies.Select(i => i.UserId).ToHashSet();
                var userIdsInGov = filteredIdentifies.Select(i => i.UserId).ToList();
                var memberRoleUserIds = await GetMemberRoleUserIdsAsync(userIdsInGov);

                var usersInGov = allIdentifies
                    .Where(i => userIdsInGov.Contains(i.UserId) && IsIndividualAccount(i, memberRoleUserIds))
                    .ToList();

                var userIds = usersInGov.Select(u => u.UserId).ToList();

                var usersWithRoles = await _context.UserRoles
                    .Where(ur => userIds.Contains(ur.UserId))
                    .Join(
                        _context.Roles,
                        userRole => userRole.RoleId,
                        role => role.Id,
                        (userRole, role) => new { userRole.UserId, RoleName = role.Name ?? string.Empty })
                    .GroupBy(x => x.UserId)
                    .ToDictionaryAsync(
                        group => group.Key,
                        group => (IList<string>)group.Select(x => x.RoleName).ToList());

                var affiliationsDict = allAffiliations.Where(a => userIds.Contains(a.UserId)).ToDictionary(a => a.UserId, a => a);
                var unionsDict = allUnions.Where(u => userIds.Contains(u.UserId)).ToDictionary(u => u.UserId, u => u);
                var federationsDict = allFederations.Where(f => userIds.Contains(f.UserId)).ToDictionary(f => f.UserId, f => f);
                var associationsDict = allAssociations.Where(a => userIds.Contains(a.UserId)).ToDictionary(a => a.UserId, a => a);
                var ngosDict = allNgos.Where(n => userIds.Contains(n.UserId)).ToDictionary(n => n.UserId, n => n);

                var maleCount = usersInGov.Count(u => u.Gender == "ذكر");
                var femaleCount = usersInGov.Count(u => u.Gender == "أنثى");
                var adminCount = 0;
                var superAdminCount = 0;
                var affiliatedCount = affiliationsDict.Count;
                var unionCount = unionsDict.Select(u => u.Key).Distinct().Count();
                var federationCount = federationsDict.Select(f => f.Key).Distinct().Count();
                var associationCount = associationsDict.Select(a => a.Key).Distinct().Count();
                var ngoCount = ngosDict.Select(n => n.Key).Distinct().Count();

                foreach (var user in usersInGov)
                {
                    if (usersWithRoles.TryGetValue(user.UserId, out var roles))
                    {
                        if (roles.Contains("SuperAdmin")) superAdminCount++;
                        if (roles.Contains("Admin")) adminCount++;
                    }
                }

                var employmentStats = new
                {
                    موظف = usersInGov.Count(u => u.EmploymentStatus == "موظف"),
                    متقاعد = usersInGov.Count(u => u.EmploymentStatus == "متقاعد"),
                    كاسب = usersInGov.Count(u => u.EmploymentStatus == "كاسب"),
                    طالب = usersInGov.Count(u => u.EmploymentStatus == "طالب"),
                    حر = usersInGov.Count(u => u.EmploymentStatus == "حر"),
                    قطاعخاص = usersInGov.Count(u => u.EmploymentStatus == "قطاع خاص")
                };
                var promotedUserIdsInGov = userIds.ToHashSet();

                var totalIndividualsSectionCount = CountPromotedAffiliationMembersByName(
                    allAffiliations,
                    promotedUserIdsInGov,
                    "الأفراد");

                var iraqStudentsOfficeCount = CountPromotedAffiliationMembersByName(
                    allAffiliations,
                    promotedUserIdsInGov,
                    "مكتب طلبة العراق");

                var iraqFemaleStudentsOfficeCount = CountPromotedAffiliationMembersByName(
                    allAffiliations,
                    promotedUserIdsInGov,
                    "مكتب طالبات العراق");

                var womenOfficeCount = CountPromotedAffiliationMembersByName(
                    allAffiliations,
                    promotedUserIdsInGov,
                    "المكتب النسوي");

                var professionalOfficeCount = CountPromotedAffiliationMembersByName(
                    allAffiliations,
                    promotedUserIdsInGov,
                    "المكتب المهني");

                var specializedGatheringsCount = CountPromotedAffiliationMembersByName(
                    allAffiliations,
                    promotedUserIdsInGov,
                    "التجمعات التخصصية");

                var stats = new
                {
                    totalUsers = usersInGov.Count,
                    maleCount = maleCount,
                    femaleCount = femaleCount,
                    adminCount = adminCount,
                    superAdminCount = superAdminCount,
                    affiliatedCount = affiliatedCount,
                    unionCount = unionCount,
                    federationCount = federationCount,
                    associationCount = associationCount,
                    ngoCount = ngoCount,

                    totalIndividualsSectionCount = totalIndividualsSectionCount,
                    iraqStudentsOfficeCount = iraqStudentsOfficeCount,
                    iraqFemaleStudentsOfficeCount = iraqFemaleStudentsOfficeCount,
                    womenOfficeCount = womenOfficeCount,
                    professionalOfficeCount = professionalOfficeCount,
                    specializedGatheringsCount = specializedGatheringsCount,

                    employmentStats = employmentStats
                };

                var usersDataList = new List<object>();
                foreach (var user in usersInGov.OrderByDescending(u => u.CreatedAt))
                {
                    var identityUser = allUsers.FirstOrDefault(u => u.Id == user.UserId);
                    addressLookup.TryGetValue(user.UserId, out var address);
                    var affiliationInfo = affiliationsDict.ContainsKey(user.UserId) ? affiliationsDict[user.UserId] : null;
                    var roles = usersWithRoles.TryGetValue(user.UserId, out var userRoles) ? userRoles : new List<string>();

                    string affiliationEntityName = null;
                    string divisionName = null;
                    string sectionName = null;
                    string groupName = null;

                    if (affiliationInfo != null)
                    {
                        if (affiliationInfo.AffiliationEntityId.HasValue)
                        {
                            var entity = allAffiliationEntities.FirstOrDefault(e => e.Id == affiliationInfo.AffiliationEntityId.Value);
                            affiliationEntityName = entity?.Name;
                        }
                        if (affiliationInfo.DivisionId.HasValue)
                        {
                            var division = allDivisions.FirstOrDefault(d => d.Id == affiliationInfo.DivisionId.Value);
                            divisionName = division?.Name;
                        }
                        if (affiliationInfo.SectionId.HasValue)
                        {
                            var section = allSections.FirstOrDefault(s => s.Id == affiliationInfo.SectionId.Value);
                            sectionName = section?.Name;
                        }
                        if (affiliationInfo.GroupId.HasValue)
                        {
                            var group = allGroups.FirstOrDefault(g => g.Id == affiliationInfo.GroupId.Value);
                            groupName = group?.Name;
                        }
                    }

                    string federationFullName = "";
                    string federationName = "";
                    string federationDivisionName = "";
                    string federationSectionName = "";
                    string federationGroupName = "";

                    if (federationsDict.ContainsKey(user.UserId))
                    {
                        var fed = federationsDict[user.UserId];

                        // الاسم الكامل
                        federationFullName = GetFederationFullName(fed);

                        // الأسماء المنفصلة
                        if (fed.Federation != null)
                            federationName = fed.Federation.Name;
                        if (fed.FederationDivision != null)
                            federationDivisionName = fed.FederationDivision.Name;
                        if (fed.FederationSection != null)
                            federationSectionName = fed.FederationSection.Name;
                        if (fed.FederationGroup != null)
                            federationGroupName = fed.FederationGroup.Name;
                    }

                    usersDataList.Add(new
                    {
                        fullName = user.FullName ?? "غير محدد",
                        motherName = user.MotherName ?? "",
                        email = identityUser?.Email ?? "غير محدد",
                        phoneNumber = user.PhoneNumber ?? "",
                        gender = user.Gender ?? "",
                        dateOfBirth = user.Date > DateTime.MinValue ? user.Date.ToString("yyyy-MM-dd") : "",
                        address = new
                        {
                            governorate = GetEffectiveGovernorate(user, address),
                            district = GetEffectiveDistrict(user, address),
                            area = address?.Area ?? "",
                            street = address?.Street ?? "",
                            house = address?.House ?? ""
                        },
                        identityCardNumber = user.IdentityCardN,
                        employment = new
                        {
                            status = user.EmploymentStatus ?? "",
                            work = user.Work ?? "",
                            ministry = user.Ministry ?? "",
                            department = user.Department ?? "",
                            position = user.Position ?? ""
                        },
                        affiliationInfo = affiliationInfo != null ? new
                        {
                            entity = affiliationEntityName,
                            division = divisionName,
                            section = sectionName,
                            group = groupName,
                            mozakeName = affiliationInfo.MozakeName,
                            mozakePhone = affiliationInfo.MozakePhoneNumber,
                            badgeNumber = affiliationInfo.BadgeNumber,
                            affiliationDate = affiliationInfo.AffiliationDate?.ToString("yyyy-MM-dd") ?? ""
                        } : null,
                        unionDetails = unionsDict.ContainsKey(user.UserId) ? new
                        {
                            name = unionsDict[user.UserId].UnionName,
                            position = unionsDict[user.UserId].Position,
                            idNumber = unionsDict[user.UserId].IdNumber
                        } : null,
                        federationDetails = federationsDict.ContainsKey(user.UserId) ? new
                        {
                            fullName = federationFullName,
                            federationName = federationName,
                            divisionName = federationDivisionName,
                            sectionName = federationSectionName,
                            groupName = federationGroupName,
                            position = federationsDict[user.UserId].Position,
                            idNumber = federationsDict[user.UserId].IdNumber,
                            affiliationDate = federationsDict[user.UserId].AffiliationDate?.ToString("yyyy-MM-dd") ?? ""
                        } : null,
                        associationDetails = associationsDict.ContainsKey(user.UserId) ? new
                        {
                            name = associationsDict[user.UserId].AssociationName,
                            position = associationsDict[user.UserId].Position,
                            idNumber = associationsDict[user.UserId].IdNumber
                        } : null,
                        ngoDetails = ngosDict.ContainsKey(user.UserId) ? new
                        {
                            name = ngosDict[user.UserId].NgoName,
                            position = ngosDict[user.UserId].Position,
                            idNumber = ngosDict[user.UserId].IdNumber
                        } : null,
                        isPromoted = user.IsPromoted,
                        isActive = identityUser != null && identityUser.EmailConfirmed,
                        accountType = user.AccountType ?? "عادي",
                        createdAt = user.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                        roles = string.Join(", ", roles),
                        education = user.Education ?? "",
                        specialization = user.Specialization ?? ""
                    });
                }

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        governorateName = governorate,
                        totalCount = usersInGov.Count,
                        users = usersDataList,
                        stats = stats
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"خطأ في تحميل تفاصيل محافظة {governorate}");
                return Json(new { success = false, message = "حدث خطأ في تحميل البيانات: " + ex.Message });
            }
        }

        // ===== باقي الدوال (GetMembersByAffiliation, GetManagerStatisticsByGovernorate, 
        // GetMembersByMembershipType, GetMembershipStatistics, GetAffiliationHierarchy,
        // GetFederationHierarchy, GetGenderStatistics, GetExportData, GetEducationStatistics,
        // GetEducationDetails, GetAllEducationDetails) تبقى كما هي دون تغيير =====

        // ===== دوال مساعدة =====
        // ===== جلب التسلسل الهرمي لجهة الانتساب (الأقسام ← الشعب ← التجمعات) =====
        [HttpGet]
        public async Task<IActionResult> GetAffiliationHierarchy(string affiliation)
        {
            try
            {
                if (string.IsNullOrEmpty(affiliation))
                {
                    return Json(new { success = false, message = "اسم جهة الانتساب مطلوب" });
                }

                // البحث عن جهة الانتساب
                var entity = await _context.AffiliationEntities
                    .FirstOrDefaultAsync(e => e.Name == affiliation);

                if (entity == null)
                {
                    return Json(new { success = false, message = "جهة الانتساب غير موجودة" });
                }

                var accessScope = await GetCurrentMapAccessScopeAsync();
                var allIdentifies = await _context.Identifies.ToListAsync();
                var allAddresses = await _context.Addresses.ToListAsync();
                var addressLookup = BuildAddressLookup(allAddresses);
                var allAffiliations = await _context.AffiliationInfos.ToListAsync();
                var affiliationLookup = allAffiliations
                    .Where(a => !string.IsNullOrWhiteSpace(a.UserId))
                    .GroupBy(a => a.UserId)
                    .ToDictionary(g => g.Key, g => g.First());
                var scopedIdentifies = FilterUsersByScope(
                        allIdentifies,
                        addressLookup,
                        affiliationLookup,
                        accessScope);
                var scopedUserIds = scopedIdentifies.Select(i => i.UserId).ToHashSet();
                var memberRoleUserIds = await GetMemberRoleUserIdsAsync(scopedUserIds);
                var allowedUserIds = scopedIdentifies
                    .Where(i => IsIndividualAccount(i, memberRoleUserIds))
                    .Select(i => i.UserId)
                    .ToHashSet();

                var allEntities = await _context.AffiliationEntities.ToListAsync();
                var allDivisions = await _context.Divisions.ToListAsync();
                var allSections = await _context.Sections.ToListAsync();
                var allGroups = await _context.Groups.ToListAsync();
                var allManagementAssignments = await _context.ManagementAssignments
                    .AsNoTracking()
                    .ToListAsync();
                var scopedCatalog = FilterAffiliationCatalogByScope(
                    accessScope,
                    allEntities,
                    allDivisions,
                    allSections,
                    allGroups);

                if (accessScope.Assignments.Any() && !scopedCatalog.Entities.Any(e => e.Id == entity.Id))
                {
                    return Json(new { success = true, data = new List<object>() });
                }

                // جلب جميع الأقسام التابعة لهذه الجهة
                var divisions = scopedCatalog.Divisions
                    .Where(d => d.AffiliationEntityId == entity.Id)
                    .OrderBy(d => d.Name)
                    .ToList();

                var result = new List<object>();

                int CountHierarchyUsers(
                    Func<AffiliationInfo, bool> affiliationPredicate,
                    Func<ManagementAssignment, bool> assignmentPredicate)
                {
                    return allAffiliations
                        .Where(affiliationPredicate)
                        .Select(a => a.UserId)
                        .Concat(allManagementAssignments
                            .Where(assignmentPredicate)
                            .Select(a => a.UserId))
                        .Where(allowedUserIds.Contains)
                        .Distinct()
                        .Count();
                }

                foreach (var division in divisions)
                {
                    // حساب عدد المستخدمين في هذا القسم
                    var divisionUserCount = CountHierarchyUsers(
                        a => a.DivisionId == division.Id,
                        a => a.DivisionId == division.Id);

                    // جلب جميع الشعب التابعة لهذا القسم
                    var sections = scopedCatalog.Sections
                        .Where(s => s.DivisionId == division.Id)
                        .OrderBy(s => s.Name)
                        .ToList();

                    var sectionsList = new List<object>();

                    foreach (var section in sections)
                    {
                        // حساب عدد المستخدمين في هذه الشعبة
                        var sectionUserCount = CountHierarchyUsers(
                            a => a.SectionId == section.Id,
                            a => a.SectionId == section.Id);

                        var groupsList = new List<object>();
                        var groups = scopedCatalog.Groups
                            .Where(g => g.SectionId == section.Id)
                            .OrderBy(g => g.Name)
                            .ToList();

                        foreach (var group in groups)
                        {
                            var groupUserCount = CountHierarchyUsers(
                                a => a.GroupId == group.Id,
                                a => a.GroupId == group.Id);
                            groupsList.Add(new
                            {
                                groupName = group.Name,
                                groupCount = groupUserCount
                            });
                        }

                        sectionsList.Add(new
                        {
                            sectionName = section.Name,
                            sectionCount = sectionUserCount,
                            showGroups = groupsList.Any(),
                            groups = groupsList
                        });
                    }

                    result.Add(new
                    {
                        divisionName = division.Name,
                        divisionCount = divisionUserCount,
                        sections = sectionsList
                    });
                }

                return Json(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في جلب التسلسل الهرمي لجهة الانتساب: {Affiliation}", affiliation);
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ===== جلب التسلسل الهرمي للاتحاد (الأقسام ← الشعب ← التجمعات) =====
        [HttpGet]
        public async Task<IActionResult> GetFederationHierarchy(string federation)
        {
            try
            {
                if (string.IsNullOrEmpty(federation))
                {
                    return Json(new { success = false, message = "اسم الاتحاد مطلوب" });
                }

                // البحث عن الاتحاد الرئيسي
                var federationMaster = await _context.Federations
                    .FirstOrDefaultAsync(f => f.Name == federation);

                if (federationMaster == null)
                {
                    return Json(new { success = false, message = "الاتحاد غير موجود" });
                }

                var accessScope = await GetCurrentMapAccessScopeAsync();
                var allIdentifies = await _context.Identifies.ToListAsync();
                var allAddresses = await _context.Addresses.ToListAsync();
                var addressLookup = BuildAddressLookup(allAddresses);
                var allAffiliations = await _context.AffiliationInfos.ToListAsync();
                var affiliationLookup = allAffiliations
                    .Where(a => !string.IsNullOrWhiteSpace(a.UserId))
                    .GroupBy(a => a.UserId)
                    .ToDictionary(g => g.Key, g => g.First());
                var scopedIdentifies = FilterUsersByScope(
                        allIdentifies,
                        addressLookup,
                        affiliationLookup,
                        accessScope);
                var scopedUserIds = scopedIdentifies.Select(i => i.UserId).ToHashSet();
                var memberRoleUserIds = await GetMemberRoleUserIdsAsync(scopedUserIds);
                var allowedUserIds = scopedIdentifies
                    .Where(i => IsIndividualAccount(i, memberRoleUserIds))
                    .Select(i => i.UserId)
                    .ToHashSet();
                var federationMemberships = await _context.FederationMemberships.ToListAsync();

                // جلب جميع أقسام الاتحاد
                var divisions = await _context.FederationDivisions
                    .Where(d => d.FederationId == federationMaster.Id)
                    .ToListAsync();

                var result = new List<object>();

                foreach (var division in divisions)
                {
                    // حساب عدد المستخدمين في هذا القسم
                    var divisionUserCount = federationMemberships
                        .Count(f => f.FederationDivisionId == division.Id && allowedUserIds.Contains(f.UserId));

                    // جلب جميع شعب هذا القسم
                    var sections = await _context.FederationSections
                        .Where(s => s.FederationDivisionId == division.Id)
                        .ToListAsync();

                    var sectionsList = new List<object>();

                    foreach (var section in sections)
                    {
                        // حساب عدد المستخدمين في هذه الشعبة
                        var sectionUserCount = federationMemberships
                            .Count(f => f.FederationSectionId == section.Id && allowedUserIds.Contains(f.UserId));

                        // جلب جميع تجمعات هذه الشعبة
                        var groups = await _context.FederationGroups
                            .Where(g => g.FederationSectionId == section.Id)
                            .ToListAsync();

                        var groupsList = new List<object>();
                        foreach (var group in groups)
                        {
                            var groupUserCount = federationMemberships
                                .Count(f => f.FederationGroupId == group.Id && allowedUserIds.Contains(f.UserId));
                            groupsList.Add(new
                            {
                                groupName = group.Name,
                                groupCount = groupUserCount
                            });
                        }

                        sectionsList.Add(new
                        {
                            sectionName = section.Name,
                            sectionCount = sectionUserCount,
                            groups = groupsList
                        });
                    }

                    result.Add(new
                    {
                        divisionName = division.Name,
                        divisionCount = divisionUserCount,
                        sections = sectionsList
                    });
                }

                return Json(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في جلب التسلسل الهرمي للاتحاد: {Federation}", federation);
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ===== جلب إحصائيات المسؤولين حسب المحافظة =====
        [HttpGet]
        public async Task<IActionResult> GetManagerStatisticsByGovernorate(string governorate)
        {
            try
            {
                if (string.IsNullOrEmpty(governorate))
                {
                    return Json(new { success = false, message = "اسم المحافظة مطلوب" });
                }

                var accessScope = await GetCurrentMapAccessScopeAsync();
                bool isSuperAdmin = accessScope.IsSuperAdmin;
                var allowedGovernorates = GetGovernoratesForScope(accessScope);

                if (!isSuperAdmin && !allowedGovernorates.Any(g => IsGovernorateInManagedScope(governorate, g)))
                {
                    return Json(new { success = false, message = "غير مصرح لك بعرض بيانات هذه المحافظة" });
                }

                var allAddresses = await _context.Addresses.ToListAsync();
                var allIdentifies = await _context.Identifies.ToListAsync();
                var addressLookup = BuildAddressLookup(allAddresses);
                var allManagementAssignments = await _context.ManagementAssignments.ToListAsync();
                var allAffiliations = await _context.AffiliationInfos.ToListAsync();
                var affiliationLookup = allAffiliations
                    .Where(a => !string.IsNullOrWhiteSpace(a.UserId))
                    .GroupBy(a => a.UserId)
                    .ToDictionary(g => g.Key, g => g.First());

                var filteredIdentifies = FilterUsersByScope(
                    allIdentifies,
                    addressLookup,
                    affiliationLookup,
                    accessScope,
                    exactGovernorate: governorate);

                var allowedUserIds = filteredIdentifies.Select(i => i.UserId).ToHashSet();

                var filteredAssignments = allManagementAssignments
                    .Where(a => allowedUserIds.Contains(a.UserId))
                    .ToList();

                var entityManagers = filteredAssignments.Count(a => a.ManagementLevel == "Entity" && a.AssignmentRole == "Manager");
                var divisionManagers = filteredAssignments.Count(a => a.ManagementLevel == "Division" && a.AssignmentRole == "Manager");
                var sectionManagers = filteredAssignments.Count(a => a.ManagementLevel == "Section" && a.AssignmentRole == "Manager");
                var groupManagers = filteredAssignments.Count(a => a.ManagementLevel == "Group" && a.AssignmentRole == "Manager");

                var entityAssistants = filteredAssignments.Count(a => a.ManagementLevel == "Entity" && a.AssignmentRole == "Assistant");
                var divisionAssistants = filteredAssignments.Count(a => a.ManagementLevel == "Division" && a.AssignmentRole == "Assistant");
                var sectionAssistants = filteredAssignments.Count(a => a.ManagementLevel == "Section" && a.AssignmentRole == "Assistant");
                var groupAssistants = filteredAssignments.Count(a => a.ManagementLevel == "Group" && a.AssignmentRole == "Assistant");

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        entityManagers = entityManagers,
                        divisionManagers = divisionManagers,
                        sectionManagers = sectionManagers,
                        groupManagers = groupManagers,
                        entityAssistants = entityAssistants,
                        divisionAssistants = divisionAssistants,
                        sectionAssistants = sectionAssistants,
                        groupAssistants = groupAssistants
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في جلب إحصائيات المسؤولين للمحافظة: {Governorate}", governorate);
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ===== جلب إحصائيات أنواع العضويات (النقابات، الاتحادات، الجمعيات، المنظمات) =====
        // ===== جلب إحصائيات أنواع العضويات (النقابات، الاتحادات، الجمعيات، المنظمات) =====
        [HttpGet]
        public async Task<IActionResult> GetMembershipStatistics()
        {
            try
            {
                var accessScope = await GetCurrentMapAccessScopeAsync();

                var allAddresses = await _context.Addresses.ToListAsync();
                var allIdentifies = await _context.Identifies.ToListAsync();
                var addressLookup = BuildAddressLookup(allAddresses);
                var allAffiliations = await _context.AffiliationInfos.ToListAsync();
                var affiliationLookup = allAffiliations
                    .Where(a => !string.IsNullOrWhiteSpace(a.UserId))
                    .GroupBy(a => a.UserId)
                    .ToDictionary(g => g.Key, g => g.First());

                var filteredIdentifies = FilterUsersByScope(
                    allIdentifies,
                    addressLookup,
                    affiliationLookup,
                    accessScope);

                var allowedUserIds = filteredIdentifies.Select(i => i.UserId).ToList();
                var memberRoleUserIds = await GetMemberRoleUserIdsAsync(allowedUserIds);
                var promotedUserIds = allIdentifies
                    .Where(i => IsIndividualAccount(i, memberRoleUserIds) && allowedUserIds.Contains(i.UserId))
                    .Select(i => i.UserId)
                    .ToHashSet();

                // إحصائيات النقابات
                var unions = await _context.UnionMemberships.ToListAsync();
                var unionsMaster = await _context.Unions.ToListAsync();
                var unionsDetails = unionsMaster.Select(m => new
                {
                    name = m.Name,
                    memberCount = unions.Count(u => u.UnionName == m.Name && promotedUserIds.Contains(u.UserId))
                }).OrderByDescending(x => x.memberCount).ToList();

                // إحصائيات الاتحادات
                var federations = await _context.FederationMemberships
                    .Include(f => f.Federation)
                    .ToListAsync();
                var federationsMaster = await _context.Federations.ToListAsync();
                var federationsDetails = federationsMaster.Select(m => new
                {
                    name = m.Name,
                    memberCount = federations.Count(f => f.Federation != null && f.Federation.Name == m.Name && promotedUserIds.Contains(f.UserId))
                }).OrderByDescending(x => x.memberCount).ToList();

                // إحصائيات الجمعيات
                var associations = await _context.AssociationMemberships.ToListAsync();
                var associationsMaster = await _context.Associations.ToListAsync();
                var associationsDetails = associationsMaster.Select(m => new
                {
                    name = m.Name,
                    memberCount = associations.Count(a => a.AssociationName == m.Name && promotedUserIds.Contains(a.UserId))
                }).OrderByDescending(x => x.memberCount).ToList();

                // إحصائيات المنظمات
                var ngos = await _context.NgoMemberships.ToListAsync();
                var ngosMaster = await _context.Ngos.ToListAsync();
                var ngosDetails = ngosMaster.Select(m => new
                {
                    name = m.Name,
                    memberCount = ngos.Count(n => n.NgoName == m.Name && promotedUserIds.Contains(n.UserId))
                }).OrderByDescending(x => x.memberCount).ToList();

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        unions = new
                        {
                            totalMembers = unionsDetails.Sum(x => x.memberCount),
                            totalTypes = unionsMaster.Count,
                            details = unionsDetails
                        },
                        federations = new
                        {
                            totalMembers = federationsDetails.Sum(x => x.memberCount),
                            totalTypes = federationsMaster.Count,
                            details = federationsDetails
                        },
                        associations = new
                        {
                            totalMembers = associationsDetails.Sum(x => x.memberCount),
                            totalTypes = associationsMaster.Count,
                            details = associationsDetails
                        },
                        ngos = new
                        {
                            totalMembers = ngosDetails.Sum(x => x.memberCount),
                            totalTypes = ngosMaster.Count,
                            details = ngosDetails
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في جلب إحصائيات العضويات");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ===== جلب بيانات التصدير إلى Excel =====
        [HttpGet]
        [HttpGet]
        public async Task<IActionResult> GetExportData()
        {
            try
            {
                var accessScope = await GetCurrentMapAccessScopeAsync();
                var userManagedGovernorate = accessScope.ManagedGovernorate;
                var userManagedDistrict = accessScope.ManagedDistrict;
                bool isSuperAdmin = accessScope.IsSuperAdmin;

                var allAddresses = await _context.Addresses.ToListAsync();
                var allIdentifies = await _context.Identifies.ToListAsync();
                var addressLookup = BuildAddressLookup(allAddresses);
                var allUnions = await _context.UnionMemberships.ToListAsync();
                var allFederations = await _context.FederationMemberships
                    .Include(f => f.Federation)
                    .ToListAsync();
                var allAssociations = await _context.AssociationMemberships.ToListAsync();
                var allNgos = await _context.NgoMemberships.ToListAsync();
                var allAffiliations = await _context.AffiliationInfos.ToListAsync();
                var affiliationLookup = allAffiliations
                    .Where(a => !string.IsNullOrWhiteSpace(a.UserId))
                    .GroupBy(a => a.UserId)
                    .ToDictionary(g => g.Key, g => g.First());

                var filteredIdentifies = FilterUsersByScope(
                    allIdentifies,
                    addressLookup,
                    affiliationLookup,
                    accessScope);

                var allowedUserIds = filteredIdentifies
                    .Select(i => i.UserId)
                    .Distinct()
                    .ToHashSet();

                var memberRoleUserIds = await GetMemberRoleUserIdsAsync(allowedUserIds);
                var promotedUsers = allIdentifies
                    .Where(i => IsIndividualAccount(i, memberRoleUserIds) && allowedUserIds.Contains(i.UserId))
                    .ToList();

                if (IsManagerScopedOnly(accessScope))
                {
                    var promotedUserIds = promotedUsers
                        .Select(i => i.UserId)
                        .ToHashSet();

                    var managerScopeName = string.Join("، ", accessScope.Assignments
                        .Select(a => a.Governorate)
                        .Where(g => !string.IsNullOrWhiteSpace(g))
                        .Distinct());

                    if (string.IsNullOrWhiteSpace(managerScopeName))
                    {
                        managerScopeName = "نطاق المسؤول";
                    }

                    return Json(new
                    {
                        success = true,
                        data = new
                        {
                            isManagerScopedView = true,
                            scopeName = managerScopeName,
                            exportedBy = User.Identity?.Name,
                            exportDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                            summary = new
                            {
                                individualCount = promotedUsers.Count,
                                maleCount = promotedUsers.Count(u => u.Gender == "ذكر"),
                                femaleCount = promotedUsers.Count(u => u.Gender == "أنثى"),
                                affiliatedCount = allAffiliations.Count(a => promotedUserIds.Contains(a.UserId) && a.AffiliationEntityId.HasValue),
                                unionCount = allUnions
                                    .Where(u => promotedUserIds.Contains(u.UserId) && !string.IsNullOrEmpty(u.UnionName))
                                    .Select(u => u.UserId)
                                    .Distinct()
                                    .Count(),
                                federationCount = allFederations
                                    .Where(f => promotedUserIds.Contains(f.UserId) && f.FederationId.HasValue)
                                    .Select(f => f.UserId)
                                    .Distinct()
                                    .Count(),
                                associationCount = allAssociations
                                    .Where(a => promotedUserIds.Contains(a.UserId) && !string.IsNullOrEmpty(a.AssociationName))
                                    .Select(a => a.UserId)
                                    .Distinct()
                                    .Count(),
                                ngoCount = allNgos
                                    .Where(n => promotedUserIds.Contains(n.UserId) && !string.IsNullOrEmpty(n.NgoName))
                                    .Select(n => n.UserId)
                                    .Distinct()
                                    .Count()
                            }
                        }
                    });
                }

                var resultGovernorates = new List<object>();

                // إذا كان سوبر أدمن نعرض كل المحافظات، وإذا أدمن نعرض فقط المسموح له
                var governoratesToExport = GetGovernoratesForScope(accessScope);

                foreach (var gov in governoratesToExport)
                {
                    var userIdsInGov = filteredIdentifies
                        .Where(i =>
                        {
                            addressLookup.TryGetValue(i.UserId, out var address);
                            return IsGovernorateInStatisticsBucket(GetEffectiveGovernorate(i, address), gov);
                        })
                        .Select(i => i.UserId)
                        .Distinct()
                        .ToList();

                    var usersInGov = promotedUsers
                        .Where(i => userIdsInGov.Contains(i.UserId))
                        .ToList();

                    if (!usersInGov.Any())
                        continue;

                    var userIds = usersInGov
                        .Select(u => u.UserId)
                        .ToHashSet();

                    int adminCount = 0;
                    int superAdminCount = 0;
                    int districtAdminCount = 0;
                    int mapViewerCount = 0;

                    foreach (var userId in userIds)
                    {
                        var user = await _userManager.FindByIdAsync(userId);
                        if (user == null) continue;

                        var roles = await _userManager.GetRolesAsync(user);

                        if (roles.Contains("SuperAdmin")) superAdminCount++;
                        if (roles.Contains("Admin")) adminCount++;
                        if (roles.Contains("DistrictAdmin")) districtAdminCount++;
                        if (roles.Contains("MapViewer")) mapViewerCount++;
                    }

                    resultGovernorates.Add(new
                    {
                        name = gov,
                        individualCount = usersInGov.Count,
                        totalUsers = userIds.Count,
                        maleCount = usersInGov.Count(u => u.Gender == "ذكر"),
                        femaleCount = usersInGov.Count(u => u.Gender == "أنثى"),
                        adminCount = adminCount,
                        superAdminCount = superAdminCount,
                        districtAdminCount = districtAdminCount,
                        mapViewerCount = mapViewerCount,
                        affiliatedCount = allAffiliations.Count(a => userIds.Contains(a.UserId) && a.AffiliationEntityId.HasValue),
                        unionCount = allUnions
                            .Where(u => userIds.Contains(u.UserId) && !string.IsNullOrEmpty(u.UnionName))
                            .Select(u => u.UserId)
                            .Distinct()
                            .Count(),
                        federationCount = allFederations
                            .Where(f => userIds.Contains(f.UserId) && f.FederationId.HasValue)
                            .Select(f => f.UserId)
                            .Distinct()
                            .Count(),
                        associationCount = allAssociations
                            .Where(a => userIds.Contains(a.UserId) && !string.IsNullOrEmpty(a.AssociationName))
                            .Select(a => a.UserId)
                            .Distinct()
                            .Count(),
                        ngoCount = allNgos
                            .Where(n => userIds.Contains(n.UserId) && !string.IsNullOrEmpty(n.NgoName))
                            .Select(n => n.UserId)
                            .Distinct()
                            .Count()
                    });
                }

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        governorates = resultGovernorates,
                        exportedBy = User.Identity?.Name,
                        managedGovernorate = userManagedGovernorate,
                        managedDistrict = userManagedDistrict,
                        isSuperAdmin = isSuperAdmin,
                        exportDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في جلب بيانات التصدير");
                return Json(new { success = false, message = ex.Message });
            }
        }
        private List<string> GetGovernoratesList()
        {
            return new List<string>
            {
                AllGovernoratesScopeName, BaghdadGeneralScopeName, "بغداد - الكرخ", "بغداد - الرصافة",
                "الأنبار", "بابل", "البصرة", "ذي قار", "القادسية",
                "ديالى", "دهوك", "أربيل", "كربلاء", "كركوك", "ميسان",
                "المثنى", "النجف", "نينوى", "صلاح الدين", "السليمانية", "واسط"
            };
        }

        private int GetGovernorateId(string name)
        {
            return name switch
            {
                "دهوك" => 30,
                "نينوى" => 17,
                "الأنبار" => 36,
                "أربيل" => 32,
                "السليمانية" => 34,
                "كركوك" => 16,
                "صلاح الدين" => 15,
                "ديالى" => 14,
                "بغداد" => 2,
                "مركزي لكل المحافظات" => 1000,
                "بغداد عامة" => 1001,
                "بغداد مركزي" => 2,
                "بغداد - الكرخ" => 1002,
                "بغداد - الرصافة" => 1003,
                "كربلاء" => 13,
                "بابل" => 12,
                "واسط" => 11,
                "النجف" => 9,
                "القادسية" => 10,
                "المثنى" => 6,
                "ذي قار" => 7,
                "ميسان" => 8,
                "البصرة" => 4,
                _ => 0
            };
        }

        private (double, double) GetGovernorateCoordinate(string governorate)
        {
            return governorate switch
            {
                "مركزي لكل المحافظات" => (44.0, 33.0),
                "بغداد عامة" => (44.3661, 33.3152),
                "بغداد" => (44.3661, 33.3152),
                "بغداد مركزي" => (44.3661, 33.3152),
                "بغداد - الكرخ" => (44.2600, 33.3152),
                "بغداد - الرصافة" => (44.4700, 33.3152),
                "الأنبار" => (41.0, 33.0),
                "بابل" => (44.5, 32.5),
                "البصرة" => (47.8, 30.5),
                "ذي قار" => (46.0, 31.0),
                "القادسية" => (45.0, 32.0),
                "ديالى" => (45.0, 33.5),
                "دهوك" => (43.0, 37.0),
                "أربيل" => (44.0, 36.0),
                "كربلاء" => (44.0, 32.5),
                "كركوك" => (44.0, 35.5),
                "ميسان" => (47.0, 31.5),
                "المثنى" => (45.0, 30.0),
                "النجف" => (44.0, 32.0),
                "نينوى" => (43.0, 36.0),
                "صلاح الدين" => (44.0, 34.5),
                "السليمانية" => (45.0, 35.5),
                "واسط" => (45.5, 32.5),
                _ => (44.0, 33.0)
            };
        }

        // ===== دوال مساعدة للأيقونات والألوان حسب المؤهل =====
        private string GetEducationIcon(string education)
        {
            return education switch
            {
                "بكالوريوس" => "fas fa-graduation-cap",
                "ماجستير" => "fas fa-university",
                "دكتوراه" => "fas fa-microscope",
                "طالب جامعي" => "fas fa-book-open",
                "دبلوم" => "fas fa-certificate",
                "متوسط" => "fas fa-school",
                "إعدادي" => "fas fa-chalkboard",
                "ابتدائي" => "fas fa-pencil-alt",
                "آمي" => "fas fa-font",
                _ => "fas fa-user-graduate"
            };
        }

        private string GetEducationColor(string education)
        {
            return education switch
            {
                "بكالوريوس" => "#3498db",
                "ماجستير" => "#9b59b6",
                "دكتوراه" => "#e74c3c",
                "طالب جامعي" => "#2ecc71",
                "دبلوم" => "#f39c12",
                "متوسط" => "#1abc9c",
                "إعدادي" => "#e67e22",
                "ابتدائي" => "#27ae60",
                "آمي" => "#95a5a6",
                _ => "#7f8c8d"
            };
        }

        // ===== جلب إحصائيات المؤهل العلمي =====
        [HttpGet]
        public async Task<IActionResult> GetEducationStatistics()
        {
            try
            {
                var accessScope = await GetCurrentMapAccessScopeAsync();
                var allIdentifies = await _context.Identifies.ToListAsync();
                var allAddresses = await _context.Addresses.ToListAsync();
                var addressLookup = BuildAddressLookup(allAddresses);
                var allAffiliations = await _context.AffiliationInfos.ToListAsync();
                var affiliationLookup = allAffiliations
                    .Where(a => !string.IsNullOrWhiteSpace(a.UserId))
                    .GroupBy(a => a.UserId)
                    .ToDictionary(g => g.Key, g => g.First());

                var filteredIdentifies = FilterUsersByScope(
                    allIdentifies,
                    addressLookup,
                    affiliationLookup,
                    accessScope);

                var allowedUserIds = filteredIdentifies.Select(i => i.UserId).ToList();
                var memberRoleUserIds = await GetMemberRoleUserIdsAsync(allowedUserIds);

                var promotedUsers = allIdentifies
                    .Where(i => IsIndividualAccount(i, memberRoleUserIds) && allowedUserIds.Contains(i.UserId) && !string.IsNullOrEmpty(i.Education))
                    .ToList();

                var educationStats = promotedUsers
                    .GroupBy(i => i.Education)
                    .Select(g => new EducationStat
                    {
                        EducationName = g.Key,
                        Count = g.Count(),
                        Icon = GetEducationIcon(g.Key),
                        Color = GetEducationColor(g.Key)
                    })
                    .OrderByDescending(x => x.Count)
                    .ToList();

                return Json(new { success = true, data = educationStats, total = promotedUsers.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في جلب إحصائيات المؤهل العلمي");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ===== جلب تفاصيل المستخدمين حسب المؤهل العلمي =====
        [HttpGet]
        public async Task<IActionResult> GetEducationDetails(string education)
        {
            try
            {
                if (string.IsNullOrEmpty(education))
                {
                    return Json(new { success = false, message = "اسم المؤهل مطلوب" });
                }

                var accessScope = await GetCurrentMapAccessScopeAsync();
                var allIdentifies = await _context.Identifies.ToListAsync();
                var allUsers = _userManager.Users.ToList();
                var allAddresses = await _context.Addresses.ToListAsync();
                var addressLookup = BuildAddressLookup(allAddresses);
                var allAffiliations = await _context.AffiliationInfos.ToListAsync();
                var affiliationLookup = allAffiliations
                    .Where(a => !string.IsNullOrWhiteSpace(a.UserId))
                    .GroupBy(a => a.UserId)
                    .ToDictionary(g => g.Key, g => g.First());

                var filteredIdentifies = FilterUsersByScope(
                    allIdentifies,
                    addressLookup,
                    affiliationLookup,
                    accessScope);

                var allowedUserIds = filteredIdentifies.Select(i => i.UserId).ToList();
                var memberRoleUserIds = await GetMemberRoleUserIdsAsync(allowedUserIds);

                var usersInEducation = allIdentifies
                    .Where(i => i.Education == education && IsIndividualAccount(i, memberRoleUserIds) && allowedUserIds.Contains(i.UserId))
                    .ToList();

                var usersList = new List<EducationUserDetail>();
                foreach (var user in usersInEducation)
                {
                    var identityUser = allUsers.FirstOrDefault(u => u.Id == user.UserId);
                    addressLookup.TryGetValue(user.UserId, out var address);

                    usersList.Add(new EducationUserDetail
                    {
                        UserId = user.UserId,
                        FullName = user.FullName ?? "غير محدد",
                        Email = identityUser?.Email ?? "غير محدد",
                        Gender = user.Gender ?? "غير محدد",
                        PhoneNumber = user.PhoneNumber ?? "",
                        Governorate = GetEffectiveGovernorate(user, address),
                        Specialization = user.Specialization ?? "",
                        DateOfBirth = user.Date > DateTime.MinValue ? user.Date : null,
                        IsPromoted = user.IsPromoted,
                        Education = user.Education
                    });
                }

                return Json(new
                {
                    success = true,
                    data = usersList,
                    education = education,
                    count = usersList.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في جلب تفاصيل المؤهل: {Education}", education);
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ===== جلب جميع تفاصيل المستخدمين حسب المؤهلات =====
        [HttpGet]
        public async Task<IActionResult> GetAllEducationDetails()
        {
            try
            {
                var accessScope = await GetCurrentMapAccessScopeAsync();
                var allIdentifies = await _context.Identifies.ToListAsync();
                var allUsers = _userManager.Users.ToList();
                var allAddresses = await _context.Addresses.ToListAsync();
                var addressLookup = BuildAddressLookup(allAddresses);
                var allAffiliations = await _context.AffiliationInfos.ToListAsync();
                var affiliationLookup = allAffiliations
                    .Where(a => !string.IsNullOrWhiteSpace(a.UserId))
                    .GroupBy(a => a.UserId)
                    .ToDictionary(g => g.Key, g => g.First());

                var filteredIdentifies = FilterUsersByScope(
                    allIdentifies,
                    addressLookup,
                    affiliationLookup,
                    accessScope);

                var allowedUserIds = filteredIdentifies.Select(i => i.UserId).ToList();
                var memberRoleUserIds = await GetMemberRoleUserIdsAsync(allowedUserIds);

                var promotedUsers = allIdentifies
                    .Where(i => IsIndividualAccount(i, memberRoleUserIds) && allowedUserIds.Contains(i.UserId) && !string.IsNullOrEmpty(i.Education))
                    .ToList();

                var educationStats = promotedUsers
                    .GroupBy(i => i.Education)
                    .Select(g => new { Education = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .ToList();

                var usersList = new List<object>();
                foreach (var user in promotedUsers)
                {
                    var identityUser = allUsers.FirstOrDefault(u => u.Id == user.UserId);
                    addressLookup.TryGetValue(user.UserId, out var address);

                    usersList.Add(new
                    {
                        userId = user.UserId,
                        fullName = user.FullName ?? "غير محدد",
                        email = identityUser?.Email ?? "غير محدد",
                        gender = user.Gender ?? "غير محدد",
                        phoneNumber = user.PhoneNumber ?? "",
                        governorate = GetEffectiveGovernorate(user, address),
                        specialization = user.Specialization ?? "",
                        dateOfBirth = user.Date > DateTime.MinValue ? user.Date.ToString("yyyy-MM-dd") : "",
                        isPromoted = user.IsPromoted,
                        education = user.Education
                    });
                }

                return Json(new
                {
                    success = true,
                    data = usersList,
                    stats = educationStats,
                    total = promotedUsers.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في جلب جميع تفاصيل المؤهلات");
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}
