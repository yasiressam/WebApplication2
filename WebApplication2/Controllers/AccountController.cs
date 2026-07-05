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
            return RedirectToIdentityLogin(returnUrl);
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(LoginViewModel model, string returnUrl = null)
        {
            return RedirectToIdentityLogin(returnUrl);
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

        private IActionResult RedirectToIdentityLogin(string? returnUrl = null)
        {
            var loginUrl = "/Identity/Account/Login";
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                loginUrl += "?returnUrl=" + Uri.EscapeDataString(returnUrl);
            }

            return LocalRedirect(loginUrl);
        }

        // =============== دوال إنشاء المستخدم من قبل الأدمن ===============

        // GET: /Account/CreateUserByAdmin
        [HttpGet]
        [Authorize(Roles = clsRoles.SuperAdmin + "," + clsRoles.Admin + "," + clsRoles.DistrictAdmin)]
        public IActionResult CreateUserByAdmin()
        {
            var model = new RegisterViewModel
            {
                RegisterMethod = "WhatsApp"
            };
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
            var registerMethod = string.Equals(model.RegisterMethod, "WhatsApp", StringComparison.OrdinalIgnoreCase)
                ? "WhatsApp"
                : "Email";

            if (registerMethod == "WhatsApp")
            {
                ModelState.Remove(nameof(RegisterViewModel.Email));
                if (string.IsNullOrWhiteSpace(model.PhoneNumber))
                {
                    ModelState.AddModelError(nameof(RegisterViewModel.PhoneNumber), "رقم الواتساب مطلوب.");
                }
            }
            else
            {
                ModelState.Remove(nameof(RegisterViewModel.PhoneNumber));
                if (string.IsNullOrWhiteSpace(model.Email))
                {
                    ModelState.AddModelError(nameof(RegisterViewModel.Email), "البريد الإلكتروني مطلوب.");
                }
            }

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

                IdentityUser? existingUser = null;
                if (registerMethod == "Email")
                {
                    existingUser = await _userManager.FindByEmailAsync(model.Email);
                }

                if (existingUser != null)
                {
                    ModelState.AddModelError("Email", "البريد الإلكتروني مسجل مسبقاً.");
                    ViewBag.Governorates = GetGovernorates();
                    ViewBag.BaghdadDistricts = new List<string> { "الكرخ", "الرصافة" };
                    return View(model);
                }

                var normalizedPhone = string.Empty;
                var localPhone = string.Empty;
                if (registerMethod == "WhatsApp")
                {
                    normalizedPhone = NormalizeIraqPhoneNumber(model.PhoneNumber);
                    if (string.IsNullOrWhiteSpace(normalizedPhone) || normalizedPhone.Length < 10)
                    {
                        ModelState.AddModelError(nameof(RegisterViewModel.PhoneNumber), "رقم الواتساب غير صحيح.");
                        ViewBag.Governorates = GetGovernorates();
                        ViewBag.BaghdadDistricts = new List<string> { "الكرخ", "الرصافة" };
                        return View(model);
                    }

                    var existingPhoneNumbers = await _context.Identifies
                        .Where(i => !string.IsNullOrWhiteSpace(i.WhatsAppNumber) || !string.IsNullOrWhiteSpace(i.PhoneNumber))
                        .Select(i => new { i.WhatsAppNumber, i.PhoneNumber })
                        .ToListAsync();

                    var phoneAlreadyExists = existingPhoneNumbers.Any(i =>
                        NormalizeIraqPhoneNumber(i.WhatsAppNumber ?? string.Empty) == normalizedPhone ||
                        NormalizeIraqPhoneNumber(i.PhoneNumber ?? string.Empty) == normalizedPhone);

                    if (phoneAlreadyExists || await _userManager.FindByNameAsync(normalizedPhone) != null)
                    {
                        ModelState.AddModelError(nameof(RegisterViewModel.PhoneNumber), "رقم الواتساب مسجل مسبقاً.");
                        ViewBag.Governorates = GetGovernorates();
                        ViewBag.BaghdadDistricts = new List<string> { "الكرخ", "الرصافة" };
                        return View(model);
                    }

                    localPhone = ToLocalIraqPhoneNumber(normalizedPhone);
                }

                var user = registerMethod == "WhatsApp"
                    ? new IdentityUser
                    {
                        UserName = normalizedPhone,
                        Email = $"{normalizedPhone}@whatsapp.local",
                        PhoneNumber = localPhone,
                        EmailConfirmed = true,
                        PhoneNumberConfirmed = true
                    }
                    : new IdentityUser
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
                            PhoneNumber = registerMethod == "WhatsApp" ? localPhone : "",
                            WhatsAppNumber = registerMethod == "WhatsApp" ? normalizedPhone : "",
                            IsWhatsAppVerified = registerMethod == "WhatsApp",
                            WhatsAppVerifiedAt = registerMethod == "WhatsApp" ? DateTime.UtcNow : null,
                            IdentityCardN = "",
                            identityDate = DateTime.Now,
                            CreatedAt = DateTime.UtcNow,
                            AccountType = "عادي",
                            IsPromoted = false,
                            Email = registerMethod == "Email" ? (user.Email ?? "") : "",
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
                            PhoneNumber = registerMethod == "WhatsApp" ? localPhone : "",
                            WhatsAppNumber = registerMethod == "WhatsApp" ? normalizedPhone : "",
                            IsWhatsAppVerified = registerMethod == "WhatsApp",
                            WhatsAppVerifiedAt = registerMethod == "WhatsApp" ? DateTime.UtcNow : null,
                            IdentityCardN = "",
                            identityDate = DateTime.Now,
                            CreatedAt = DateTime.UtcNow,
                            AccountType = "عادي",
                            IsPromoted = false,
                            Email = registerMethod == "Email" ? (user.Email ?? "") : "",
                            IsBasicInfoApproved = false,
                            WorkGovernorate = adminGovernorate,
                            WorkDistrict = null
                        };
                        _context.Identifies.Add(identify);
                        await _context.SaveChangesAsync();

                        _logger.LogInformation($"✅ تم تعيين محافظة العمل التنظيمي للمستخدم {user.Email} إلى {adminGovernorate}");
                    }

                    var createdIdentifier = registerMethod == "WhatsApp"
                        ? localPhone
                        : model.Email;

                    _logger.LogInformation($"✅ تم إنشاء مستخدم جديد من قبل الأدمن: {createdIdentifier} بدور {roleToAssign}");

                    TempData["SuccessMessage"] = $"✅ تم إنشاء المستخدم '{createdIdentifier}' بنجاح. " +
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
                return RedirectToIdentityLogin();
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

        private static string ToLocalIraqPhoneNumber(string phoneNumber)
        {
            var normalizedPhone = NormalizeIraqPhoneNumber(phoneNumber);
            if (normalizedPhone.StartsWith("9647", StringComparison.Ordinal))
            {
                return "0" + normalizedPhone[3..];
            }

            return phoneNumber;
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
