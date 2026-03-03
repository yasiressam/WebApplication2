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

                // 2. جلب جميع المستخدمين في هذه المحافظة
                var usersInGovernorate = await _context.Identifies
                    .Include(i => i.Address)
                    .Include(i => i.User)
                    .Where(i => i.Address.Governorate == managedGovernorate &&
                               i.UserId != currentUserId) // استبعاد نفسه
                    .OrderBy(i => i.FullName)
                    .ToListAsync();

                // 3. استبعاد المستخدمين الذين لديهم دور Admin أو SuperAdmin
                var filteredUsers = new List<Identify>();

                foreach (var user in usersInGovernorate)
                {
                    if (user.User != null)
                    {
                        var roles = await _userManager.GetRolesAsync(user.User);
                        // استبعاد إذا كان المستخدم Admin أو SuperAdmin
                        if (!roles.Contains(clsRoles.Admin) && !roles.Contains(clsRoles.SuperAdmin))
                        {
                            filteredUsers.Add(user);
                        }
                    }
                    else
                    {
                        // إذا لم يكن للمستخدم User (حالة نادرة)، نضيفه
                        filteredUsers.Add(user);
                    }
                }

                ViewBag.UserCount = filteredUsers.Count;
                return View(filteredUsers);
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

                // التحقق من أن المستخدم ليس Admin أو SuperAdmin
                if (user.User != null)
                {
                    var roles = await _userManager.GetRolesAsync(user.User);
                    if (roles.Contains(clsRoles.Admin) || roles.Contains(clsRoles.SuperAdmin))
                    {
                        TempData["ErrorMessage"] = "لا يمكن عرض تفاصيل مشرف آخر.";
                        return RedirectToAction(nameof(Users));
                    }
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

            // جلب جميع المستخدمين في المحافظة
            var usersInGovernorate = await _context.Identifies
                .Include(i => i.User)
                .Where(i => i.Address.Governorate == managedGovernorate)
                .ToListAsync();

            // استبعاد الأدمنية والسوبر أدمنية من الإحصائيات
            int totalUsers = 0;
            int maleCount = 0;
            int femaleCount = 0;
            int todayRegistered = 0;

            foreach (var user in usersInGovernorate)
            {
                if (user.User != null)
                {
                    var roles = await _userManager.GetRolesAsync(user.User);
                    if (!roles.Contains(clsRoles.Admin) && !roles.Contains(clsRoles.SuperAdmin))
                    {
                        totalUsers++;

                        if (user.Gender == "ذكر")
                            maleCount++;
                        else if (user.Gender == "أنثى")
                            femaleCount++;

                        if (user.Date.Date == DateTime.Today)
                            todayRegistered++;
                    }
                }
            }

            var stats = new
            {
                TotalUsers = totalUsers,
                MaleCount = maleCount,
                FemaleCount = femaleCount,
                TodayRegistered = todayRegistered
            };

            ViewBag.Stats = stats;
            ViewBag.ManagedGovernorate = managedGovernorate;

            return View();
        }

        // POST: حذف مستخدم
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser([FromBody] DeleteUserRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.UserId))
                    return Json(new { success = false, message = "معرف المستخدم مطلوب" });

                var currentUserId = _userManager.GetUserId(User);

                // الحصول على محافظة الأدمن الحالي
                var adminProfile = await _context.Identifies
                    .FirstOrDefaultAsync(i => i.UserId == currentUserId);

                if (adminProfile == null || string.IsNullOrEmpty(adminProfile.ManagedGovernorate))
                {
                    return Json(new { success = false, message = "لم يتم تعيين محافظة لك" });
                }

                var managedGovernorate = adminProfile.ManagedGovernorate;

                // البحث عن المستخدم في قاعدة البيانات
                var user = await _userManager.FindByIdAsync(request.UserId);
                if (user == null)
                    return Json(new { success = false, message = "المستخدم غير موجود" });

                // التحقق من أن المستخدم في نفس محافظة الأدمن
                var userProfile = await _context.Identifies
                    .Include(i => i.Address)
                    .FirstOrDefaultAsync(i => i.UserId == request.UserId);

                if (userProfile == null || userProfile.Address == null || userProfile.Address.Governorate != managedGovernorate)
                {
                    return Json(new { success = false, message = "لا يمكنك حذف مستخدم خارج محافظتك" });
                }

                // التحقق من أن المستخدم ليس Admin أو SuperAdmin
                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains(clsRoles.Admin) || roles.Contains(clsRoles.SuperAdmin))
                {
                    return Json(new { success = false, message = "لا يمكن حذف مشرف" });
                }

                // حذف العنوان إذا كان موجوداً
                if (userProfile.Address != null)
                {
                    _context.Addresses.Remove(userProfile.Address);
                }

                // حذف الملف الشخصي
                _context.Identifies.Remove(userProfile);
                await _context.SaveChangesAsync();

                // حذف المستخدم من نظام Identity
                var result = await _userManager.DeleteAsync(user);

                if (result.Succeeded)
                {
                    return Json(new
                    {
                        success = true,
                        message = "✅ تم حذف المستخدم وجميع بياناته بنجاح"
                    });
                }

                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return Json(new { success = false, message = $"❌ فشل في حذف المستخدم: {errors}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في حذف المستخدم");
                return Json(new { success = false, message = $"❌ حدث خطأ: {ex.Message}" });
            }
        }
    }

    public class DeleteUserRequest
    {
        public string UserId { get; set; }
    }
}