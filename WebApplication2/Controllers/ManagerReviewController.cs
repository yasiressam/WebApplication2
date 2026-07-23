using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using WebApplication2.Data;
using WebApplication2.Models;
using WebApplication2.Services;

namespace WebApplication2.Controllers
{
    [Authorize(Roles = clsRoles.Manager + "," + clsRoles.AssistantManager)]
    public class ManagerReviewController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly INotificationService _notificationService;
        private readonly ILogger<ManagerReviewController> _logger;

        public ManagerReviewController(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            INotificationService notificationService,
            ILogger<ManagerReviewController> logger)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
            _logger = logger;
        }

        private async Task<Address?> GetUserAddressAsync(string userId)
        {
            return await _context.Addresses.FirstOrDefaultAsync(a => a.UserId == userId);
        }

        private async Task<VoterCard?> GetUserVoterCardAsync(string userId)
        {
            return await _context.VoterCards.FirstOrDefaultAsync(v => v.UserId == userId);
        }

        private async Task<Identify?> GetUserProfileAsync(string userId)
        {
            return await _context.Identifies
                .Include(i => i.WorkLocation)
                .FirstOrDefaultAsync(i => i.UserId == userId);
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

        private string GetArabicLevelName(string level, string role)
        {
            string levelName = level switch
            {
                "Entity" => "Ø¬Ù‡Ø©",
                "Division" => "Ù‚Ø³Ù…",
                "Section" => "Ø´Ø¹Ø¨Ø©",
                "Group" => "ÙˆØ­Ø¯Ø©",
                _ => level
            };

            string roleName = role switch
            {
                "Manager" => "Ù…Ø³Ø¤ÙˆÙ„",
                "Assistant" => "Ù…Ø³Ø§Ø¹Ø¯",
                _ => role
            };

            return $"{roleName} {levelName}";
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

            if (!string.IsNullOrWhiteSpace(workLocation?.Governorate) && workLocation.Governorate == "Ø¨ØºØ¯Ø§Ø¯")
            {
                return workLocation.District ?? string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(profile?.WorkGovernorate) && profile.WorkGovernorate == "Ø¨ØºØ¯Ø§Ø¯")
            {
                return profile.WorkDistrict ?? string.Empty;
            }

            return string.Empty;
        }

        private async Task<List<ManagementAssignment>> GetCurrentManagerAssignmentsAsync()
        {
            var currentUserId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return new List<ManagementAssignment>();
            }

            var allowedAssignmentRoles = new List<string>();
            if (User.IsInRole(clsRoles.Manager))
            {
                allowedAssignmentRoles.Add("Manager");
            }

            if (User.IsInRole(clsRoles.AssistantManager))
            {
                allowedAssignmentRoles.Add("Assistant");
            }

            if (!allowedAssignmentRoles.Any())
            {
                return new List<ManagementAssignment>();
            }

            return await _context.ManagementAssignments
                .Where(x => x.UserId == currentUserId && allowedAssignmentRoles.Contains(x.AssignmentRole))
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
        }

        private static bool IsCentralAssignment(ManagementAssignment assignment)
        {
            return string.IsNullOrWhiteSpace(assignment.Governorate) ||
                   assignment.Governorate == "Ù…Ø±ÙƒØ²ÙŠ" ||
                   assignment.Governorate == "ÙƒÙ„ Ø§Ù„Ù…Ø­Ø§ÙØ¸Ø§Øª";
        }

        private static bool IsGovernorateInManagedScope(string? governorate, string? managedGovernorate)
        {
            if (string.IsNullOrWhiteSpace(governorate) || string.IsNullOrWhiteSpace(managedGovernorate))
                return false;

            var current = governorate.Trim();
            var managed = managedGovernorate.Trim();

            if (string.Equals(current, managed, StringComparison.OrdinalIgnoreCase))
                return true;

            return managed == "Ø¨ØºØ¯Ø§Ø¯ Ù…Ø±ÙƒØ²ÙŠ" &&
                   (current == "Ø¨ØºØ¯Ø§Ø¯" || current.StartsWith("Ø¨ØºØ¯Ø§Ø¯ -", StringComparison.OrdinalIgnoreCase));
        }

        private static bool ShouldHidePromotionRequestsForAssignment(ManagementAssignment assignment)
        {
            return IsCentralAssignment(assignment) ||
                   (assignment.Governorate == "Ø¨ØºØ¯Ø§Ø¯" &&
                    (string.IsNullOrWhiteSpace(assignment.BaghdadScope) || assignment.BaghdadScope == "Ù…Ø±ÙƒØ²ÙŠ"));
        }

        private static string NormalizeBaghdadScope(string? district)
        {
            return string.IsNullOrWhiteSpace(district) ? "Ù…Ø±ÙƒØ²ÙŠ" : district;
        }

        private static bool MatchesBaghdadScope(ManagementAssignment assignment, string governorate, string district)
        {
            if (governorate != "Ø¨ØºØ¯Ø§Ø¯" || IsCentralAssignment(assignment))
            {
                return true;
            }

            var assignmentScope = NormalizeBaghdadScope(assignment.BaghdadScope);
            return assignmentScope == "Ù…Ø±ÙƒØ²ÙŠ" || assignmentScope == NormalizeBaghdadScope(district);
        }

        private bool MatchesAssignmentScope(ManagementAssignment assignment, string governorate, string district, AffiliationInfo? affiliationInfo)
        {
            if (!IsCentralAssignment(assignment) &&
                (string.IsNullOrWhiteSpace(governorate) || !IsGovernorateInManagedScope(governorate, assignment.Governorate)))
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

        private static bool MatchesCentralAssignmentUnit(ManagementAssignment centralAssignment, ManagementAssignment assignment)
        {
            if (IsCentralAssignment(assignment))
                return false;

            if (!string.Equals(centralAssignment.ManagementLevel, assignment.ManagementLevel, StringComparison.OrdinalIgnoreCase))
                return false;

            if (centralAssignment.AffiliationEntityId != assignment.AffiliationEntityId)
                return false;

            return centralAssignment.ManagementLevel switch
            {
                "Entity" => true,
                "Division" => centralAssignment.DivisionId == assignment.DivisionId,
                "Section" => centralAssignment.DivisionId == assignment.DivisionId &&
                             centralAssignment.SectionId == assignment.SectionId,
                "Group" => centralAssignment.DivisionId == assignment.DivisionId &&
                           centralAssignment.SectionId == assignment.SectionId &&
                           centralAssignment.GroupId == assignment.GroupId,
                _ => false
            };
        }

        private async Task<string> GetManagedUnitNameAsync(ManagementAssignment assignment)
        {
            return assignment.ManagementLevel switch
            {
                "Entity" => await GetAffiliationEntityNameAsync(assignment.AffiliationEntityId) ?? "",
                "Division" => await GetDivisionNameAsync(assignment.DivisionId) ?? "",
                "Section" => await GetSectionNameAsync(assignment.SectionId) ?? "",
                "Group" => await GetGroupNameAsync(assignment.GroupId) ?? "",
                _ => ""
            };
        }

        private async Task<bool> CanCurrentManagerReviewUserAsync(Identify identify, bool allowGovernorateOnlyWhenNoAffiliation = false)
        {
            var assignments = await GetCurrentManagerAssignmentsAsync();
            if (!assignments.Any())
            {
                return false;
            }

            var address = await GetUserAddressAsync(identify.UserId);
            var governorate = GetEffectiveGovernorate(identify, address);
            var district = GetEffectiveDistrict(identify, address);
            if (string.IsNullOrWhiteSpace(governorate))
            {
                return false;
            }

            var affiliationInfo = await _context.AffiliationInfos
                .FirstOrDefaultAsync(a => a.UserId == identify.UserId);

            if (allowGovernorateOnlyWhenNoAffiliation && affiliationInfo == null)
            {
                return assignments.Any(a =>
                    (IsCentralAssignment(a) || IsGovernorateInManagedScope(governorate, a.Governorate)) &&
                    MatchesBaghdadScope(a, governorate, district));
            }

            return assignments.Any(a => MatchesAssignmentScope(a, governorate, district, affiliationInfo));
        }

        private async Task<List<Identify>> FilterUsersByManagerScopeAsync(IEnumerable<Identify> identifies, bool allowGovernorateOnlyWhenNoAffiliation = false)
        {
            var result = new List<Identify>();

            foreach (var identify in identifies)
            {
                if (await CanCurrentManagerReviewUserAsync(identify, allowGovernorateOnlyWhenNoAffiliation))
                {
                    result.Add(identify);
                }
            }

            return result;
        }

        private IQueryable<Identify> BuildManagerScopedIdentifyQuery(
            IQueryable<Identify> source,
            IReadOnlyCollection<ManagementAssignment> assignments,
            bool allowGovernorateOnlyWhenNoAffiliation = false)
        {
            if (assignments == null || assignments.Count == 0)
            {
                return source.Where(i => false);
            }

            var scopedQuery = source.Where(i => false);

            foreach (var assignment in assignments)
            {
                var assignmentQuery = ApplyAssignmentLocationScope(source, assignment);

                if (allowGovernorateOnlyWhenNoAffiliation)
                {
                    scopedQuery = scopedQuery.Union(
                        assignmentQuery.Where(i => !_context.AffiliationInfos.Any(a => a.UserId == i.UserId)));
                }

                scopedQuery = scopedQuery.Union(ApplyAssignmentAffiliationScope(assignmentQuery, assignment));
            }

            return scopedQuery;
        }

        private IQueryable<Identify> ApplyAssignmentLocationScope(IQueryable<Identify> query, ManagementAssignment assignment)
        {
            if (IsCentralAssignment(assignment))
            {
                return query;
            }

            var managedGovernorate = assignment.Governorate?.Trim() ?? string.Empty;
            var baghdadScope = NormalizeBaghdadScope(assignment.BaghdadScope);

            query = query.Where(i =>
                (
                    !string.IsNullOrEmpty(i.WorkLocation != null ? i.WorkLocation.Governorate : null)
                        ? i.WorkLocation!.Governorate!
                        : (i.WorkGovernorate ?? string.Empty)
                ) == managedGovernorate ||
                (
                    managedGovernorate == "بغداد مركزي" &&
                    (
                        (
                            !string.IsNullOrEmpty(i.WorkLocation != null ? i.WorkLocation.Governorate : null)
                                ? i.WorkLocation!.Governorate!
                                : (i.WorkGovernorate ?? string.Empty)
                        ) == "بغداد" ||
                        (
                            !string.IsNullOrEmpty(i.WorkLocation != null ? i.WorkLocation.Governorate : null)
                                ? i.WorkLocation!.Governorate!
                                : (i.WorkGovernorate ?? string.Empty)
                        ).StartsWith("بغداد -")
                    )
                ));

            if (assignment.Governorate == "بغداد" && baghdadScope != "مركزي")
            {
                query = query.Where(i =>
                    (
                        !string.IsNullOrEmpty(i.WorkLocation != null ? i.WorkLocation.Governorate : null)
                            ? i.WorkLocation!.Governorate!
                            : (i.WorkGovernorate ?? string.Empty)
                    ) == "بغداد" &&
                    (
                        !string.IsNullOrEmpty(i.WorkLocation != null ? i.WorkLocation.Governorate : null) &&
                        i.WorkLocation!.Governorate == "بغداد"
                            ? (i.WorkLocation.District ?? string.Empty)
                            : (
                                i.WorkGovernorate == "بغداد"
                                    ? (i.WorkDistrict ?? string.Empty)
                                    : string.Empty
                              )
                    ) == baghdadScope);
            }

            return query;
        }

        private IQueryable<Identify> ApplyAssignmentAffiliationScope(IQueryable<Identify> query, ManagementAssignment assignment)
        {
            if (!assignment.AffiliationEntityId.HasValue)
            {
                return query.Where(i => false);
            }

            var entityId = assignment.AffiliationEntityId.Value;
            var divisionId = assignment.DivisionId;
            var sectionId = assignment.SectionId;
            var groupId = assignment.GroupId;

            return assignment.ManagementLevel switch
            {
                "Entity" => query.Where(i => _context.AffiliationInfos.Any(a =>
                    a.UserId == i.UserId &&
                    a.AffiliationEntityId == entityId)),

                "Division" => !divisionId.HasValue
                    ? query.Where(i => false)
                    : query.Where(i => _context.AffiliationInfos.Any(a =>
                        a.UserId == i.UserId &&
                        a.AffiliationEntityId == entityId &&
                        a.DivisionId == divisionId.Value)),

                "Section" => !divisionId.HasValue || !sectionId.HasValue
                    ? query.Where(i => false)
                    : query.Where(i => _context.AffiliationInfos.Any(a =>
                        a.UserId == i.UserId &&
                        a.AffiliationEntityId == entityId &&
                        a.DivisionId == divisionId.Value &&
                        a.SectionId == sectionId.Value)),

                "Group" => !divisionId.HasValue || !sectionId.HasValue || !groupId.HasValue
                    ? query.Where(i => false)
                    : query.Where(i => _context.AffiliationInfos.Any(a =>
                        a.UserId == i.UserId &&
                        a.AffiliationEntityId == entityId &&
                        a.DivisionId == divisionId.Value &&
                        a.SectionId == sectionId.Value &&
                        a.GroupId == groupId.Value)),

                _ => query.Where(i => false)
            };
        }

        private async Task<List<Identify>> GetScopedManagerIdentifiesAsync()
        {
            var assignments = await GetCurrentManagerAssignmentsAsync();
            if (!assignments.Any())
            {
                return new List<Identify>();
            }

            return await BuildManagerScopedIdentifyQuery(
                    _context.Identifies
                        .AsNoTracking()
                        .Include(i => i.WorkLocation),
                    assignments)
                .OrderBy(i => i.FullName)
                .ToListAsync();
        }

        [HttpGet]
        [Authorize(Roles = clsRoles.Manager)]
        public async Task<IActionResult> Managers(string? search = null)
        {
            try
            {
                var currentUserId = _userManager.GetUserId(User);
                var assignments = await GetCurrentManagerAssignmentsAsync();
                var centralAssignments = assignments
                    .Where(a => a.AssignmentRole == "Manager" && IsCentralAssignment(a))
                    .ToList();

                if (!centralAssignments.Any())
                {
                    TempData["ErrorMessage"] = "Ù‡Ø°Ù‡ Ø§Ù„ØµÙØ­Ø© Ù…Ø®ØµØµØ© Ù„Ù„Ù…Ø³Ø¤ÙˆÙ„ Ø§Ù„Ù…Ø±ÙƒØ²ÙŠ Ù„ÙƒÙ„ Ø§Ù„Ù…Ø­Ø§ÙØ¸Ø§Øª.";
                    return RedirectToAction(nameof(Users));
                }

                var allAssignments = await _context.ManagementAssignments
                    .AsNoTracking()
                    .Where(a => a.UserId != currentUserId)
                    .ToListAsync();

                var matchedAssignments = allAssignments
                    .Where(a => centralAssignments.Any(c => MatchesCentralAssignmentUnit(c, a)))
                    .OrderBy(a => a.Governorate)
                    .ThenBy(a => a.ManagementLevel)
                    .ThenByDescending(a => a.AssignmentRole == "Manager")
                    .ToList();

                var managerUserIds = matchedAssignments.Select(a => a.UserId).Distinct().ToHashSet();
                var profilesByUserId = await _context.Identifies
                    .AsNoTracking()
                    .Include(i => i.WorkLocation)
                    .Where(i => managerUserIds.Contains(i.UserId))
                    .ToDictionaryAsync(i => i.UserId);

                var usersById = await _userManager.Users
                    .AsNoTracking()
                    .Where(u => managerUserIds.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id);

                var model = new List<CentralManagedManagerVM>();
                foreach (var assignment in matchedAssignments)
                {
                    profilesByUserId.TryGetValue(assignment.UserId, out var profile);
                    usersById.TryGetValue(assignment.UserId, out var user);

                    var unitName = await GetManagedUnitNameAsync(assignment);
                    var centralAssignment = centralAssignments.First(c => MatchesCentralAssignmentUnit(c, assignment));
                    var centralUnitName = await GetManagedUnitNameAsync(centralAssignment);

                    model.Add(new CentralManagedManagerVM
                    {
                        UserId = assignment.UserId,
                        FullName = profile?.FullName ?? user?.Email ?? "ØºÙŠØ± Ù…ÙƒØªÙ…Ù„",
                        Email = user?.Email ?? profile?.Email ?? "",
                        PhoneNumber = profile?.PhoneNumber ?? "",
                        Governorate = assignment.Governorate,
                        AssignmentRole = assignment.AssignmentRole,
                        AssignmentRoleArabic = assignment.AssignmentRole == "Assistant" ? "Ù…Ø¹Ø§ÙˆÙ†" : "Ù…Ø³Ø¤ÙˆÙ„",
                        ManagementLevel = assignment.ManagementLevel,
                        ManagementLevelArabic = assignment.ManagementLevel switch
                        {
                            "Entity" => "Ø¬Ù‡Ø©",
                            "Division" => "Ù‚Ø³Ù…",
                            "Section" => "Ø´Ø¹Ø¨Ø©",
                            "Group" => "ÙˆØ­Ø¯Ø©",
                            _ => assignment.ManagementLevel
                        },
                        ManagedUnitName = unitName,
                        CentralScopeName = centralUnitName,
                        IsActive = user?.EmailConfirmed ?? false,
                        CreatedAt = assignment.CreatedAt
                    });
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var normalizedSearch = search.Trim().ToLowerInvariant();
                    model = model
                        .Where(m =>
                            (m.FullName ?? "").ToLowerInvariant().Contains(normalizedSearch) ||
                            (m.Email ?? "").ToLowerInvariant().Contains(normalizedSearch) ||
                            (m.PhoneNumber ?? "").ToLowerInvariant().Contains(normalizedSearch) ||
                            (m.Governorate ?? "").ToLowerInvariant().Contains(normalizedSearch) ||
                            (m.ManagedUnitName ?? "").ToLowerInvariant().Contains(normalizedSearch))
                        .ToList();
                }

                ViewBag.Search = search;
                ViewBag.CentralScopes = string.Join("ØŒ ", centralAssignments.Select(a => GetArabicLevelName(a.ManagementLevel, a.AssignmentRole)).Distinct());
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ø®Ø·Ø£ ÙÙŠ Ø¹Ø±Ø¶ Ù…Ø³Ø¤ÙˆÙ„ÙŠ Ù†Ø·Ø§Ù‚ Ø§Ù„Ù…Ø³Ø¤ÙˆÙ„ Ø§Ù„Ù…Ø±ÙƒØ²ÙŠ");
                TempData["ErrorMessage"] = "Ø­Ø¯Ø« Ø®Ø·Ø£ ÙÙŠ ØªØ­Ù…ÙŠÙ„ Ø§Ù„Ù…Ø³Ø¤ÙˆÙ„ÙŠÙ†.";
                return RedirectToAction(nameof(Users));
            }
        }

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

        [HttpGet]
        public async Task<IActionResult> Users(int page = 1, string? search = null, string? status = null)
        {
            try
            {
                const int pageSize = 10;
                page = Math.Max(1, page);

                var assignments = await GetCurrentManagerAssignmentsAsync();
                if (!assignments.Any())
                {
                    TempData["ErrorMessage"] = "Ù„Ø§ ØªÙˆØ¬Ø¯ Ù…Ø³Ø¤ÙˆÙ„ÙŠØ© Ø¥Ø¯Ø§Ø±ÙŠØ© ÙØ¹Ø§Ù„Ø© Ù…Ø±ØªØ¨Ø·Ø© Ø¨Ø­Ø³Ø§Ø¨Ùƒ.";
                    ViewBag.CurrentPage = 1;
                    ViewBag.TotalPages = 1;
                    ViewBag.TotalUsers = 0;
                    ViewBag.FilteredUsers = 0;
                    ViewBag.ActiveUsers = 0;
                    ViewBag.PromotedUsers = 0;
                    ViewBag.ManagedScope = "";
                    ViewBag.Search = search;
                    ViewBag.StatusFilter = status;
                    return View(new List<ManagerScopedUserVM>());
                }

                var scopedIdentifies = await BuildManagerScopedIdentifyQuery(
                        _context.Identifies
                            .AsNoTracking()
                            .Include(i => i.WorkLocation),
                        assignments)
                    .OrderBy(i => i.FullName)
                    .ToListAsync();
                var scopedUserIds = scopedIdentifies.Select(i => i.UserId).ToHashSet();

                var usersById = await _userManager.Users
                    .AsNoTracking()
                    .Where(u => scopedUserIds.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id);

                var addressesByUserId = await _context.Addresses
                    .AsNoTracking()
                    .Where(a => scopedUserIds.Contains(a.UserId))
                    .GroupBy(a => a.UserId)
                    .ToDictionaryAsync(g => g.Key, g => g.First());

                var voterCardsByUserId = await _context.VoterCards
                    .AsNoTracking()
                    .Where(v => scopedUserIds.Contains(v.UserId))
                    .GroupBy(v => v.UserId)
                    .ToDictionaryAsync(g => g.Key, g => g.First());

                var affiliationInfosByUserId = await _context.AffiliationInfos
                    .AsNoTracking()
                    .Include(a => a.AffiliationEntity)
                    .Include(a => a.Division)
                    .Include(a => a.Section)
                    .Include(a => a.Group)
                    .Where(a => scopedUserIds.Contains(a.UserId))
                    .GroupBy(a => a.UserId)
                    .ToDictionaryAsync(g => g.Key, g => g.First());

                var assignmentsByUserId = await _context.ManagementAssignments
                    .AsNoTracking()
                    .Where(a => scopedUserIds.Contains(a.UserId))
                    .GroupBy(a => a.UserId)
                    .ToDictionaryAsync(g => g.Key, g => g.OrderByDescending(a => a.AssignmentRole == "Manager").ThenByDescending(a => a.CreatedAt).ToList());

                var totalUsers = scopedIdentifies.Count;
                var totalActiveUsers = scopedIdentifies.Count(i =>
                    usersById.TryGetValue(i.UserId, out var user) && user.EmailConfirmed);
                var totalPromotedUsers = scopedIdentifies.Count(i =>
                    !string.IsNullOrWhiteSpace(i.Education) &&
                    (i.AccountType == "ÙØ±Ø¯" || i.IsPromoted));

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var normalizedSearch = search.Trim().ToLower();
                    scopedIdentifies = scopedIdentifies
                        .Where(i =>
                        {
                            usersById.TryGetValue(i.UserId, out var user);
                            affiliationInfosByUserId.TryGetValue(i.UserId, out var affiliationInfo);
                            return (i.FullName ?? string.Empty).ToLower().Contains(normalizedSearch) ||
                                   (i.PhoneNumber ?? string.Empty).ToLower().Contains(normalizedSearch) ||
                                   (user?.Email ?? string.Empty).ToLower().Contains(normalizedSearch) ||
                                   (affiliationInfo?.BadgeNumber ?? string.Empty).ToLower().Contains(normalizedSearch);
                        })
                        .ToList();
                }

                if (status == "active")
                {
                    scopedIdentifies = scopedIdentifies
                        .Where(i => usersById.TryGetValue(i.UserId, out var user) && user.EmailConfirmed)
                        .ToList();
                }
                else if (status == "inactive")
                {
                    scopedIdentifies = scopedIdentifies
                        .Where(i => !usersById.TryGetValue(i.UserId, out var user) || !user.EmailConfirmed)
                        .ToList();
                }
                else if (status == "promoted")
                {
                    scopedIdentifies = scopedIdentifies
                        .Where(i => !string.IsNullOrWhiteSpace(i.Education) &&
                                    (i.AccountType == "ÙØ±Ø¯" || i.IsPromoted))
                        .ToList();
                }
                scopedIdentifies = scopedIdentifies
                    .OrderByDescending(i => assignmentsByUserId.ContainsKey(i.UserId))
                    .ThenByDescending(i => i.UserId == _userManager.GetUserId(User))
                    .ThenBy(i => i.FullName)
                    .ToList();

                var filteredUsers = scopedIdentifies.Count;
                var totalPages = Math.Max(1, (int)Math.Ceiling(filteredUsers / (double)pageSize));
                page = Math.Min(page, totalPages);

                var pagedIdentifies = scopedIdentifies
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var model = new List<ManagerScopedUserVM>();
                foreach (var identify in pagedIdentifies)
                {
                    usersById.TryGetValue(identify.UserId, out var user);
                    addressesByUserId.TryGetValue(identify.UserId, out var address);
                    voterCardsByUserId.TryGetValue(identify.UserId, out var voterCard);
                    affiliationInfosByUserId.TryGetValue(identify.UserId, out var affiliationInfo);
                    var primaryAssignment = assignmentsByUserId.TryGetValue(identify.UserId, out var userAssignments)
                        ? userAssignments.FirstOrDefault()
                        : null;

                    model.Add(new ManagerScopedUserVM
                    {
                        Id = identify.Id,
                        UserId = identify.UserId,
                        FullName = identify.FullName ?? "ØºÙŠØ± Ù…ÙƒØªÙ…Ù„",
                        Email = user?.Email ?? identify.Email ?? "",
                        PhoneNumber = identify.PhoneNumber ?? "",
                        Governorate = GetEffectiveGovernorate(identify, address),
                        District = GetEffectiveDistrict(identify, address),
                        AffiliationEntity = affiliationInfo?.AffiliationEntity?.Name ?? "",
                        Division = affiliationInfo?.Division?.Name ?? "",
                        Section = affiliationInfo?.Section?.Name ?? "",
                        Group = affiliationInfo?.Group?.Name ?? "",
                        BadgeNumber = affiliationInfo?.BadgeNumber ?? "",
                        AccountType = identify.AccountType ?? "Ø¹Ø§Ø¯ÙŠ",
                        IsActive = user?.EmailConfirmed ?? false,
                        IsPromoted = identify.IsPromoted,
                        RequestedPromotion = identify.RequestedPromotion,
                        IsManager = primaryAssignment != null,
                        ManagementRoleArabic = primaryAssignment?.AssignmentRole == "Assistant" ? "Ù…Ø¹Ø§ÙˆÙ†" : primaryAssignment != null ? "Ù…Ø³Ø¤ÙˆÙ„" : "",
                        ManagementLevelArabic = primaryAssignment?.ManagementLevel switch
                        {
                            "Entity" => "Ø¬Ù‡Ø©",
                            "Division" => "Ù‚Ø³Ù…",
                            "Section" => "Ø´Ø¹Ø¨Ø©",
                            "Group" => "ÙˆØ­Ø¯Ø©",
                            _ => ""
                        },
                        CompletionPercentage = CalculateCompletionPercentage(identify, address, voterCard),
                        CreatedAt = identify.CreatedAt
                    });
                }

                ViewBag.CurrentPage = page;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalPages = totalPages;
                ViewBag.TotalUsers = totalUsers;
                ViewBag.FilteredUsers = filteredUsers;
                ViewBag.ActiveUsers = totalActiveUsers;
                ViewBag.PromotedUsers = totalPromotedUsers;
                ViewBag.ManagedScope = string.Join("ØŒ ", assignments.Select(a => a.Governorate).Distinct());
                ViewBag.Search = search;
                ViewBag.StatusFilter = status;

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ø®Ø·Ø£ ÙÙŠ Ø¹Ø±Ø¶ Ù…Ø³ØªØ®Ø¯Ù…ÙŠ Ø§Ù„Ù…Ø³Ø¤ÙˆÙ„ Ø§Ù„Ø¥Ø¯Ø§Ø±ÙŠ");
                TempData["ErrorMessage"] = "Ø­Ø¯Ø« Ø®Ø·Ø£ ÙÙŠ ØªØ­Ù…ÙŠÙ„ Ø¨ÙŠØ§Ù†Ø§Øª Ø§Ù„Ù…Ø³ØªØ®Ø¯Ù…ÙŠÙ†.";
                return View(new List<ManagerScopedUserVM>());
            }
        }

        [HttpGet]
        [Authorize(Roles = clsRoles.Manager)]
        public async Task<IActionResult> ExportMembersOnlyToExcel()
        {
            try
            {
                var scopedIdentifies = await GetScopedManagerIdentifiesAsync();
                var memberUserIds = scopedIdentifies
                    .Where(i => !string.IsNullOrWhiteSpace(i.Education))
                    .Where(i => i.AccountType == "ÙØ±Ø¯" || i.IsPromoted)
                    .Select(i => i.UserId)
                    .ToHashSet();

                if (!memberUserIds.Any())
                {
                    TempData["WarningMessage"] = "Ù„Ø§ ØªÙˆØ¬Ø¯ Ø£ÙØ±Ø§Ø¯ Ø¶Ù…Ù† ØµÙ„Ø§Ø­ÙŠØ§ØªÙƒ Ù„Ù„ØªØµØ¯ÙŠØ±";
                    return RedirectToAction(nameof(Users));
                }

                var users = await _userManager.Users
                    .Where(u => memberUserIds.Contains(u.Id))
                    .OrderBy(u => u.Email)
                    .ToListAsync();

                var data = await BuildFullUsersExcelData(users);
                var fileContent = GenerateExcelFile(data);
                return File(fileContent, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Manager_Members_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ø®Ø·Ø£ ÙÙŠ ØªØµØ¯ÙŠØ± Excel Ù„Ø£ÙØ±Ø§Ø¯ Ø§Ù„Ù…Ø³Ø¤ÙˆÙ„ Ø§Ù„Ø¥Ø¯Ø§Ø±ÙŠ");
                TempData["ErrorMessage"] = $"Ø­Ø¯Ø« Ø®Ø·Ø£: {ex.Message}";
                return RedirectToAction(nameof(Users));
            }
        }

        private async Task<List<object[]>> BuildFullUsersExcelData(List<IdentityUser> users)
        {
            var data = new List<object[]>();

            data.Add(new object[]
            {
                "Ø§Ù„ØµÙˆØ±Ø© Ø§Ù„Ø´Ø®ØµÙŠØ©", "Ø§Ù„Ø§Ø³Ù… Ø§Ù„Ø±Ø¨Ø§Ø¹ÙŠ", "Ø§Ù„Ù„Ù‚Ø¨", "Ø§Ø³Ù… Ø§Ù„Ø£Ù…", "ØªØ§Ø±ÙŠØ® Ø§Ù„Ù…ÙŠÙ„Ø§Ø¯",
                "Ø§Ù„Ø¬Ù†Ø³", "Ø§Ù„Ø­Ø§Ù„Ø© Ø§Ù„Ø§Ø¬ØªÙ…Ø§Ø¹ÙŠØ©", "Ø±Ù‚Ù… Ø§Ù„Ù‡Ø§ØªÙ", "Ø§Ù„ØªØ­ØµÙŠÙ„ Ø§Ù„Ø¯Ø±Ø§Ø³ÙŠ", "Ø§Ù„Ø§Ø®ØªØµØ§Øµ",
                "Ù†ÙˆØ¹ Ø§Ù„Ø¬Ø§Ù…Ø¹Ø©", "Ù†ÙˆØ¹ Ø§Ù„Ù…Ø¤Ø³Ø³Ø©", "Ø§Ø³Ù… Ø§Ù„Ø¬Ø§Ù…Ø¹Ø©/Ø§Ù„Ù…Ø¹Ù‡Ø¯", "Ø§Ù„ÙƒÙ„ÙŠØ©/Ø§Ù„Ù‚Ø³Ù…", "Ù†ÙˆØ¹ Ø§Ù„Ø¯Ø±Ø§Ø³Ø©",
                "Ø§Ù„Ù…Ø±Ø­Ù„Ø© Ø§Ù„Ø¯Ø±Ø§Ø³ÙŠØ©", "Ù…Ø­Ø§ÙØ¸Ø© Ø§Ù„Ø¹Ù…Ù„ Ø§Ù„ØªÙ†Ø¸ÙŠÙ…ÙŠ", "Ù‚Ø¶Ø§Ø¡ Ø§Ù„Ø¹Ù…Ù„ Ø§Ù„ØªÙ†Ø¸ÙŠÙ…ÙŠ", "Ù…Ø­Ø§ÙØ¸Ø© Ø§Ù„Ø³ÙƒÙ†", "Ù‚Ø¶Ø§Ø¡ Ø§Ù„Ø³ÙƒÙ†",
                "Ø§Ù„Ù…Ù†Ø·Ù‚Ø©", "Ø§Ù„Ù…Ø­Ù„Ø©", "Ø§Ù„Ø²Ù‚Ø§Ù‚",
                "Ø§Ù„Ø¯Ø§Ø±", "Ø£Ù‚Ø±Ø¨ Ù†Ù‚Ø·Ø© Ø¯Ø§Ù„Ø©", "Ø±Ù‚Ù… Ø§Ù„Ø¨Ø·Ø§Ù‚Ø© Ø§Ù„Ù…ÙˆØ­Ø¯Ø©", "ØªØ§Ø±ÙŠØ® Ø§Ù„Ø¥ØµØ¯Ø§Ø±",
                "Ø±Ù‚Ù… Ø¨Ø·Ø§Ù‚Ø© Ø§Ù„Ù†Ø§Ø®Ø¨", "Ø±Ù‚Ù… Ù…Ø±ÙƒØ² Ø§Ù„Ø§Ù‚ØªØ±Ø§Ø¹", "Ø§Ù„Ø­Ø§Ù„Ø© Ø§Ù„ÙˆØ¸ÙŠÙÙŠØ©", "Ø¬Ù‡Ø© Ø§Ù„Ø¹Ù…Ù„",
                "Ø§Ù„ÙˆØ²Ø§Ø±Ø©", "Ø§Ù„Ø¯Ø§Ø¦Ø±Ø©", "Ø§Ù„Ù…Ù†ØµØ¨", "Ø§Ù„Ø¹Ù†ÙˆØ§Ù† Ø§Ù„ÙˆØ¸ÙŠÙÙŠ", "Ø§Ù„Ø¯Ø±Ø¬Ø© Ø§Ù„ÙˆØ¸ÙŠÙÙŠØ©",
                "Ø¬Ù‡Ø© Ø§Ù„Ø§Ù†ØªØ³Ø§Ø¨", "Ø§Ù„Ù‚Ø³Ù…", "Ø§Ù„Ø´Ø¹Ø¨Ø©", "Ø§Ù„ÙˆØ­Ø¯Ø©", "Ø§Ø³Ù… Ø§Ù„Ù…Ø²ÙƒÙŠ", "Ø±Ù‚Ù… Ù‡Ø§ØªÙ Ø§Ù„Ù…Ø²ÙƒÙŠ",
                "Ø±Ù‚Ù… Ø§Ù„Ø¨Ø§Ø¬", "ØªØ§Ø±ÙŠØ® Ø§Ù„Ø§Ù†ØªÙ…Ø§Ø¡", "Ø§Ø³Ù… Ø§Ù„Ù†Ù‚Ø§Ø¨Ø©", "Ø§Ù„Ù…Ù†ØµØ¨ ÙÙŠ Ø§Ù„Ù†Ù‚Ø§Ø¨Ø©",
                "Ø±Ù‚Ù… Ø§Ù„Ø¹Ø¶ÙˆÙŠØ© ÙÙŠ Ø§Ù„Ù†Ù‚Ø§Ø¨Ø©", "ØªØ§Ø±ÙŠØ® Ø§Ù„Ù†ÙØ§Ø°/Ø§Ù„Ø§Ù†ØªÙ‡Ø§Ø¡ Ù„Ù„Ù†Ù‚Ø§Ø¨Ø©", "Ø§Ø³Ù… Ø§Ù„Ø§ØªØ­Ø§Ø¯",
                "Ù‚Ø³Ù… Ø§Ù„Ø§ØªØ­Ø§Ø¯", "Ø´Ø¹Ø¨Ø© Ø§Ù„Ø§ØªØ­Ø§Ø¯", "ÙˆØ­Ø¯Ø© Ø§Ù„Ø§ØªØ­Ø§Ø¯", "Ø§Ù„Ù…Ù†ØµØ¨ ÙÙŠ Ø§Ù„Ø§ØªØ­Ø§Ø¯",
                "Ø±Ù‚Ù… Ø§Ù„Ø¹Ø¶ÙˆÙŠØ© ÙÙŠ Ø§Ù„Ø§ØªØ­Ø§Ø¯", "ØªØ§Ø±ÙŠØ® Ø§Ù„Ù†ÙØ§Ø°/Ø§Ù„Ø§Ù†ØªÙ‡Ø§Ø¡ Ù„Ù„Ø§ØªØ­Ø§Ø¯", "Ø§Ø³Ù… Ø§Ù„Ø¬Ù…Ø¹ÙŠØ©",
                "Ø§Ù„Ù…Ù†ØµØ¨ ÙÙŠ Ø§Ù„Ø¬Ù…Ø¹ÙŠØ©", "Ø±Ù‚Ù… Ø§Ù„Ø¹Ø¶ÙˆÙŠØ© ÙÙŠ Ø§Ù„Ø¬Ù…Ø¹ÙŠØ©", "ØªØ§Ø±ÙŠØ® Ø§Ù„Ù†ÙØ§Ø°/Ø§Ù„Ø§Ù†ØªÙ‡Ø§Ø¡ Ù„Ù„Ø¬Ù…Ø¹ÙŠØ©",
                "Ø§Ø³Ù… Ø§Ù„Ù…Ù†Ø¸Ù…Ø©", "Ø§Ù„Ù…Ù†ØµØ¨ ÙÙŠ Ø§Ù„Ù…Ù†Ø¸Ù…Ø©", "Ø±Ù‚Ù… Ø§Ù„Ø¹Ø¶ÙˆÙŠØ© ÙÙŠ Ø§Ù„Ù…Ù†Ø¸Ù…Ø©",
                "ØªØ§Ø±ÙŠØ® Ø§Ù„Ù†ÙØ§Ø°/Ø§Ù„Ø§Ù†ØªÙ‡Ø§Ø¡ Ù„Ù„Ù…Ù†Ø¸Ù…Ø©", "Ø§Ù„Ø¨Ø±ÙŠØ¯ Ø§Ù„Ø¥Ù„ÙƒØªØ±ÙˆÙ†ÙŠ", "Ø§Ù„Ø£Ø¯ÙˆØ§Ø±", "Ù†Ø´Ø·ØŸ",
                "Ù†ÙˆØ¹ Ø§Ù„Ø­Ø³Ø§Ø¨", "Ù…ØµØ¹Ø¯ØŸ", "ØªØ§Ø±ÙŠØ® Ø§Ù„ØªØµØ¹ÙŠØ¯", "Ù…ØµØ¹Ø¯ Ø¨ÙˆØ§Ø³Ø·Ø©", "Ø·Ù„Ø¨ ØªØ±Ù‚ÙŠØ©ØŸ",
                "ØªØ§Ø±ÙŠØ® Ø§Ù„Ø·Ù„Ø¨", "Ø³Ø¨Ø¨ Ø§Ù„Ø±ÙØ¶", "Ø§Ù„Ù…Ø³Ø¤ÙˆÙ„ÙŠØ§Øª Ø§Ù„Ø¥Ø¯Ø§Ø±ÙŠØ©", "Ø§Ù„Ù…Ø­Ø§ÙØ¸Ø© Ø§Ù„Ù…ÙØ¯Ø§Ø±Ø©", "Ø§Ù„Ù‚Ø¶Ø§Ø¡ Ø§Ù„Ù…ÙØ¯Ø§Ø±"
            });

            var userIds = users.Select(u => u.Id).ToList();
            var profiles = await _context.Identifies.AsNoTracking()
                .Where(i => userIds.Contains(i.UserId))
                .ToListAsync();
            var profilesByUserId = profiles.ToDictionary(i => i.UserId, i => i);
            var profileIds = profiles.Select(p => p.Id).ToList();

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

                var federationName = federation?.Federation?.Name ?? "";
                var federationDivisionName = federation?.FederationDivision?.Name ?? "";
                var federationSectionName = federation?.FederationSection?.Name ?? "";
                var federationGroupName = federation?.FederationGroup?.Name ?? "";

                var residenceGovernorate = address?.Governorate ?? "";
                var residenceDistrict = address?.District ?? "";
                var workGovernorate = workLocation?.Governorate ?? userProfile?.WorkGovernorate ?? "";
                var workDistrict = workGovernorate == "Ø¨ØºØ¯Ø§Ø¯"
                    ? workLocation?.District ?? userProfile?.WorkDistrict ?? ""
                    : "";

                var managementDisplayParts = new List<string>();
                var managedGovernorate = userProfile?.ManagedGovernorate ?? "";
                var managedDistrict = userProfile?.ManagedDistrict ?? "";
                var userAssignments = managementAssignmentsByUserId.TryGetValue(user.Id, out var assignmentList)
                    ? assignmentList
                    : new List<ManagementAssignment>();

                foreach (var assignment in userAssignments)
                {
                    var entityName = "";
                    var levelArabic = GetArabicLevelName(assignment.ManagementLevel, assignment.AssignmentRole);

                    if (assignment.ManagementLevel == "Entity" && assignment.AffiliationEntityId.HasValue)
                    {
                        var entity = await _context.AffiliationEntities.FirstOrDefaultAsync(e => e.Id == assignment.AffiliationEntityId.Value);
                        entityName = entity?.Name ?? "";
                    }
                    else if (assignment.ManagementLevel == "Division" && assignment.DivisionId.HasValue)
                    {
                        var division = await _context.Divisions.FirstOrDefaultAsync(d => d.Id == assignment.DivisionId.Value);
                        entityName = division?.Name ?? "";
                    }
                    else if (assignment.ManagementLevel == "Section" && assignment.SectionId.HasValue)
                    {
                        var section = await _context.Sections.FirstOrDefaultAsync(s => s.Id == assignment.SectionId.Value);
                        entityName = section?.Name ?? "";
                    }
                    else if (assignment.ManagementLevel == "Group" && assignment.GroupId.HasValue)
                    {
                        var group = await _context.Groups.FirstOrDefaultAsync(g => g.Id == assignment.GroupId.Value);
                        entityName = group?.Name ?? "";
                    }

                    managementDisplayParts.Add($"{levelArabic}: {entityName}");

                    if (!string.IsNullOrEmpty(assignment.Governorate) && string.IsNullOrEmpty(managedGovernorate))
                    {
                        managedGovernorate = assignment.Governorate;
                    }

                    if (!string.IsNullOrEmpty(assignment.BaghdadScope) && string.IsNullOrEmpty(managedDistrict))
                    {
                        managedDistrict = assignment.BaghdadScope;
                    }
                }

                if (managedGovernorate == "Ø¨ØºØ¯Ø§Ø¯" && string.IsNullOrWhiteSpace(managedDistrict))
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
                    userProfile?.FullName ?? "ØºÙŠØ± Ù…ÙƒØªÙ…Ù„",
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
                    workDistrict,
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
                    CleanExcelPlaceholder(userProfile?.JobGrade, "-- Ø§Ø®ØªØ± Ø§Ù„Ø¯Ø±Ø¬Ø© Ø§Ù„ÙˆØ¸ÙŠÙÙŠØ© --"),
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
                    NormalizeExcelText(user.EmailConfirmed ? "Ù†Ø´Ø·" : "ØºÙŠØ± Ù†Ø´Ø·"),
                    TranslateAccountTypeForExport(userProfile?.AccountType),
                    NormalizeExcelText(userProfile?.IsPromoted == true ? "Ù…ØµØ¹Ø¯" : "ØºÙŠØ± Ù…ØµØ¹Ø¯"),
                    userProfile?.PromotionDate?.ToString("yyyy-MM-dd") ?? "",
                    NormalizeExcelText(promotedByDisplay),
                    NormalizeExcelText(userProfile?.RequestedPromotion == true ? "Ù†Ø¹Ù…" : "Ù„Ø§"),
                    userProfile?.RequestedPromotionDate?.ToString("yyyy-MM-dd") ?? "",
                    userProfile?.RejectionReason ?? "",
                    TranslateManagementDisplayForExport(string.Join(", ", managementDisplayParts)),
                    managedGovernorate,
                    managedDistrict
                });
            }

            return data;
        }

        private byte[] GenerateExcelFile(List<object[]> data)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Users");

            for (var i = 0; i < data.Count; i++)
            {
                for (var j = 0; j < data[i].Length; j++)
                {
                    worksheet.Cell(i + 1, j + 1).Value = NormalizeExcelText(data[i][j]?.ToString());
                }
            }

            var range = worksheet.Range(1, 1, data.Count, data[0].Length);
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            var headerRange = worksheet.Range(1, 1, 1, data[0].Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
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

        [HttpGet]
        [Authorize(Roles = clsRoles.Manager)]
        public async Task<IActionResult> PendingBasicInfo(int page = 1)
        {
            try
            {
                const int pageSize = 10;
                var assignments = await GetCurrentManagerAssignmentsAsync();
                if (!assignments.Any())
                {
                    TempData["ErrorMessage"] = "âŒ Ù„Ø§ ØªÙˆØ¬Ø¯ Ù…Ø³Ø¤ÙˆÙ„ÙŠØ© Ø¥Ø¯Ø§Ø±ÙŠØ© ÙØ¹Ø§Ù„Ø© Ù…Ø±ØªØ¨Ø·Ø© Ø¨Ø­Ø³Ø§Ø¨Ùƒ.";
                    return View("~/Views/Admin/PendingBasicInfo.cshtml", new List<PromotionRequestViewModel>());
                }

                var scopedRequestsQuery = BuildManagerScopedIdentifyQuery(
                    _context.Identifies
                    .AsNoTracking()
                    .Where(i =>
                        i.IsBasicInfoApproved == false &&
                        string.IsNullOrEmpty(i.BasicInfoRejectionReason) &&
                        !string.IsNullOrEmpty(i.FullName) &&
                        !string.IsNullOrEmpty(i.IdentityCardN) &&
                        i.IdentityCardN.Length >= 12),
                    assignments,
                    allowGovernorateOnlyWhenNoAffiliation: true);

                var totalRequests = await scopedRequestsQuery.CountAsync();
                var totalPages = Math.Max(1, (int)Math.Ceiling(totalRequests / (double)pageSize));
                page = Math.Max(1, Math.Min(page, totalPages));

                var pagedRequests = await scopedRequestsQuery
                    .Include(i => i.WorkLocation)
                    .OrderByDescending(i => i.BasicInfoRequestedAt ?? i.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var userIds = pagedRequests.Select(r => r.UserId).ToList();
                var usersById = await GetUsersByIdsAsync(userIds);
                var addressesByUserId = await GetAddressesByUserIdsAsync(userIds);

                var viewModel = new List<PromotionRequestViewModel>();
                foreach (var request in pagedRequests)
                {
                    usersById.TryGetValue(request.UserId, out var user);
                    addressesByUserId.TryGetValue(request.UserId, out var userAddress);

                    viewModel.Add(new PromotionRequestViewModel
                    {
                        Id = request.Id,
                        UserId = request.UserId,
                        UserEmail = user?.Email ?? "",
                        FullName = request.FullName ?? "",
                        PhoneNumber = request.PhoneNumber ?? "",
                        Governorate = GetEffectiveGovernorate(request, userAddress),
                        District = GetEffectiveDistrict(request, userAddress),
                        IdentityCardN = request.IdentityCardN,
                        RequestDate = request.BasicInfoRequestedAt ?? request.CreatedAt,
                        AccountType = request.AccountType ?? "Ø¹Ø§Ø¯ÙŠ",
                        CoverImage = request.CoverImage,
                        HasCompleteProfile = IsProfileComplete(request, userAddress, null, null),
                        CompletionPercentage = CalculateCompletionPercentage(request, userAddress, null),
                        RejectionReason = request.BasicInfoRejectionReason
                    });
                }

                ViewBag.ManagedGovernorate = string.Join("ØŒ ", assignments.Select(a => a.Governorate).Distinct());
                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = totalPages;
                ViewBag.TotalRequests = totalRequests;
                ViewBag.PageSize = pageSize;
                return View("~/Views/Admin/PendingBasicInfo.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ø®Ø·Ø£ ÙÙŠ Ø¹Ø±Ø¶ Ø·Ù„Ø¨Ø§Øª Ø§Ù„Ù…Ø±Ø§Ø¬Ø¹Ø© Ù„Ù„Ù…Ø³Ø¤ÙˆÙ„ Ø§Ù„Ø¥Ø¯Ø§Ø±ÙŠ");
                TempData["ErrorMessage"] = "Ø­Ø¯Ø« Ø®Ø·Ø£ ÙÙŠ ØªØ­Ù…ÙŠÙ„ Ø§Ù„Ø¨ÙŠØ§Ù†Ø§Øª";
                return View("~/Views/Admin/PendingBasicInfo.cshtml", new List<PromotionRequestViewModel>());
            }
        }

        [HttpGet]
        [Authorize(Roles = clsRoles.Manager)]
        public async Task<IActionResult> PromotionRequests(int page = 1)
        {
            try
            {
                const int pageSize = 10;
                var assignments = await GetCurrentManagerAssignmentsAsync();
                if (!assignments.Any())
                {
                    TempData["ErrorMessage"] = "âŒ Ù„Ø§ ØªÙˆØ¬Ø¯ Ù…Ø³Ø¤ÙˆÙ„ÙŠØ© Ø¥Ø¯Ø§Ø±ÙŠØ© ÙØ¹Ø§Ù„Ø© Ù…Ø±ØªØ¨Ø·Ø© Ø¨Ø­Ø³Ø§Ø¨Ùƒ.";
                    return View("~/Views/Admin/PromotionRequests.cshtml", new List<PromotionRequestViewModel>());
                }

                if (assignments.Any(ShouldHidePromotionRequestsForAssignment))
                {
                    TempData["ErrorMessage"] = "Ø·Ù„Ø¨Ø§Øª Ø§Ù„ØªØ±Ù‚ÙŠØ© Ù„Ø§ ØªØ¸Ù‡Ø± Ù„Ù„Ù…Ø³Ø¤ÙˆÙ„ Ø§Ù„Ù…Ø±ÙƒØ²ÙŠ Ù„ÙƒÙ„ Ø§Ù„Ù…Ø­Ø§ÙØ¸Ø§Øª.";
                    return RedirectToAction(nameof(Users));
                }

                var scopedRequestsQuery = BuildManagerScopedIdentifyQuery(
                    _context.Identifies
                    .AsNoTracking()
                    .Where(i =>
                        i.RequestedPromotion == true &&
                        i.IsPromoted == false &&
                        string.IsNullOrEmpty(i.RejectionReason)),
                    assignments);

                var totalRequests = await scopedRequestsQuery.CountAsync();
                var totalPages = Math.Max(1, (int)Math.Ceiling(totalRequests / (double)pageSize));
                page = Math.Max(1, Math.Min(page, totalPages));
                var pagedRequests = await scopedRequestsQuery
                    .Include(i => i.WorkLocation)
                    .OrderByDescending(i => i.RequestedPromotionDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var userIds = pagedRequests.Select(r => r.UserId).ToList();
                var usersById = await GetUsersByIdsAsync(userIds);
                var addressesByUserId = await GetAddressesByUserIdsAsync(userIds);
                var voterCardsByUserId = await GetVoterCardsByUserIdsAsync(userIds);
                var affiliationsByUserId = await GetAffiliationsByUserIdsAsync(userIds);

                var viewModel = new List<PromotionRequestViewModel>();
                foreach (var request in pagedRequests)
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
                        AccountType = request.AccountType ?? "Ø¹Ø§Ø¯ÙŠ",
                        CoverImage = request.CoverImage,
                        CompletionPercentage = CalculateCompletionPercentage(request, userAddress, voterCard),
                        HasCompleteProfile = IsProfileComplete(request, userAddress, voterCard, null),
                        RejectionReason = request.RejectionReason
                    });
                }

                ViewBag.ManagedGovernorate = string.Join("ØŒ ", assignments.Select(a => a.Governorate).Distinct());
                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = totalPages;
                ViewBag.TotalRequests = totalRequests;
                ViewBag.PageSize = pageSize;
                return View("~/Views/Admin/PromotionRequests.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ø®Ø·Ø£ ÙÙŠ Ø¹Ø±Ø¶ Ø·Ù„Ø¨Ø§Øª Ø§Ù„ØªØ±Ù‚ÙŠØ© Ù„Ù„Ù…Ø³Ø¤ÙˆÙ„ Ø§Ù„Ø¥Ø¯Ø§Ø±ÙŠ");
                TempData["ErrorMessage"] = "Ø­Ø¯Ø« Ø®Ø·Ø£ ÙÙŠ ØªØ­Ù…ÙŠÙ„ Ø§Ù„Ø¨ÙŠØ§Ù†Ø§Øª";
                return View("~/Views/Admin/PromotionRequests.cshtml", new List<PromotionRequestViewModel>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> Details(string id)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    TempData["ErrorMessage"] = "Ù…Ø¹Ø±Ù Ø§Ù„Ù…Ø³ØªØ®Ø¯Ù… Ù…Ø·Ù„ÙˆØ¨";
                    return RedirectToAction(nameof(PendingBasicInfo));
                }

                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "Ø§Ù„Ù…Ø³ØªØ®Ø¯Ù… ØºÙŠØ± Ù…ÙˆØ¬ÙˆØ¯";
                    return RedirectToAction(nameof(PendingBasicInfo));
                }

                var userProfile = await GetUserProfileAsync(id);
                if (userProfile == null || !await CanCurrentManagerReviewUserAsync(userProfile, allowGovernorateOnlyWhenNoAffiliation: true))
                {
                    TempData["ErrorMessage"] = "âŒ Ù„Ø§ ØªÙ…Ù„Ùƒ ØµÙ„Ø§Ø­ÙŠØ© Ø¹Ø±Ø¶ ØªÙØ§ØµÙŠÙ„ Ù‡Ø°Ø§ Ø§Ù„Ù…Ø³ØªØ®Ø¯Ù…";
                    return RedirectToAction(nameof(PendingBasicInfo));
                }

                var roles = await _userManager.GetRolesAsync(user);
                var address = await GetUserAddressAsync(id);
                var voterCard = await GetUserVoterCardAsync(id);
                var union = await GetUserUnionAsync(id);
                var federation = await GetUserFederationAsync(id);
                var association = await GetUserAssociationAsync(id);
                var ngo = await GetUserNgoAsync(id);
                var affiliationInfo = await GetUserAffiliationInfoAsync(id);

                var rolesList = roles.ToList();
                if (userProfile.AccountType == "ÙØ±Ø¯" && !rolesList.Contains("ÙØ±Ø¯"))
                    rolesList.Add("ÙØ±Ø¯");

                var affiliationEntityName = await GetAffiliationEntityNameAsync(affiliationInfo?.AffiliationEntityId);
                var divisionName = await GetDivisionNameAsync(affiliationInfo?.DivisionId);
                var sectionName = await GetSectionNameAsync(affiliationInfo?.SectionId);
                var groupName = await GetGroupNameAsync(affiliationInfo?.GroupId);

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
                        entityName = await GetAffiliationEntityNameAsync(assignment.AffiliationEntityId);
                    else if (assignment.ManagementLevel == "Division" && assignment.DivisionId.HasValue)
                        divisionNameAssign = await GetDivisionNameAsync(assignment.DivisionId);
                    else if (assignment.ManagementLevel == "Section" && assignment.SectionId.HasValue)
                        sectionNameAssign = await GetSectionNameAsync(assignment.SectionId);
                    else if (assignment.ManagementLevel == "Group" && assignment.GroupId.HasValue)
                        groupNameAssign = await GetGroupNameAsync(assignment.GroupId);

                    managementDisplayList.Add(new ManagementAssignmentDisplayVM
                    {
                        Id = assignment.Id,
                        Level = assignment.ManagementLevel,
                        AssignmentRole = assignment.AssignmentRole,
                        LevelArabic = GetArabicLevelName(assignment.ManagementLevel, assignment.AssignmentRole),
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
                    PhoneNumber = userProfile.PhoneNumber ?? "",
                    IsActive = user.EmailConfirmed,
                    Roles = string.Join(", ", rolesList),
                    FullName = userProfile.FullName ?? "ØºÙŠØ± Ù…ÙƒØªÙ…Ù„",
                    LastName = userProfile.LastName ?? "",
                    MotherName = userProfile.MotherName ?? "",
                    DateOfBirth = userProfile.Date,
                    Gender = userProfile.Gender ?? "ØºÙŠØ± Ù…Ø­Ø¯Ø¯",
                    MaritalStatus = userProfile.MaritalStatus ?? "",
                    Education = userProfile.Education ?? "",
                    Specialization = userProfile.Specialization ?? "",
                    UniversityType = userProfile.UniversityType ?? "",
                    InstitutionType = userProfile.InstitutionType ?? "",
                    InstitutionName = userProfile.InstitutionName ?? "",
                    FacultyDepartment = userProfile.FacultyDepartment ?? "",
                    StudyType = userProfile.StudyType ?? "",
                    StudyStage = userProfile.StudyStage ?? "",
                    IdentityCardN = userProfile.IdentityCardN ?? "",
                    IdentityDate = userProfile.identityDate,
                    JobTitle = userProfile.JobTitle ?? "",
                    JobGrade = userProfile.JobGrade ?? "",
                    WorkGovernorate = GetEffectiveGovernorate(userProfile, address),
                    WorkDistrict = GetEffectiveDistrict(userProfile, address),
                    Address = address,
                    CoverImage = userProfile.CoverImage,
                    CreatedAt = userProfile.CreatedAt,
                    AffiliationDate = userProfile.AffiliationDate,
                    EmploymentStatus = userProfile.EmploymentStatus,
                    Work = userProfile.Work,
                    Ministry = userProfile.Ministry,
                    Department = userProfile.Department,
                    Position = userProfile.Position,
                    RequestedPromotion = userProfile.RequestedPromotion,
                    RequestedPromotionDate = userProfile.RequestedPromotionDate,
                    RejectionReason = userProfile.RejectionReason,
                    AccountType = userProfile.AccountType ?? "Ø¹Ø§Ø¯ÙŠ",
                    IsPromoted = userProfile.IsPromoted,
                    PromotionDate = userProfile.PromotionDate,
                    PromotedBy = await ResolveActorDisplayNameAsync(userProfile.PromotedBy),
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

                return View("~/Views/Admin/Details.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ø®Ø·Ø£ ÙÙŠ Ø¹Ø±Ø¶ ØªÙØ§ØµÙŠÙ„ Ø§Ù„Ù…Ø³ØªØ®Ø¯Ù… Ù„Ù„Ù…Ø³Ø¤ÙˆÙ„ Ø§Ù„Ø¥Ø¯Ø§Ø±ÙŠ");
                TempData["ErrorMessage"] = "Ø­Ø¯Ø« Ø®Ø·Ø£ ÙÙŠ ØªØ­Ù…ÙŠÙ„ Ø§Ù„Ø¨ÙŠØ§Ù†Ø§Øª";
                return RedirectToAction(nameof(PendingBasicInfo));
            }
        }

        [HttpGet]
        [Authorize(Roles = clsRoles.Manager)]
        public async Task<IActionResult> RequestHistory(
            string type = "promotion",
            string? searchName = null,
            string? searchGovernorate = null,
            string? searchPhone = null,
            int page = 1,
            int pageSize = 10)
        {
            var assignments = await GetCurrentManagerAssignmentsAsync();
            if (!assignments.Any())
            {
                TempData["ErrorMessage"] = "Ù„Ø§ ØªÙˆØ¬Ø¯ Ù…Ø³Ø¤ÙˆÙ„ÙŠØ© Ø¥Ø¯Ø§Ø±ÙŠØ© ÙØ¹Ø§Ù„Ø© Ù…Ø±ØªØ¨Ø·Ø© Ø¨Ø­Ø³Ø§Ø¨Ùƒ.";
                return RedirectToAction(nameof(Users));
            }

            var normalizedType = string.Equals(type, "basic", StringComparison.OrdinalIgnoreCase)
                ? "basic"
                : "promotion";

            pageSize = NormalizeRequestHistoryPageSize(pageSize);
            IQueryable<Identify> query = _context.Identifies
                .AsNoTracking()
                .Include(i => i.WorkLocation);
            query = normalizedType == "basic"
                ? query.Where(i => i.IsBasicInfoApproved || !string.IsNullOrEmpty(i.BasicInfoRejectionReason))
                : query.Where(i => i.IsPromoted || !string.IsNullOrEmpty(i.RejectionReason));

            query = ApplyRequestHistoryQueryFilters(query, searchName, searchGovernorate, searchPhone);

            var scopedIdentifies = await FilterUsersByManagerScopeAsync(
                await query.ToListAsync(),
                allowGovernorateOnlyWhenNoAffiliation: normalizedType == "basic");

            var orderedScopedIdentifies = scopedIdentifies
                .OrderByDescending(i => normalizedType == "basic"
                    ? (i.BasicInfoRequestedAt ?? i.CreatedAt)
                    : (i.RequestedPromotionDate ?? i.CreatedAt))
                .ToList();
            var governorateOptions = await GetRequestHistoryGovernorateOptionsAsync(orderedScopedIdentifies);

            var totalItems = orderedScopedIdentifies.Count;
            var approvedCount = normalizedType == "basic"
                ? orderedScopedIdentifies.Count(i => i.IsBasicInfoApproved && string.IsNullOrEmpty(i.BasicInfoRejectionReason))
                : orderedScopedIdentifies.Count(i => i.IsPromoted && string.IsNullOrEmpty(i.RejectionReason));
            var rejectedCount = normalizedType == "basic"
                ? orderedScopedIdentifies.Count(i => !string.IsNullOrEmpty(i.BasicInfoRejectionReason))
                : orderedScopedIdentifies.Count(i => !string.IsNullOrEmpty(i.RejectionReason));
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
            page = Math.Max(1, Math.Min(page, totalPages));

            var pagedIdentifies = orderedScopedIdentifies
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var userIds = pagedIdentifies.Select(i => i.UserId).ToList();
            var usersById = await GetUsersByIdsAsync(userIds);
            var addressesByUserId = await GetAddressesByUserIdsAsync(userIds);

            var items = new List<RequestHistoryItemViewModel>();
            foreach (var identify in pagedIdentifies)
            {
                usersById.TryGetValue(identify.UserId, out var user);
                addressesByUserId.TryGetValue(identify.UserId, out var address);
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
                        ? identify.BasicInfoApprovedBy
                        : identify.PromotedBy,
                    Status = isRejected ? "Ù…Ø±ÙÙˆØ¶" : "ØªÙ…Øª Ø§Ù„Ù…ÙˆØ§ÙÙ‚Ø©",
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
                Title = normalizedType == "basic" ? "Ø³Ø¬Ù„ Ø·Ù„Ø¨Ø§Øª Ø§Ù„Ù…Ø±Ø§Ø¬Ø¹Ø©" : "Ø³Ø¬Ù„ Ø·Ù„Ø¨Ø§Øª Ø§Ù„ØªØ±Ù‚ÙŠØ©",
                Subtitle = "Ø§Ù„Ø·Ù„Ø¨Ø§Øª Ø§Ù„Ù…Ø¹Ø§Ù„Ø¬Ø© Ù…Ø±ØªØ¨Ø© Ù…Ù† Ø§Ù„Ø£Ø­Ø¯Ø« Ø¥Ù„Ù‰ Ø§Ù„Ø£Ù‚Ø¯Ù…",
                BackAction = normalizedType == "basic" ? nameof(PendingBasicInfo) : nameof(PromotionRequests),
                BackController = "ManagerReview",
                DetailsAction = nameof(Details),
                DetailsController = "ManagerReview",
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

        private async Task<List<string>> GetRequestHistoryGovernorateOptionsAsync(IEnumerable<Identify> identifies)
        {
            var profiles = identifies
                .Select(i => new { i.Id, i.UserId, i.WorkGovernorate })
                .ToList();

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = clsRoles.Manager)]
        public async Task<IActionResult> ApproveBasicInfo(int id)
        {
            try
            {
                var identify = await _context.Identifies.FindAsync(id);
                if (identify == null)
                    return Json(new { success = false, message = "Ø§Ù„Ù…Ø³ØªØ®Ø¯Ù… ØºÙŠØ± Ù…ÙˆØ¬ÙˆØ¯" });

                if (!await CanCurrentManagerReviewUserAsync(identify, allowGovernorateOnlyWhenNoAffiliation: true))
                    return Json(new { success = false, message = "âŒ Ù„Ø§ ØªÙ…Ù„Ùƒ ØµÙ„Ø§Ø­ÙŠØ© Ø§Ù„Ù…ÙˆØ§ÙÙ‚Ø© Ø¹Ù„Ù‰ Ù‡Ø°Ø§ Ø§Ù„Ù…Ø³ØªØ®Ø¯Ù…" });

                identify.IsBasicInfoApproved = true;
                identify.BasicInfoApprovedBy = await GetCurrentActorDisplayNameAsync("Manager");
                identify.BasicInfoApprovalDate = DateTime.Now;

                _context.Identifies.Update(identify);
                await _context.SaveChangesAsync();

                await _notificationService.CreateNotificationFromTemplate(
                    NotificationTemplateKeys.BasicInfoApproved,
                    identify.UserId,
                    icon: "bi-check-circle",
                    clickUrl: "/Register/AdditionalInfo");

                return Json(new { success = true, message = "âœ… ØªÙ…Øª Ø§Ù„Ù…ÙˆØ§ÙÙ‚Ø© Ø¹Ù„Ù‰ Ø§Ù„Ø¨ÙŠØ§Ù†Ø§Øª Ø§Ù„Ø£Ø³Ø§Ø³ÙŠØ©" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ø®Ø·Ø£ ÙÙŠ Ø§Ù„Ù…ÙˆØ§ÙÙ‚Ø© Ø¹Ù„Ù‰ Ø§Ù„Ø¨ÙŠØ§Ù†Ø§Øª Ø§Ù„Ø£Ø³Ø§Ø³ÙŠØ© Ù…Ù† Ù‚Ø¨Ù„ Ø§Ù„Ù…Ø³Ø¤ÙˆÙ„ Ø§Ù„Ø¥Ø¯Ø§Ø±ÙŠ");
                return Json(new { success = false, message = $"âŒ Ø­Ø¯Ø« Ø®Ø·Ø£: {ex.Message}" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = clsRoles.Manager)]
        public async Task<IActionResult> RejectBasicInfo(int id, string reason)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(reason))
                    return Json(new { success = false, message = "âŒ Ø§Ù„Ø±Ø¬Ø§Ø¡ ÙƒØªØ§Ø¨Ø© Ø³Ø¨Ø¨ Ø§Ù„Ø±ÙØ¶" });

                var identify = await _context.Identifies.FindAsync(id);
                if (identify == null)
                    return Json(new { success = false, message = "âŒ Ø§Ù„Ù…Ø³ØªØ®Ø¯Ù… ØºÙŠØ± Ù…ÙˆØ¬ÙˆØ¯" });

                if (!await CanCurrentManagerReviewUserAsync(identify, allowGovernorateOnlyWhenNoAffiliation: true))
                    return Json(new { success = false, message = "âŒ Ù„Ø§ ØªÙ…Ù„Ùƒ ØµÙ„Ø§Ø­ÙŠØ© Ø±ÙØ¶ Ù‡Ø°Ø§ Ø§Ù„Ù…Ø³ØªØ®Ø¯Ù…" });

                identify.BasicInfoRejectionReason = reason;
                identify.IsBasicInfoApproved = false;
                identify.BasicInfoApprovedBy = await GetCurrentActorDisplayNameAsync("Manager");
                identify.BasicInfoApprovalDate = null;

                _context.Identifies.Update(identify);
                await _context.SaveChangesAsync();

                await _notificationService.CreateNotificationFromTemplate(
                    NotificationTemplateKeys.BasicInfoRejected,
                    identify.UserId,
                    new Dictionary<string, string?> { ["reason"] = reason },
                    "bi-x-circle-fill",
                    "/Register/BasicInfo");

                return Json(new { success = true, message = "âœ… ØªÙ… Ø±ÙØ¶ Ø§Ù„Ø·Ù„Ø¨ Ø¨Ù†Ø¬Ø§Ø­" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ø®Ø·Ø£ ÙÙŠ Ø±ÙØ¶ Ø§Ù„Ø¨ÙŠØ§Ù†Ø§Øª Ø§Ù„Ø£Ø³Ø§Ø³ÙŠØ© Ù…Ù† Ù‚Ø¨Ù„ Ø§Ù„Ù…Ø³Ø¤ÙˆÙ„ Ø§Ù„Ø¥Ø¯Ø§Ø±ÙŠ");
                return Json(new { success = false, message = $"âŒ Ø­Ø¯Ø« Ø®Ø·Ø£: {ex.Message}" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = clsRoles.Manager)]
        public async Task<IActionResult> ApprovePromotion(int id)
        {
            try
            {
                var identify = await _context.Identifies.FindAsync(id);
                if (identify == null)
                    return Json(new { success = false, message = "Ø§Ù„Ù…Ø³ØªØ®Ø¯Ù… ØºÙŠØ± Ù…ÙˆØ¬ÙˆØ¯" });

                if (!await CanCurrentManagerReviewUserAsync(identify))
                    return Json(new { success = false, message = "âŒ Ù„Ø§ ØªÙ…Ù„Ùƒ ØµÙ„Ø§Ø­ÙŠØ© Ø§Ù„Ù…ÙˆØ§ÙÙ‚Ø© Ø¹Ù„Ù‰ Ù‡Ø°Ø§ Ø§Ù„Ø·Ù„Ø¨" });

                identify.AccountType = "ÙØ±Ø¯";
                identify.IsPromoted = true;
                identify.PromotionDate = DateTime.Now;
                identify.PromotedBy = await GetCurrentActorDisplayNameAsync("Manager");
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

                await _notificationService.CreateNotificationFromTemplate(
                    NotificationTemplateKeys.PromotionApproved,
                    identify.UserId,
                    icon: "bi-star-fill",
                    clickUrl: "/Register/ProfileDetails");

                return Json(new { success = true, message = "âœ… ØªÙ…Øª Ø§Ù„Ù…ÙˆØ§ÙÙ‚Ø© Ø¹Ù„Ù‰ Ø§Ù„ØªØ±Ù‚ÙŠØ© Ø¨Ù†Ø¬Ø§Ø­" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ø®Ø·Ø£ ÙÙŠ Ø§Ù„Ù…ÙˆØ§ÙÙ‚Ø© Ø¹Ù„Ù‰ Ø§Ù„ØªØ±Ù‚ÙŠØ© Ù…Ù† Ù‚Ø¨Ù„ Ø§Ù„Ù…Ø³Ø¤ÙˆÙ„ Ø§Ù„Ø¥Ø¯Ø§Ø±ÙŠ");
                return Json(new { success = false, message = $"Ø­Ø¯Ø« Ø®Ø·Ø£: {ex.Message}" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = clsRoles.Manager)]
        public async Task<IActionResult> RejectPromotion(int id, string reason)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(reason))
                    return Json(new { success = false, message = "Ø§Ù„Ø±Ø¬Ø§Ø¡ ÙƒØªØ§Ø¨Ø© Ø³Ø¨Ø¨ Ø§Ù„Ø±ÙØ¶" });

                var identify = await _context.Identifies.FindAsync(id);
                if (identify == null)
                    return Json(new { success = false, message = "Ø§Ù„Ù…Ø³ØªØ®Ø¯Ù… ØºÙŠØ± Ù…ÙˆØ¬ÙˆØ¯" });

                if (!await CanCurrentManagerReviewUserAsync(identify))
                    return Json(new { success = false, message = "âŒ Ù„Ø§ ØªÙ…Ù„Ùƒ ØµÙ„Ø§Ø­ÙŠØ© Ø±ÙØ¶ Ù‡Ø°Ø§ Ø§Ù„Ø·Ù„Ø¨" });

                identify.RequestedPromotion = false;
                identify.RejectionReason = reason;
                identify.PromotionDate = null;
                identify.PromotedBy = await GetCurrentActorDisplayNameAsync("Manager");

                _context.Identifies.Update(identify);
                await _context.SaveChangesAsync();

                await _notificationService.CreateNotificationFromTemplate(
                    NotificationTemplateKeys.PromotionRejected,
                    identify.UserId,
                    new Dictionary<string, string?> { ["reason"] = reason },
                    "bi-x-circle-fill",
                    "/Register/ProfileDetails");

                return Json(new { success = true, message = "âœ… ØªÙ… Ø±ÙØ¶ Ø§Ù„Ø·Ù„Ø¨ Ø¨Ù†Ø¬Ø§Ø­" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ø®Ø·Ø£ ÙÙŠ Ø±ÙØ¶ Ø§Ù„ØªØ±Ù‚ÙŠØ© Ù…Ù† Ù‚Ø¨Ù„ Ø§Ù„Ù…Ø³Ø¤ÙˆÙ„ Ø§Ù„Ø¥Ø¯Ø§Ø±ÙŠ");
                return Json(new { success = false, message = $"âŒ Ø­Ø¯Ø« Ø®Ø·Ø£: {ex.Message}" });
            }
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

    public class ManagerScopedUserVM
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Governorate { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string AffiliationEntity { get; set; } = string.Empty;
        public string Division { get; set; } = string.Empty;
        public string Section { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public string BadgeNumber { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsPromoted { get; set; }
        public bool RequestedPromotion { get; set; }
        public bool IsManager { get; set; }
        public string ManagementRoleArabic { get; set; } = string.Empty;
        public string ManagementLevelArabic { get; set; } = string.Empty;
        public int CompletionPercentage { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CentralManagedManagerVM
    {
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Governorate { get; set; } = string.Empty;
        public string AssignmentRole { get; set; } = string.Empty;
        public string AssignmentRoleArabic { get; set; } = string.Empty;
        public string ManagementLevel { get; set; } = string.Empty;
        public string ManagementLevelArabic { get; set; } = string.Empty;
        public string ManagedUnitName { get; set; } = string.Empty;
        public string CentralScopeName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
