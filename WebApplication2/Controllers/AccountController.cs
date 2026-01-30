using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity.UI.Services;
using WebApplication2.Models;
using System.Security.Claims;
using WebApplication2.Data;
using Microsoft.EntityFrameworkCore;

namespace WebApplication2.Controllers
{
    [AllowAnonymous]
    public class AccountController : Controller
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<AccountController> _logger;
        private readonly ApplicationDbContext _context;

        public AccountController(
            SignInManager<IdentityUser> signInManager,
            UserManager<IdentityUser> userManager,
            ILogger<AccountController> logger,
            ApplicationDbContext context)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
            _context = context;
        }

        // GET: /Account/Login
        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            // عرض رسالة ترحيبية للمستخدم الجديد
            if (TempData["NewlyConfirmedUserId"] != null)
            {
                ViewBag.ShowWelcomeMessage = true;
            }

            // عرض رسائل النجاح أو الخطأ
            if (TempData["SuccessMessage"] != null)
            {
                ViewBag.SuccessMessage = TempData["SuccessMessage"];
            }

            if (TempData["ErrorMessage"] != null)
            {
                ViewBag.ErrorMessage = TempData["ErrorMessage"];
            }

            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                ViewBag.ErrorMessage = "يوجد أخطاء في البيانات المدخلة.";
                return View(model);
            }

            try
            {
                var user = await _userManager.FindByEmailAsync(model.Email);

                if (user == null)
                {
                    ModelState.AddModelError("", "البريد الإلكتروني أو كلمة المرور غير صحيحة.");
                    ViewBag.ErrorMessage = "البريد الإلكتروني أو كلمة المرور غير صحيحة.";
                    return View(model);
                }

                // التحقق من تأكيد البريد
                if (!await _userManager.IsEmailConfirmedAsync(user))
                {
                    ModelState.AddModelError("", "يجب تأكيد بريدك الإلكتروني أولاً.");
                    ViewBag.ErrorMessage = "يجب تأكيد بريدك الإلكتروني أولاً.";
                    return View(model);
                }

                var result = await _signInManager.PasswordSignInAsync(
                    model.Email,
                    model.Password,
                    model.RememberMe,
                    lockoutOnFailure: false);

                if (result.Succeeded)
                {
                    _logger.LogInformation("✅ تم تسجيل الدخول: {Email}", model.Email);

                    // 👇 **هنا التعديل المهم: توجيه كل مستخدم حسب دوره**

                    // 1. الحصول على أدوار المستخدم
                    var roles = await _userManager.GetRolesAsync(user);

                    // 2. التحقق من وجود ملف شخصي
                    var userId = user.Id;
                    var existingProfile = await _context.Identifies
                        .Include(i => i.Address)
                        .FirstOrDefaultAsync(i => i.UserId == userId);

                    if (existingProfile == null)
                    {
                        // توجيه لإكمال الملف الشخصي
                        TempData["SuccessMessage"] = "مرحباً بك! يرجى إكمال بياناتك الشخصية.";
                        return RedirectToAction("CompleteProfile", "Register", new { userId = userId });
                    }

                    // 👇 **توجيه كل مستخدم حسب دوره**
                    if (roles.Contains(clsRoles.SuperAdmin))
                    {
                        // السوبر أدمن يذهب إلى إدارة المستخدمين
                        return RedirectToAction("Users", "SuperAdmin");
                    }
                    else if (roles.Contains(clsRoles.Admin))
                    {
                        // الأدمن يذهب إلى صفحة مستخدمي محافظته
                        return RedirectToAction("Users", "Admin");
                    }
                    else
                    {
                        // المستخدم العادي يذهب للصفحة الرئيسية
                        return RedirectToLocal(returnUrl);
                    }
                }

                if (result.IsLockedOut)
                {
                    _logger.LogWarning("⚠️ الحساب مقفل: {Email}", model.Email);
                    return View("Lockout");
                }
                else
                {
                    ModelState.AddModelError("", "محاولة تسجيل دخول غير صحيحة.");
                    ViewBag.ErrorMessage = "البريد الإلكتروني أو كلمة المرور غير صحيحة.";
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ أثناء تسجيل الدخول");
                ModelState.AddModelError("", "حدث خطأ أثناء تسجيل الدخول.");
                ViewBag.ErrorMessage = "حدث خطأ أثناء تسجيل الدخول. يرجى المحاولة مرة أخرى.";
                return View(model);
            }
        }

        // GET: /Account/Logout
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            _logger.LogInformation("✅ تم تسجيل الخروج");
            TempData["SuccessMessage"] = "تم تسجيل الخروج بنجاح.";
            return RedirectToAction("Index", "Home");
        }

        // GET: /Account/ForceCompleteProfile
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> ForceCompleteProfile()
        {
            var userId = _userManager.GetUserId(User);

            var existingProfile = await _context.Identifies
                .FirstOrDefaultAsync(i => i.UserId == userId);

            if (existingProfile != null)
            {
                TempData["InfoMessage"] = "ملفك الشخصي مكتمل بالفعل.";
                return RedirectToAction("Index", "Home");
            }

            TempData["SuccessMessage"] = "يرجى إكمال بياناتك الشخصية.";
            return RedirectToAction("CompleteProfile", "Register", new { userId = userId });
        }

        private IActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }
    }
}