using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WebApplication2.Data;
using WebApplication2.Models;
using static Microsoft.CodeAnalysis.CSharp.SyntaxTokenParser;

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
            ApplicationDbContext context,
            ILogger<AccountController> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
            _logger = logger;
        }

        // ===== دوال مساعدة لجلب البيانات المرتبطة باستخدام DbContext مباشرة =====
        private async Task<Address?> GetUserAddressAsync(string userId)
        {
            return await _context.Addresses
                .FirstOrDefaultAsync(a => a.UserId == userId);
        }

        private async Task<VoterCard?> GetUserVoterCardAsync(string userId)
        {
            return await _context.VoterCards
                .FirstOrDefaultAsync(v => v.UserId == userId);
        }

        private async Task<Identify?> GetUserProfileAsync(string userId)
        {
            return await _context.Identifies
                .FirstOrDefaultAsync(i => i.UserId == userId);
        }

        // GET: /Account/Login
        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (TempData["NewlyConfirmedUserId"] != null)
            {
                ViewBag.ShowWelcomeMessage = true;
            }

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
                var loginIdentifier = model.Email.Trim();
                var loginByPhone = !loginIdentifier.Contains('@');

                IdentityUser? user;
                if (loginByPhone)
                {
                    var normalizedPhone = NormalizeIraqPhoneNumber(loginIdentifier);
                    var identify = await _context.Identifies
                        .FirstOrDefaultAsync(i =>
                            i.IsWhatsAppVerified &&
                            i.WhatsAppNumber == normalizedPhone);

                    user = identify != null
                        ? await _userManager.FindByIdAsync(identify.UserId)
                        : null;
                }
                else
                {
                    user = await _userManager.FindByEmailAsync(loginIdentifier);
                }

                if (user == null)
                {
                    ModelState.AddModelError("", "بيانات الدخول أو كلمة المرور غير صحيحة.");
                    ViewBag.ErrorMessage = "بيانات الدخول أو كلمة المرور غير صحيحة.";
                    return View(model);
                }

                var result = await _signInManager.PasswordSignInAsync(
                    user.UserName ?? loginIdentifier,
                    model.Password,
                    model.RememberMe,
                    lockoutOnFailure: true);

                if (result.Succeeded)
                {
                    _logger.LogInformation("✅ تم تسجيل الدخول: {LoginIdentifier}", loginIdentifier);

                    var roles = await _userManager.GetRolesAsync(user);
                    var userId = user.Id;

                    if (roles.Contains(clsRoles.SuperAdmin))
                    {
                        return RedirectToAction("Users", "SuperAdmin");
                    }
                    else if (roles.Contains(clsRoles.Admin) || roles.Contains(clsRoles.DistrictAdmin))
                    {
                        return RedirectToAction("Users", "Admin");
                    }
                    else if (roles.Contains(clsRoles.Manager))
                    {
                        return RedirectToAction("PromotionRequests", "ManagerReview");
                    }

                    var profile = await _context.Identifies
                        .FirstOrDefaultAsync(i => i.UserId == userId);

                    var address = await GetUserAddressAsync(userId);
                    var voterCard = await GetUserVoterCardAsync(userId);

                    if (profile == null)
                    {
                        profile = new Identify
                        {
                            UserId = userId,
                            CreatedAt = DateTime.UtcNow,
                            AccountType = "عادي",
                            IsPromoted = false,
                            Email = user.Email,
                            FullName = "",
                            MotherName = "",
                            PhoneNumber = "",
                            IdentityCardN = "",
                            Date = DateTime.Now,
                            IsBasicInfoApproved = false
                        };
                        _context.Identifies.Add(profile);
                        await _context.SaveChangesAsync();

                        TempData["InfoMessage"] = "مرحباً بك! يرجى إكمال بياناتك الأساسية أولاً.";
                        return RedirectToAction("BasicInfo", "Register");
                    }

                    // ✅ التحقق من البيانات الأساسية (بعد التعديل)
                    if (string.IsNullOrWhiteSpace(profile.FullName) ||
                        string.IsNullOrWhiteSpace(profile.MotherName) ||
                        string.IsNullOrWhiteSpace(profile.IdentityCardN) ||
                        string.IsNullOrWhiteSpace(profile.PhoneNumber) ||
                        string.IsNullOrWhiteSpace(profile.Gender))
                    {
                        TempData["InfoMessage"] = "يرجى إكمال بياناتك الأساسية أولاً.";
                        return RedirectToAction("BasicInfo", "Register");
                    }

                    if (string.IsNullOrWhiteSpace(profile.WorkGovernorate))
                    {
                        TempData["InfoMessage"] = "يرجى إكمال بيانات محافظة العمل التنظيمي أولاً.";
                        return RedirectToAction("BasicInfo", "Register");
                    }

                    if (!profile.IsBasicInfoApproved)
                    {
                        TempData["InfoMessage"] = "بياناتك الأساسية قيد المراجعة. سيتم إشعارك عند الموافقة.";
                        return RedirectToAction("BasicInfoPending", "Register");
                    }

                    if (!IsAdditionalInfoComplete(profile, voterCard))
                    {
                        TempData["InfoMessage"] = "الرجاء إكمال البيانات الإضافية.";
                        return RedirectToAction("AdditionalInfo", "Register");
                    }

                    if (profile.RequestedPromotion && !profile.IsPromoted)
                    {
                        TempData["InfoMessage"] = "طلب الترقية الخاص بك قيد المراجعة.";
                        return RedirectToAction("PromotionPending", "Register");
                    }

                    return RedirectToLocal(returnUrl);
                }

                if (result.IsLockedOut)
                {
                    _logger.LogWarning("⚠️ الحساب مقفل: {Email}", model.Email);
                    return View("Lockout");
                }
                else
                {
                    ModelState.AddModelError("", "محاولة تسجيل دخول غير صحيحة.");
                    ViewBag.ErrorMessage = "بيانات الدخول أو كلمة المرور غير صحيحة.";
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

        // POST: /Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
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

        // =============== دوال إنشاء المستخدم من قبل الأدمن ===============

        // GET: /Account/CreateUserByAdmin
        [HttpGet]
        [Authorize(Roles = clsRoles.SuperAdmin + "," + clsRoles.Admin + "," + clsRoles.DistrictAdmin)]
        public IActionResult CreateUserByAdmin()
        {
            var model = new RegisterViewModel();
            ViewBag.Governorates = GetGovernorates();
            ViewBag.BaghdadDistricts = new List<string> { "الكرخ", "الرصافة" };
            return View(model);
        }

        // POST: /Account/CreateUserByAdmin
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = clsRoles.SuperAdmin + "," + clsRoles.Admin + "," + clsRoles.DistrictAdmin)]
        public async Task<IActionResult> CreateUserByAdmin(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Governorates = GetGovernorates();
                ViewBag.BaghdadDistricts = new List<string> { "الكرخ", "الرصافة" };
                return View(model);
            }

            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                var currentUserRoles = await _userManager.GetRolesAsync(currentUser);

                var roleToAssign = ResolveAllowedRole(model.Role, currentUserRoles);
                if (roleToAssign == null)
                {
                    ModelState.AddModelError("", "لا تملك صلاحية إنشاء هذا النوع من المستخدمين.");
                    ViewBag.Governorates = GetGovernorates();
                    ViewBag.BaghdadDistricts = new List<string> { "الكرخ", "الرصافة" };
                    return View(model);
                }

                var existingUser = await _userManager.FindByEmailAsync(model.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError("Email", "البريد الإلكتروني مسجل مسبقاً.");
                    ViewBag.Governorates = GetGovernorates();
                    ViewBag.BaghdadDistricts = new List<string> { "الكرخ", "الرصافة" };
                    return View(model);
                }

                var user = new IdentityUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    EmailConfirmed = true
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, roleToAssign);

                    var adminProfile = await _context.Identifies
                        .FirstOrDefaultAsync(i => i.UserId == currentUser.Id);

                    string adminGovernorate = adminProfile?.ManagedGovernorate ?? "بغداد";

                    if (roleToAssign == clsRoles.Admin)
                    {
                        if (string.IsNullOrEmpty(model.ManagedGovernorate))
                        {
                            ModelState.AddModelError("ManagedGovernorate", "يجب تحديد المحافظة للأدمن");
                            await _userManager.DeleteAsync(user);
                            ViewBag.Governorates = GetGovernorates();
                            ViewBag.BaghdadDistricts = new List<string> { "الكرخ", "الرصافة" };
                            return View(model);
                        }

                        var identify = new Identify
                        {
                            UserId = user.Id,
                            FullName = "مدير محافظة",
                            ManagedGovernorate = model.ManagedGovernorate,
                            ManagedDistrict = null,
                            Date = DateTime.Now,
                            Gender = "ذكر",
                            PhoneNumber = "",
                            IdentityCardN = "",
                            identityDate = DateTime.Now,
                            CreatedAt = DateTime.UtcNow,
                            AccountType = "عادي",
                            IsPromoted = false,
                            Email = user.Email,
                            IsBasicInfoApproved = false
                        };
                        _context.Identifies.Add(identify);
                        await _context.SaveChangesAsync();

                        _logger.LogInformation($"✅ تم إنشاء أدمن لمحافظة: {model.ManagedGovernorate}");
                    }
                    else
                    {
                        var identify = new Identify
                        {
                            UserId = user.Id,
                            FullName = "",
                            Date = DateTime.Now,
                            Gender = "",
                            PhoneNumber = "",
                            IdentityCardN = "",
                            identityDate = DateTime.Now,
                            CreatedAt = DateTime.UtcNow,
                            AccountType = "عادي",
                            IsPromoted = false,
                            Email = user.Email,
                            IsBasicInfoApproved = false,
                            WorkGovernorate = adminGovernorate,
                            WorkDistrict = null
                        };
                        _context.Identifies.Add(identify);
                        await _context.SaveChangesAsync();

                        _logger.LogInformation($"✅ تم تعيين محافظة العمل التنظيمي للمستخدم {user.Email} إلى {adminGovernorate}");
                    }

                    _logger.LogInformation($"✅ تم إنشاء مستخدم جديد من قبل الأدمن: {model.Email} بدور {roleToAssign}");

                    TempData["SuccessMessage"] = $"✅ تم إنشاء المستخدم '{model.Email}' بنجاح. " +
                        (roleToAssign == clsRoles.Admin ? $"المحافظة: {model.ManagedGovernorate}" : $"محافظة: {adminGovernorate}");

                    if (User.IsInRole(clsRoles.SuperAdmin))
                    {
                        return RedirectToAction("Users", "SuperAdmin");
                    }
                    else
                    {
                        return RedirectToAction("Users", "Admin");
                    }
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ أثناء إنشاء مستخدم من قبل الأدمن");
                ModelState.AddModelError("", "حدث خطأ أثناء إنشاء المستخدم.");
            }

            ViewBag.Governorates = GetGovernorates();
            return View(model);
        }

        // GET: /Account/MyProfile
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> MyProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login");
            }

            var userId = user.Id;

            var profile = await _context.Identifies
                .FirstOrDefaultAsync(i => i.UserId == userId);

            var address = await GetUserAddressAsync(userId);
            var roles = await _userManager.GetRolesAsync(user);

            if (profile == null)
            {
                TempData["InfoMessage"] = "يرجى إكمال ملفك الشخصي أولاً";
                return RedirectToAction("BasicInfo", "Register");
            }

            var viewModel = new
            {
                Email = user.Email,
                PhoneNumber = user.PhoneNumber ?? profile.PhoneNumber,
                FullName = profile.FullName,
                Governorate = profile.WorkGovernorate ?? "غير محدد",
                Role = roles.FirstOrDefault() ?? "User",
                RegistrationDate = profile.Date,
                IsProfileComplete = true
            };

            return View(viewModel);
        }

        #region ========== دوال مساعدة للتحقق من اكتمال الملف ==========

        private bool IsBasicInfoComplete(Identify profile, Address? address)
        {
            if (profile == null) return false;

            if (string.IsNullOrWhiteSpace(profile.FullName)) return false;
            if (string.IsNullOrWhiteSpace(profile.MotherName)) return false;
            if (profile.Date == null || profile.Date == DateTime.MinValue) return false;
            if (string.IsNullOrWhiteSpace(profile.Gender)) return false;
            if (string.IsNullOrWhiteSpace(profile.PhoneNumber)) return false;
            if (string.IsNullOrWhiteSpace(profile.IdentityCardN)) return false;
            if (profile.IdentityCardN.Length != 12) return false;
            if (string.IsNullOrWhiteSpace(profile.WorkGovernorate)) return false;

            return true;
        }

        private static string NormalizeIraqPhoneNumber(string phoneNumber)
        {
            var digits = new string((phoneNumber ?? string.Empty).Where(char.IsDigit).ToArray());
            if (string.IsNullOrWhiteSpace(digits))
                return string.Empty;

            if (digits.StartsWith("00", StringComparison.Ordinal))
                digits = digits[2..];

            if (digits.StartsWith("964", StringComparison.Ordinal))
                return digits;

            if (digits.StartsWith("0", StringComparison.Ordinal))
                return "964" + digits[1..];

            if (digits.StartsWith("7", StringComparison.Ordinal))
                return "964" + digits;

            return digits;
        }

        private bool IsAdditionalInfoComplete(Identify profile, VoterCard? voterCard)
        {
            if (profile == null) return false;

            if (voterCard == null) return false;
            if (string.IsNullOrWhiteSpace(voterCard.VoterCardNumber)) return false;

            return true;
        }

        #endregion

        #region ========== قوائم البيانات الثابتة ==========

        private List<string> GetGovernorates()
        {
            return new List<string>
            {
                "بغداد", "الأنبار", "بابل", "البصرة", "ذي قار", "القادسية",
                "ديالى", "دهوك", "أربيل", "كربلاء", "كركوك", "ميسان",
                "المثنى", "النجف", "نينوى", "صلاح الدين", "السليمانية", "واسط"
            };
        }

        private string? ResolveAllowedRole(string? requestedRole, IList<string> currentUserRoles)
        {
            var normalizedRole = string.IsNullOrWhiteSpace(requestedRole)
                ? clsRoles.User
                : requestedRole.Trim();

            if (currentUserRoles.Contains(clsRoles.SuperAdmin))
            {
                var superAdminAllowedRoles = new HashSet<string>
                {
                    clsRoles.SuperAdmin,
                    clsRoles.Admin,
                    clsRoles.DistrictAdmin,
                    clsRoles.User,
                    clsRoles.Member,
                    clsRoles.NewsEditor,
                    clsRoles.MapViewer,
                    clsRoles.Manager,
                    clsRoles.AssistantManager
                };

                return superAdminAllowedRoles.Contains(normalizedRole) ? normalizedRole : null;
            }

            if (currentUserRoles.Contains(clsRoles.Admin) || currentUserRoles.Contains(clsRoles.DistrictAdmin))
            {
                return normalizedRole == clsRoles.User ? clsRoles.User : null;
            }

            return null;
        }

        #endregion
    }
}
