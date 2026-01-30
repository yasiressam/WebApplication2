using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;

namespace WebApplication2.Controllers
{
    [Authorize(Roles = clsRoles.Admin)]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            ILogger<AdminController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        // GET: عرض المستخدمين في محافظة الأدمن فقط
        public async Task<IActionResult> Users()
        {
            try
            {
                var currentUserId = _userManager.GetUserId(User);

                // 1. الحصول على محافظة هذا الأدمن
                var adminProfile = await _context.Identifies
                    .FirstOrDefaultAsync(i => i.UserId == currentUserId);

                if (adminProfile == null || string.IsNullOrEmpty(adminProfile.ManagedGovernorate))
                {
                    ViewBag.ErrorMessage = "❌ لم يتم تعيين محافظة لك.";
                    return View(new List<Identify>());
                }

                var managedGovernorate = adminProfile.ManagedGovernorate;
                ViewBag.ManagedGovernorate = managedGovernorate;

                // 2. جلب المستخدمين في هذه المحافظة فقط
                var usersInGovernorate = await _context.Identifies
                    .Include(i => i.Address)
                    .Include(i => i.User)
                    .Where(i => i.Address.Governorate == managedGovernorate &&
                               i.UserId != currentUserId) // استبعاد نفسه
                    .OrderBy(i => i.FullName)
                    .ToListAsync();

                ViewBag.UserCount = usersInGovernorate.Count;
                return View(usersInGovernorate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في عرض المستخدمين للأدمن");
                ViewBag.ErrorMessage = "حدث خطأ في تحميل البيانات.";
                return View(new List<Identify>());
            }
        }

        // GET: عرض تفاصيل مستخدم
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                var currentUserId = _userManager.GetUserId(User);
                var adminProfile = await _context.Identifies
                    .FirstOrDefaultAsync(i => i.UserId == currentUserId);

                if (adminProfile == null)
                    return NotFound();

                var managedGovernorate = adminProfile.ManagedGovernorate;

                var user = await _context.Identifies
                    .Include(i => i.Address)
                    .Include(i => i.User)
                    .FirstOrDefaultAsync(i => i.Id == id && i.Address.Governorate == managedGovernorate);

                if (user == null)
                {
                    TempData["ErrorMessage"] = "المستخدم غير موجود أو ليس في محافظتك.";
                    return RedirectToAction(nameof(Users));
                }

                return View(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في عرض تفاصيل المستخدم");
                TempData["ErrorMessage"] = "حدث خطأ في تحميل البيانات.";
                return RedirectToAction(nameof(Users));
            }
        }

        // GET: إحصائيات
        public async Task<IActionResult> Dashboard()
        {
            var currentUserId = _userManager.GetUserId(User);
            var adminProfile = await _context.Identifies
                .FirstOrDefaultAsync(i => i.UserId == currentUserId);

            if (adminProfile == null || string.IsNullOrEmpty(adminProfile.ManagedGovernorate))
            {
                ViewBag.ErrorMessage = "لم يتم تعيين محافظة لك.";
                return View();
            }

            var managedGovernorate = adminProfile.ManagedGovernorate;

            var stats = new
            {
                TotalUsers = await _context.Identifies
                    .CountAsync(i => i.Address.Governorate == managedGovernorate),

                MaleCount = await _context.Identifies
                    .CountAsync(i => i.Address.Governorate == managedGovernorate && i.Gender == "ذكر"),

                FemaleCount = await _context.Identifies
                    .CountAsync(i => i.Address.Governorate == managedGovernorate && i.Gender == "أنثى"),

                TodayRegistered = await _context.Identifies
                    .CountAsync(i => i.Address.Governorate == managedGovernorate &&
                                    i.Date.Date == DateTime.Today)
            };

            ViewBag.Stats = stats;
            ViewBag.ManagedGovernorate = managedGovernorate;

            return View();
        }
    }
}