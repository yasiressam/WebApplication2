using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;
using WebApplication2.Models.ViewModels;

namespace WebApplication2.Controllers
{
    [Authorize]
    public class EvaluationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<EvaluationController> _logger;

        public EvaluationController(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            ILogger<EvaluationController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }
        // ========== ✅ أضف هذه الدالة هنا ==========
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        // ========== دوال مساعدة ==========

        private async Task<bool> IsSuperAdmin()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(currentUser);
            return roles.Contains(clsRoles.SuperAdmin);
        }

        private async Task<string?> GetCurrentAdminGovernorate()
        {
            var currentUserId = _userManager.GetUserId(User);
            var adminProfile = await _context.Identifies
                .Include(i => i.WorkLocation)
                .FirstOrDefaultAsync(i => i.UserId == currentUserId);
            return adminProfile?.ManagedGovernorate;
        }

        private string GetEffectiveGovernorate(Identify? profile, Address? address)
        {
            var workLocation = profile != null
                ? profile.WorkLocation ?? _context.WorkLocations.AsNoTracking().FirstOrDefault(w => w.IdentifyId == profile.Id)
                : null;

            return !string.IsNullOrWhiteSpace(workLocation?.Governorate)
                ? workLocation.Governorate
                : !string.IsNullOrWhiteSpace(profile?.WorkGovernorate)
                ? profile.WorkGovernorate
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

            return managed == "بغداد مركزي" &&
                   (current == "بغداد" || current.StartsWith("بغداد -", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// جلب جميع الأفراد المصعدين (IsPromoted = true)
        /// </summary>
        private async Task<List<Identify>> GetPromotedUsersAsync()
        {
            var isSuperAdmin = await IsSuperAdmin();
            var adminGovernorate = await GetCurrentAdminGovernorate();

            var query = _context.Identifies.Where(i => i.IsPromoted == true);

            if (!isSuperAdmin && !string.IsNullOrEmpty(adminGovernorate))
            {
                var allAddresses = await _context.Addresses.ToListAsync();
                var allProfiles = await _context.Identifies.ToListAsync();
                var userIdsInGovernorate = allAddresses
                    .Where(a =>
                    {
                        var profile = allProfiles.FirstOrDefault(i => i.UserId == a.UserId);
                        return IsGovernorateInManagedScope(GetEffectiveGovernorate(profile, a), adminGovernorate);
                    })
                    .Select(a => a.UserId)
                    .ToList();
                query = query.Where(i => userIdsInGovernorate.Contains(i.UserId));
            }

            return await query.OrderBy(i => i.FullName).ToListAsync();
        }

        private (List<Identify> Users, int CurrentPage, int TotalPages, int TotalUsers, int PageSize) PaginateEvaluationUsers(List<Identify> users, int page)
        {
            const int pageSize = 10;
            var totalUsers = users.Count;
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalUsers / (double)pageSize));
            var currentPage = Math.Max(1, Math.Min(page, totalPages));
            var pagedUsers = users
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return (pagedUsers, currentPage, totalPages, totalUsers, pageSize);
        }

        /// <summary>
        /// الحصول على الشهر والسنة الحاليين أو المحددين
        /// </summary>
        private (int month, int year) GetCurrentMonthYear(int? month, int? year)
        {
            return (
                month ?? DateTime.Now.Month,
                year ?? DateTime.Now.Year
            );
        }

        // ========== عرض جميع الأفراد لتقييم عامل معين ==========

        /// <summary>
        /// صفحة تقييم عامل التواصل
        /// </summary>
        [Authorize(Roles = clsRoles.Admin + "," + clsRoles.SuperAdmin)]
        [HttpGet]
        public async Task<IActionResult> Communication(int? month, int? year, int page = 1)
        {
            var (currentMonth, currentYear) = GetCurrentMonthYear(month, year);
            var users = await GetPromotedUsersAsync();
            var paged = PaginateEvaluationUsers(users, page);

            var evaluations = await _context.CommunicationEvaluations
                .Where(e => e.Month == currentMonth && e.Year == currentYear)
                .ToDictionaryAsync(e => e.UserId, e => e.Score);

            var model = new EvaluationFactorViewModel
            {
                FactorName = "Communication",
                FactorDisplayName = "التواصل",
                MaxScore = 12,
                Month = currentMonth,
                Year = currentYear,
                CurrentPage = paged.CurrentPage,
                TotalPages = paged.TotalPages,
                TotalUsers = paged.TotalUsers,
                PageSize = paged.PageSize,
                Users = paged.Users.Select(u => new UserFactorScoreVM
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    PhoneNumber = u.PhoneNumber,
                    Governorate = GetUserGovernorate(u.UserId).Result,
                    CurrentScore = evaluations.ContainsKey(u.UserId) ? evaluations[u.UserId] : 6,
                    MaxScore = 12
                }).ToList()
            };

            return View(model);
        }

        /// <summary>
        /// حفظ تقييم عامل التواصل
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveCommunication(string userId, int score, int month, int year)
        {
            var existing = await _context.CommunicationEvaluations
                .FirstOrDefaultAsync(e => e.UserId == userId && e.Month == month && e.Year == year);

            if (existing != null)
            {
                existing.Score = score;
                existing.EvaluatedBy = _userManager.GetUserId(User);
                existing.EvaluatedAt = DateTime.Now;
                _context.CommunicationEvaluations.Update(existing);
            }
            else
            {
                var evaluation = new CommunicationEvaluation
                {
                    UserId = userId,
                    Score = score,
                    Month = month,
                    Year = year,
                    EvaluatedBy = _userManager.GetUserId(User),
                    EvaluatedAt = DateTime.Now
                };
                _context.CommunicationEvaluations.Add(evaluation);
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "✅ تم حفظ التقييم بنجاح";
            return RedirectToAction("Communication", new { month, year });
        }

        // ========== نفس الدوال لباقي العوامل ==========

        [Authorize(Roles = clsRoles.Admin + "," + clsRoles.SuperAdmin)]
        [HttpGet]
        public async Task<IActionResult> MediaActivity(int? month, int? year, int page = 1)
        {
            var (currentMonth, currentYear) = GetCurrentMonthYear(month, year);
            var users = await GetPromotedUsersAsync();
            var paged = PaginateEvaluationUsers(users, page);
            var evaluations = await _context.MediaActivityEvaluations
                .Where(e => e.Month == currentMonth && e.Year == currentYear)
                .ToDictionaryAsync(e => e.UserId, e => e.Score);

            var model = new EvaluationFactorViewModel
            {
                FactorName = "MediaActivity",
                FactorDisplayName = "النشاط الإعلامي",
                MaxScore = 12,
                Month = currentMonth,
                Year = currentYear,
                CurrentPage = paged.CurrentPage,
                TotalPages = paged.TotalPages,
                TotalUsers = paged.TotalUsers,
                PageSize = paged.PageSize,
                Users = paged.Users.Select(u => new UserFactorScoreVM
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    PhoneNumber = u.PhoneNumber,
                    Governorate = GetUserGovernorate(u.UserId).Result,
                    CurrentScore = evaluations.ContainsKey(u.UserId) ? evaluations[u.UserId] : 6,
                    MaxScore = 12
                }).ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveMediaActivity(string userId, int score, int month, int year)
        {
            var existing = await _context.MediaActivityEvaluations
                .FirstOrDefaultAsync(e => e.UserId == userId && e.Month == month && e.Year == year);

            if (existing != null)
            {
                existing.Score = score;
                existing.EvaluatedBy = _userManager.GetUserId(User);
                existing.EvaluatedAt = DateTime.Now;
                _context.MediaActivityEvaluations.Update(existing);
            }
            else
            {
                _context.MediaActivityEvaluations.Add(new MediaActivityEvaluation
                {
                    UserId = userId,
                    Score = score,
                    Month = month,
                    Year = year,
                    EvaluatedBy = _userManager.GetUserId(User),
                    EvaluatedAt = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "✅ تم حفظ التقييم بنجاح";
            return RedirectToAction("MediaActivity", new { month, year });
        }

        [Authorize(Roles = clsRoles.Admin + "," + clsRoles.SuperAdmin)]
        [HttpGet]
        public async Task<IActionResult> MovementActivity(int? month, int? year, int page = 1)
        {
            var (currentMonth, currentYear) = GetCurrentMonthYear(month, year);
            var users = await GetPromotedUsersAsync();
            var paged = PaginateEvaluationUsers(users, page);
            var evaluations = await _context.MovementActivityEvaluations
                .Where(e => e.Month == currentMonth && e.Year == currentYear)
                .ToDictionaryAsync(e => e.UserId, e => e.Score);

            var model = new EvaluationFactorViewModel
            {
                FactorName = "MovementActivity",
                FactorDisplayName = "النشاط الحركي",
                MaxScore = 12,
                Month = currentMonth,
                Year = currentYear,
                CurrentPage = paged.CurrentPage,
                TotalPages = paged.TotalPages,
                TotalUsers = paged.TotalUsers,
                PageSize = paged.PageSize,
                Users = paged.Users.Select(u => new UserFactorScoreVM
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    PhoneNumber = u.PhoneNumber,
                    Governorate = GetUserGovernorate(u.UserId).Result,
                    CurrentScore = evaluations.ContainsKey(u.UserId) ? evaluations[u.UserId] : 6,
                    MaxScore = 12
                }).ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveMovementActivity(string userId, int score, int month, int year)
        {
            var existing = await _context.MovementActivityEvaluations
                .FirstOrDefaultAsync(e => e.UserId == userId && e.Month == month && e.Year == year);

            if (existing != null)
            {
                existing.Score = score;
                existing.EvaluatedBy = _userManager.GetUserId(User);
                existing.EvaluatedAt = DateTime.Now;
                _context.MovementActivityEvaluations.Update(existing);
            }
            else
            {
                _context.MovementActivityEvaluations.Add(new MovementActivityEvaluation
                {
                    UserId = userId,
                    Score = score,
                    Month = month,
                    Year = year,
                    EvaluatedBy = _userManager.GetUserId(User),
                    EvaluatedAt = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "✅ تم حفظ التقييم بنجاح";
            return RedirectToAction("MovementActivity", new { month, year });
        }

        [Authorize(Roles = clsRoles.Admin + "," + clsRoles.SuperAdmin)]
        [HttpGet]
        public async Task<IActionResult> Polarization(int? month, int? year, int page = 1)
        {
            var (currentMonth, currentYear) = GetCurrentMonthYear(month, year);
            var users = await GetPromotedUsersAsync();
            var paged = PaginateEvaluationUsers(users, page);
            var evaluations = await _context.PolarizationEvaluations
                .Where(e => e.Month == currentMonth && e.Year == currentYear)
                .ToDictionaryAsync(e => e.UserId, e => e.Score);

            var model = new EvaluationFactorViewModel
            {
                FactorName = "Polarization",
                FactorDisplayName = "الاستقطاب",
                MaxScore = 12,
                Month = currentMonth,
                Year = currentYear,
                CurrentPage = paged.CurrentPage,
                TotalPages = paged.TotalPages,
                TotalUsers = paged.TotalUsers,
                PageSize = paged.PageSize,
                Users = paged.Users.Select(u => new UserFactorScoreVM
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    PhoneNumber = u.PhoneNumber,
                    Governorate = GetUserGovernorate(u.UserId).Result,
                    CurrentScore = evaluations.ContainsKey(u.UserId) ? evaluations[u.UserId] : 6,
                    MaxScore = 12
                }).ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SavePolarization(string userId, int score, int month, int year)
        {
            var existing = await _context.PolarizationEvaluations
                .FirstOrDefaultAsync(e => e.UserId == userId && e.Month == month && e.Year == year);

            if (existing != null)
            {
                existing.Score = score;
                existing.EvaluatedBy = _userManager.GetUserId(User);
                existing.EvaluatedAt = DateTime.Now;
                _context.PolarizationEvaluations.Update(existing);
            }
            else
            {
                _context.PolarizationEvaluations.Add(new PolarizationEvaluation
                {
                    UserId = userId,
                    Score = score,
                    Month = month,
                    Year = year,
                    EvaluatedBy = _userManager.GetUserId(User),
                    EvaluatedAt = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "✅ تم حفظ التقييم بنجاح";
            return RedirectToAction("Polarization", new { month, year });
        }

        [Authorize(Roles = clsRoles.Admin + "," + clsRoles.SuperAdmin)]
        [HttpGet]
        public async Task<IActionResult> SocialMedia(int? month, int? year, int page = 1)
        {
            var (currentMonth, currentYear) = GetCurrentMonthYear(month, year);
            var users = await GetPromotedUsersAsync();
            var paged = PaginateEvaluationUsers(users, page);
            var evaluations = await _context.SocialMediaEvaluations
                .Where(e => e.Month == currentMonth && e.Year == currentYear)
                .ToDictionaryAsync(e => e.UserId, e => e.Score);

            var model = new EvaluationFactorViewModel
            {
                FactorName = "SocialMedia",
                FactorDisplayName = "نشاط التواصل الاجتماعي",
                MaxScore = 12,
                Month = currentMonth,
                Year = currentYear,
                CurrentPage = paged.CurrentPage,
                TotalPages = paged.TotalPages,
                TotalUsers = paged.TotalUsers,
                PageSize = paged.PageSize,
                Users = paged.Users.Select(u => new UserFactorScoreVM
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    PhoneNumber = u.PhoneNumber,
                    Governorate = GetUserGovernorate(u.UserId).Result,
                    CurrentScore = evaluations.ContainsKey(u.UserId) ? evaluations[u.UserId] : 6,
                    MaxScore = 12
                }).ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSocialMedia(string userId, int score, int month, int year)
        {
            var existing = await _context.SocialMediaEvaluations
                .FirstOrDefaultAsync(e => e.UserId == userId && e.Month == month && e.Year == year);

            if (existing != null)
            {
                existing.Score = score;
                existing.EvaluatedBy = _userManager.GetUserId(User);
                existing.EvaluatedAt = DateTime.Now;
                _context.SocialMediaEvaluations.Update(existing);
            }
            else
            {
                _context.SocialMediaEvaluations.Add(new SocialMediaEvaluation
                {
                    UserId = userId,
                    Score = score,
                    Month = month,
                    Year = year,
                    EvaluatedBy = _userManager.GetUserId(User),
                    EvaluatedAt = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "✅ تم حفظ التقييم بنجاح";
            return RedirectToAction("SocialMedia", new { month, year });
        }

        [Authorize(Roles = clsRoles.Admin + "," + clsRoles.SuperAdmin)]
        [HttpGet]
        public async Task<IActionResult> SupervisorOpinion(int? month, int? year, int page = 1)
        {
            var (currentMonth, currentYear) = GetCurrentMonthYear(month, year);
            var users = await GetPromotedUsersAsync();
            var paged = PaginateEvaluationUsers(users, page);
            var evaluations = await _context.SupervisorOpinionEvaluations
                .Where(e => e.Month == currentMonth && e.Year == currentYear)
                .ToDictionaryAsync(e => e.UserId, e => e.Score);

            var model = new EvaluationFactorViewModel
            {
                FactorName = "SupervisorOpinion",
                FactorDisplayName = "رأي المسؤول المباشر",
                MaxScore = 16,
                Month = currentMonth,
                Year = currentYear,
                CurrentPage = paged.CurrentPage,
                TotalPages = paged.TotalPages,
                TotalUsers = paged.TotalUsers,
                PageSize = paged.PageSize,
                Users = paged.Users.Select(u => new UserFactorScoreVM
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    PhoneNumber = u.PhoneNumber,
                    Governorate = GetUserGovernorate(u.UserId).Result,
                    CurrentScore = evaluations.ContainsKey(u.UserId) ? evaluations[u.UserId] : 8,
                    MaxScore = 16
                }).ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSupervisorOpinion(string userId, int score, int month, int year)
        {
            var existing = await _context.SupervisorOpinionEvaluations
                .FirstOrDefaultAsync(e => e.UserId == userId && e.Month == month && e.Year == year);

            if (existing != null)
            {
                existing.Score = score;
                existing.EvaluatedBy = _userManager.GetUserId(User);
                existing.EvaluatedAt = DateTime.Now;
                _context.SupervisorOpinionEvaluations.Update(existing);
            }
            else
            {
                _context.SupervisorOpinionEvaluations.Add(new SupervisorOpinionEvaluation
                {
                    UserId = userId,
                    Score = score,
                    Month = month,
                    Year = year,
                    EvaluatedBy = _userManager.GetUserId(User),
                    EvaluatedAt = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "✅ تم حفظ التقييم بنجاح";
            return RedirectToAction("SupervisorOpinion", new { month, year });
        }

        // ========== لوحة التحكم (Dashboard) ==========

        [HttpGet]
        public async Task<IActionResult> Dashboard(int? month, int? year)
        {
            var (currentMonth, currentYear) = GetCurrentMonthYear(month, year);
            var users = await GetPromotedUsersAsync();

            // جلب جميع التقييمات دفعة واحدة
            var communicationScores = await _context.CommunicationEvaluations
                .Where(e => e.Month == currentMonth && e.Year == currentYear)
                .ToDictionaryAsync(e => e.UserId, e => e.Score);

            var mediaScores = await _context.MediaActivityEvaluations
                .Where(e => e.Month == currentMonth && e.Year == currentYear)
                .ToDictionaryAsync(e => e.UserId, e => e.Score);

            var movementScores = await _context.MovementActivityEvaluations
                .Where(e => e.Month == currentMonth && e.Year == currentYear)
                .ToDictionaryAsync(e => e.UserId, e => e.Score);

            var polarizationScores = await _context.PolarizationEvaluations
                .Where(e => e.Month == currentMonth && e.Year == currentYear)
                .ToDictionaryAsync(e => e.UserId, e => e.Score);

            var socialScores = await _context.SocialMediaEvaluations
                .Where(e => e.Month == currentMonth && e.Year == currentYear)
                .ToDictionaryAsync(e => e.UserId, e => e.Score);

            var supervisorScores = await _context.SupervisorOpinionEvaluations
                .Where(e => e.Month == currentMonth && e.Year == currentYear)
                .ToDictionaryAsync(e => e.UserId, e => e.Score);

            var politicalForumScores = await _context.PoliticalForumAttendances
                .Where(e => e.Month == currentMonth && e.Year == currentYear)
                .GroupBy(e => e.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.Sum(x => x.Score));

            var periodicScores = await _context.PeriodicMeetingAttendances
                .Where(e => e.Month == currentMonth && e.Year == currentYear)
                .GroupBy(e => e.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.Sum(x => x.Score));

            var summaries = new List<UserEvaluationSummary>();

            foreach (var user in users)
            {
                summaries.Add(new UserEvaluationSummary
                {
                    UserId = user.UserId,
                    FullName = user.FullName,
                    PhoneNumber = user.PhoneNumber,
                    Governorate = await GetUserGovernorate(user.UserId),
                    CommunicationScore = communicationScores.GetValueOrDefault(user.UserId, 6),
                    MediaActivityScore = mediaScores.GetValueOrDefault(user.UserId, 6),
                    MovementActivityScore = movementScores.GetValueOrDefault(user.UserId, 6),
                    PolarizationScore = polarizationScores.GetValueOrDefault(user.UserId, 6),
                    SocialMediaScore = socialScores.GetValueOrDefault(user.UserId, 6),
                    SupervisorOpinionScore = supervisorScores.GetValueOrDefault(user.UserId, 8),
                    PoliticalForumScore = politicalForumScores.GetValueOrDefault(user.UserId, 0),
                    PeriodicMeetingsScore = periodicScores.GetValueOrDefault(user.UserId, 0)
                });
            }

            ViewBag.Month = currentMonth;
            ViewBag.Year = currentYear;
            ViewBag.MonthsList = GetMonthsList();
            ViewBag.YearsList = GetYearsList();

            return View(summaries);
        }

        // ========== ✅ دوال الحضور ==========

        /// <summary>
        /// صفحة تسجيل الحضور
        /// </summary>
        [Authorize(Roles = clsRoles.Admin + "," + clsRoles.SuperAdmin)]
        [HttpGet]
        public async Task<IActionResult> Attendance(int? month, int? year)
        {
            var currentMonth = month ?? DateTime.Now.Month;
            var currentYear = year ?? DateTime.Now.Year;

            // جلب الأحداث النشطة (قادمة أو جارية)
            var events = await _context.Events
                .Where(e => e.IsActive && e.EndDate >= DateTime.Now)
                .OrderBy(e => e.StartDate)
                .Take(20)
                .ToListAsync();

            var model = new AttendanceViewModel
            {
                Month = currentMonth,
                Year = currentYear,
                Events = events
            };

            return View(model);
        }

        /// <summary>
        /// جلب المستخدمين المستهدفين لحدث معين
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetEventTargetUsers(int eventId, int month, int year)
        {
            var eventItem = await _context.Events.FindAsync(eventId);
            if (eventItem == null)
                return Json(new { success = false, message = "الحدث غير موجود" });

            var targetUsers = await GetTargetUsersForEvent(eventItem);
            var users = new List<object>();

            foreach (var userId in targetUsers)
            {
                var profile = await _context.Identifies.FirstOrDefaultAsync(i => i.UserId == userId);

                // جلب الحضور من الجدول المناسب حسب نوع الحدث
                bool attended = false;
                if (eventItem.EventType == "منتدى سياسي")
                {
                    var attendance = await _context.PoliticalForumAttendances
                        .FirstOrDefaultAsync(a => a.UserId == userId && a.EventId == eventId && a.Month == month && a.Year == year);
                    attended = attendance?.Attended ?? false;
                }
                else if (eventItem.EventType == "اجتماع دوري")
                {
                    var attendance = await _context.PeriodicMeetingAttendances
                        .FirstOrDefaultAsync(a => a.UserId == userId && a.EventId == eventId && a.Month == month && a.Year == year);
                    attended = attendance?.Attended ?? false;
                }

                users.Add(new
                {
                    userId = userId,
                    fullName = profile?.FullName ?? "",
                    phoneNumber = profile?.PhoneNumber ?? "",
                    attended = attended
                });
            }

            return Json(new { success = true, users = users });
        }

        /// <summary>
        /// حفظ الحضور
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SaveAttendance([FromBody] SaveAttendanceRequest request)
        {
            try
            {
                var eventItem = await _context.Events.FindAsync(request.EventId);
                if (eventItem == null)
                    return Json(new { success = false, message = "الحدث غير موجود" });

                if (eventItem.EventType == "منتدى سياسي")
                {
                    var existing = await _context.PoliticalForumAttendances
                        .FirstOrDefaultAsync(a => a.UserId == request.UserId && a.EventId == request.EventId && a.Month == request.Month && a.Year == request.Year);

                    if (existing != null)
                    {
                        existing.Attended = request.Attended;
                        existing.RecordedBy = _userManager.GetUserId(User);
                        existing.RecordedAt = DateTime.Now;
                        _context.PoliticalForumAttendances.Update(existing);
                    }
                    else
                    {
                        _context.PoliticalForumAttendances.Add(new PoliticalForumAttendance
                        {
                            UserId = request.UserId,
                            EventId = request.EventId,
                            Attended = request.Attended,
                            Month = request.Month,
                            Year = request.Year,
                            RecordedBy = _userManager.GetUserId(User),
                            RecordedAt = DateTime.Now
                        });
                    }
                }
                else if (eventItem.EventType == "اجتماع دوري")
                {
                    var existing = await _context.PeriodicMeetingAttendances
                        .FirstOrDefaultAsync(a => a.UserId == request.UserId && a.EventId == request.EventId && a.Month == request.Month && a.Year == request.Year);

                    if (existing != null)
                    {
                        existing.Attended = request.Attended;
                        existing.RecordedBy = _userManager.GetUserId(User);
                        existing.RecordedAt = DateTime.Now;
                        _context.PeriodicMeetingAttendances.Update(existing);
                    }
                    else
                    {
                        _context.PeriodicMeetingAttendances.Add(new PeriodicMeetingAttendance
                        {
                            UserId = request.UserId,
                            EventId = request.EventId,
                            Attended = request.Attended,
                            Month = request.Month,
                            Year = request.Year,
                            RecordedBy = _userManager.GetUserId(User),
                            RecordedAt = DateTime.Now
                        });
                    }
                }

                await _context.SaveChangesAsync();

                // بعد حفظ الحضور، قم بتحديث درجة الحضور للشهر
                await UpdateAttendanceScore(request.UserId, request.Month, request.Year, eventItem.EventType);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// تحديث درجة الحضور للمستخدم في شهر معين
        /// </summary>
        private async Task UpdateAttendanceScore(string userId, int month, int year, string eventType)
        {
            if (eventType == "منتدى سياسي")
            {
                // جلب جميع أحداث المنتدى السياسي في الشهر
                var totalEvents = await _context.Events
                    .CountAsync(e => e.EventType == "منتدى سياسي" && e.StartDate.Month == month && e.StartDate.Year == year && e.IsActive);

                var attendedCount = await _context.PoliticalForumAttendances
                    .CountAsync(a => a.UserId == userId && a.Month == month && a.Year == year && a.Attended);

                var score = totalEvents > 0 ? (attendedCount / (double)totalEvents) * 12 : 0;

                var existing = await _context.PoliticalForumAttendances
                    .FirstOrDefaultAsync(a => a.UserId == userId && a.Month == month && a.Year == year);

                if (existing != null)
                {
                    existing.Score = score;
                    _context.PoliticalForumAttendances.Update(existing);
                }
            }
            else if (eventType == "اجتماع دوري")
            {
                var totalEvents = await _context.Events
                    .CountAsync(e => e.EventType == "اجتماع دوري" && e.StartDate.Month == month && e.StartDate.Year == year && e.IsActive);

                var attendedCount = await _context.PeriodicMeetingAttendances
                    .CountAsync(a => a.UserId == userId && a.Month == month && a.Year == year && a.Attended);

                var score = totalEvents > 0 ? (attendedCount / (double)totalEvents) * 12 : 0;

                var existing = await _context.PeriodicMeetingAttendances
                    .FirstOrDefaultAsync(a => a.UserId == userId && a.Month == month && a.Year == year);

                if (existing != null)
                {
                    existing.Score = score;
                    _context.PeriodicMeetingAttendances.Update(existing);
                }
            }

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// جلب المستخدمين المستهدفين للحدث (مطلوب للدالة أعلاه)
        /// </summary>
        private async Task<List<string>> GetTargetUsersForEvent(Event eventItem)
        {
            // جلب الأفراد المصعدين فقط
            var allPromotedUserIds = await _context.Identifies
                .Where(i => i.IsPromoted == true)
                .Select(i => i.UserId)
                .ToListAsync();

            if (!allPromotedUserIds.Any()) return new List<string>();

            // تصفية حسب المحافظة
            var allAddresses = await _context.Addresses.ToListAsync();
            var allProfiles = await _context.Identifies.ToListAsync();

            var userIdsInGovernorate = allAddresses
                .Where(a =>
                {
                    var profile = allProfiles.FirstOrDefault(i => i.UserId == a.UserId);
                    return GetEffectiveGovernorate(profile, a) == eventItem.Governorate &&
                           allPromotedUserIds.Contains(a.UserId);
                })
                .Select(a => a.UserId)
                .ToList();

            if (!userIdsInGovernorate.Any()) return new List<string>();

            // بناء الاستعلام
            var query = _context.Identifies
                .Where(i => userIdsInGovernorate.Contains(i.UserId) && i.IsPromoted == true);

            // فلتر حسب الفئة
            switch (eventItem.TargetCategory)
            {
                case "student":
                    query = query.Where(i => i.Education == "طالب جامعي");
                    break;
                case "employee":
                    query = query.Where(i => i.EmploymentStatus == "موظف");
                    break;
                case "specific_entity":
                    if (eventItem.TargetAffiliationEntityId.HasValue)
                    {
                        var userWithEntity = await _context.AffiliationInfos
                            .Where(a => a.AffiliationEntityId == eventItem.TargetAffiliationEntityId.Value)
                            .Select(a => a.UserId)
                            .ToListAsync();
                        query = query.Where(i => userWithEntity.Contains(i.UserId));
                    }
                    break;
            }

            return await query.Select(i => i.UserId).ToListAsync();
        }

        // ========== دوال مساعدة إضافية ==========

        private async Task<string> GetUserGovernorate(string userId)
        {
            var profile = await _context.Identifies.FirstOrDefaultAsync(i => i.UserId == userId);
            var address = await _context.Addresses.FirstOrDefaultAsync(a => a.UserId == userId);
            return GetEffectiveGovernorate(profile, address) ?? "غير محدد";
        }

        private List<SelectListItem> GetMonthsList()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "1", Text = "يناير" },
                new SelectListItem { Value = "2", Text = "فبراير" },
                new SelectListItem { Value = "3", Text = "مارس" },
                new SelectListItem { Value = "4", Text = "إبريل" },
                new SelectListItem { Value = "5", Text = "مايو" },
                new SelectListItem { Value = "6", Text = "يونيو" },
                new SelectListItem { Value = "7", Text = "يوليو" },
                new SelectListItem { Value = "8", Text = "أغسطس" },
                new SelectListItem { Value = "9", Text = "سبتمبر" },
                new SelectListItem { Value = "10", Text = "أكتوبر" },
                new SelectListItem { Value = "11", Text = "نوفمبر" },
                new SelectListItem { Value = "12", Text = "ديسمبر" }
            };
        }

        private List<SelectListItem> GetYearsList()
        {
            var years = new List<SelectListItem>();
            for (int i = 2020; i <= DateTime.Now.Year + 1; i++)
            {
                years.Add(new SelectListItem { Value = i.ToString(), Text = i.ToString() });
            }
            return years;
        }
    }
}
