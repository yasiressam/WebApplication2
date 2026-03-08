using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;
using System.Net;
using System.Diagnostics;

namespace WebApplication2.Controllers
{
    [AllowAnonymous]
    public class RegisterController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<RegisterController> _logger;

        public RegisterController(
            UserManager<IdentityUser> userManager,
            IEmailSender emailSender,
            ApplicationDbContext context,
            ILogger<RegisterController> logger)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _context = context;
            _logger = logger;
        }

        // GET: /Register
        [HttpGet]
        public IActionResult Index()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View(new RegisterViewModel());
        }

        // POST: /Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var existingUser = await _userManager.FindByEmailAsync(model.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError("Email", "هذا البريد الإلكتروني مسجل مسبقاً");
                    return View(model);
                }

                var user = new IdentityUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    EmailConfirmed = false
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    _logger.LogInformation("✅ تم إنشاء حساب جديد: {Email}", model.Email);

                    await _userManager.AddToRoleAsync(user, clsRoles.User);

                    var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    var encodedToken = WebUtility.UrlEncode(token);
                    var loginUrl = Url.Action("Login", "Account", null,
                        protocol: Request.Scheme, host: Request.Host.Value);
                    var callbackUrl = Url.Action(
                        "ConfirmEmail",
                        "Register",
                        new { userId = user.Id, token = encodedToken, returnUrl = loginUrl },
                        protocol: Request.Scheme,
                        host: Request.Host.Value);

                    await _emailSender.SendEmailAsync(
                        model.Email,
                        "تأكيد حسابك في موقع الأخبار",
                        $@"
                        <div dir='rtl' style='font-family: Arial, sans-serif; padding: 20px;'>
                            <h2 style='color: #007bff;'>مرحباً بك في موقع الأخبار!</h2>
                            <p>شكراً لتسجيلك في موقعنا. لتأكيد حسابك، يرجى الضغط على الرابط أدناه:</p>
                            <p style='text-align: center; margin: 30px 0;'>
                                <a href='{callbackUrl}' 
                                   style='background-color: #28a745; 
                                          color: white; 
                                          padding: 12px 24px; 
                                          text-decoration: none; 
                                          border-radius: 5px;
                                          display: inline-block;
                                          font-weight: bold;'>
                                    تأكيد حسابي
                                </a>
                            </p>
                            <p>بعد التأكيد، سيتم توجيهك إلى صفحة تسجيل الدخول.</p>
                            <hr>
                            <p style='color: #6c757d; font-size: 12px;'>
                                إذا لم تطلب هذا التسجيل، يرجى تجاهل هذا الإيميل.
                            </p>
                        </div>"
                    );

                    TempData["UserEmail"] = model.Email;
                    return RedirectToAction("CheckEmail");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                    _logger.LogError("❌ خطأ في التسجيل: {Error}", error.Description);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ أثناء التسجيل");
                ModelState.AddModelError("", "حدث خطأ. حاول مرة أخرى.");
            }

            return View(model);
        }

        // GET: /Register/CheckEmail
        [HttpGet]
        public IActionResult CheckEmail()
        {
            var email = TempData["UserEmail"] as string;
            if (string.IsNullOrEmpty(email))
                return RedirectToAction("Index");

            ViewBag.Email = email;
            return View();
        }

        // GET: /Register/ConfirmEmail
        [HttpGet]
        public async Task<IActionResult> ConfirmEmail(string userId, string token, string returnUrl = null)
        {
            _logger.LogInformation("🔍 تأكيد البريد: {UserId}", userId);

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
            {
                TempData["ErrorMessage"] = "رابط التأكيد غير صالح.";
                return RedirectToAction("Index", "Home");
            }

            try
            {
                var decodedToken = WebUtility.UrlDecode(token);
                var user = await _userManager.FindByIdAsync(userId);

                if (user == null)
                {
                    TempData["ErrorMessage"] = "المستخدم غير موجود.";
                    return RedirectToAction("Index", "Home");
                }

                if (user.EmailConfirmed)
                {
                    TempData["SuccessMessage"] = "حسابك مؤكد بالفعل. يمكنك تسجيل الدخول الآن.";
                    return RedirectToAction("Login", "Account");
                }

                var result = await _userManager.ConfirmEmailAsync(user, decodedToken);

                if (result.Succeeded)
                {
                    _logger.LogInformation("✅ تم تأكيد البريد: {Email}", user.Email);

                    TempData["SuccessMessage"] = "✅ تم تأكيد حسابك بنجاح! يرجى تسجيل الدخول الآن.";
                    TempData["NewlyConfirmedUserId"] = user.Id;

                    return RedirectToAction("Login", "Account");
                }
                else
                {
                    _logger.LogError("❌ فشل تأكيد البريد");
                    TempData["ErrorMessage"] = "فشل تأكيد البريد الإلكتروني.";
                    return RedirectToAction("Index", "Home");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ أثناء تأكيد البريد");
                TempData["ErrorMessage"] = "حدث خطأ أثناء تأكيد البريد.";
                return RedirectToAction("Index", "Home");
            }
        }

        // GET: /Register/CompleteProfile
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> CompleteProfile()
        {
            var userId = _userManager.GetUserId(User);
            _logger.LogInformation("🔍 طلب إكمال الملف الشخصي: {UserId}", userId);

            if (string.IsNullOrEmpty(userId))
            {
                TempData["ErrorMessage"] = "يجب تسجيل الدخول أولاً.";
                return RedirectToAction("Login", "Account");
            }

            var user = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(user);

            var existingProfile = await _context.Identifies
                .Include(i => i.Address)
                .FirstOrDefaultAsync(i => i.UserId == userId);

            if (existingProfile != null)
            {
                return RedirectToAction("ProfileDetails");
            }

            var newModel = new PersonalProfileViewModel
            {
                UserId = userId,
                FullName = "",
                DateOfBirth = DateTime.Now.AddYears(-20),
                Gender = "ذكر",
                PhoneNumber = "",
                IdentityCardN = 100000,
                IdentityDate = DateTime.Now,
                Governorate = "بغداد",
                RationN = 0,
                RationCenter = 0,

                Email = user.Email,
                UserRole = roles.FirstOrDefault() ?? "User",
                IsEmailConfirmed = user.EmailConfirmed,

                Governorates = GetGovernorates(),
                Genders = new List<string> { "ذكر", "أنثى" },
                Educations = GetEducations()
            };

            return View(newModel);
        }


        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteProfile(PersonalProfileViewModel model)
        {
            _logger.LogInformation("=== 🚀 بدء حفظ الملف الشخصي الكامل ===");

            // تحميل القوائم
            model.Governorates = GetGovernorates();
            model.Genders = new List<string> { "ذكر", "أنثى" };
            model.Educations = GetEducations();

            // إزالة التحقق من جميع الحقول
            ModelState.Clear(); // هذا يزيل كل التحقق

            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "لم يتم العثور على المستخدم";
                    return RedirectToAction("Login", "Account");
                }

                // التأكد من أن القيم الرقمية ليست null
                model.RationN = model.RationN ?? 0;
                model.RationCenter = model.RationCenter ?? 0;

                var existingIdentify = await _context.Identifies
                    .Include(i => i.Address)
                    .FirstOrDefaultAsync(i => i.UserId == model.UserId);

                if (existingIdentify != null)
                {
                    // تحديث البيانات الموجودة
                    existingIdentify.FullName = model.FullName ?? "";
                    existingIdentify.LastName = model.LastName ?? "";
                    existingIdentify.MotherName = model.MotherName ?? "";
                    existingIdentify.Date = model.DateOfBirth;
                    existingIdentify.Gender = model.Gender ?? "ذكر";
                    existingIdentify.MozakeName = model.MozakeName ?? "";
                    existingIdentify.Education = model.Education ?? "";
                    existingIdentify.Specialization = model.Specialization ?? "";
                    existingIdentify.PhoneNumber = model.PhoneNumber ?? "";
                    existingIdentify.IdentityCardN = model.IdentityCardN;
                    existingIdentify.identityDate = model.IdentityDate;
                    existingIdentify.RationN = model.RationN;
                    existingIdentify.RationCenter = model.RationCenter;

                    if (existingIdentify.Address != null)
                    {
                        existingIdentify.Address.Governorate = model.Governorate ?? "بغداد";
                        existingIdentify.Address.District = model.District ?? "";
                        existingIdentify.Address.SubDistrict = model.SubDistrict ?? "";
                        existingIdentify.Address.Alley = model.Alley ?? "";
                        existingIdentify.Address.Street = model.Street ?? "";
                        existingIdentify.Address.House = model.House ?? "";
                        existingIdentify.Address.NearestPoint = model.NearestPoint ?? "";
                    }
                    else
                    {
                        var address = new Address
                        {
                            Governorate = model.Governorate ?? "بغداد",
                            District = model.District ?? "",
                            SubDistrict = model.SubDistrict ?? "",
                            Alley = model.Alley ?? "",
                            Street = model.Street ?? "",
                            House = model.House ?? "",
                            NearestPoint = model.NearestPoint ?? ""
                        };
                        _context.Addresses.Add(address);
                        await _context.SaveChangesAsync();
                        existingIdentify.AddressId = address.Id;
                    }

                    _context.Identifies.Update(existingIdentify);
                }
                else
                {
                    // إنشاء بيانات جديدة
                    var address = new Address
                    {
                        Governorate = model.Governorate ?? "بغداد",
                        District = model.District ?? "",
                        SubDistrict = model.SubDistrict ?? "",
                        Alley = model.Alley ?? "",
                        Street = model.Street ?? "",
                        House = model.House ?? "",
                        NearestPoint = model.NearestPoint ?? ""
                    };

                    _context.Addresses.Add(address);
                    await _context.SaveChangesAsync();

                    var identify = new Identify
                    {
                        FullName = model.FullName ?? "",
                        LastName = model.LastName ?? "",
                        MotherName = model.MotherName ?? "",
                        Date = model.DateOfBirth,
                        Gender = model.Gender ?? "ذكر",
                        MozakeName = model.MozakeName ?? "",
                        Education = model.Education ?? "",
                        Specialization = model.Specialization ?? "",
                        PhoneNumber = model.PhoneNumber ?? "",
                        IdentityCardN = model.IdentityCardN,
                        identityDate = model.IdentityDate,
                        RationN = model.RationN,
                        RationCenter = model.RationCenter,
                        UserId = user.Id,
                        AddressId = address.Id
                    };

                    _context.Identifies.Add(identify);
                }

                // تحديث رقم الهاتف في IdentityUser إذا تغير
                if (!string.IsNullOrEmpty(model.PhoneNumber) && user.PhoneNumber != model.PhoneNumber)
                {
                    user.PhoneNumber = model.PhoneNumber;
                    await _userManager.UpdateAsync(user);
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "✅ تم حفظ الملف الشخصي بنجاح!";
                return RedirectToAction("ProfileDetails");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ في حفظ الملف الشخصي");
                ViewBag.ErrorMessage = "حدث خطأ أثناء الحفظ: " + ex.Message;
                return View(model);
            }
        }

        // GET: /Register/ProfileDetails
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> ProfileDetails()
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                TempData["ErrorMessage"] = "يجب تسجيل الدخول أولاً.";
                return RedirectToAction("Login", "Account");
            }

            var user = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(user);

            var profile = await _context.Identifies
                .Include(i => i.Address)
                .FirstOrDefaultAsync(i => i.UserId == userId);

            if (profile == null)
            {
                return RedirectToAction("CompleteProfile");
            }

            var viewModel = new PersonalProfileViewModel
            {
                UserId = userId,
                Email = user.Email,
                UserRole = roles.FirstOrDefault() ?? "User",
                IsEmailConfirmed = user.EmailConfirmed,

                FullName = profile.FullName ?? "",
                LastName = profile.LastName ?? "",
                MotherName = profile.MotherName ?? "",
                DateOfBirth = profile.Date,
                Gender = profile.Gender ?? "ذكر",
                MozakeName = profile.MozakeName ?? "",
                Education = profile.Education ?? "",
                Specialization = profile.Specialization ?? "",
                PhoneNumber = profile.PhoneNumber ?? "",
                IdentityCardN = profile.IdentityCardN,
                IdentityDate = profile.identityDate,
                RationN = profile.RationN,
                RationCenter = profile.RationCenter,

                Governorate = profile.Address?.Governorate ?? "بغداد",
                District = profile.Address?.District ?? "",
                SubDistrict = profile.Address?.SubDistrict ?? "",
                Alley = profile.Address?.Alley ?? "",
                Street = profile.Address?.Street ?? "",
                House = profile.Address?.House ?? "",
                NearestPoint = profile.Address?.NearestPoint ?? "",

                RegistrationDate = profile.Date,
                Governorates = GetGovernorates(),
                Genders = new List<string> { "ذكر", "أنثى" },
                Educations = GetEducations()
            };

            return View(viewModel);
        }

        private List<string> GetGovernorates()
        {
            return new List<string>
            {
                "بغداد", "الأنبار", "بابل", "البصرة", "ذي قار", "القادسية",
                "ديالى", "دهوك", "أربيل", "كربلاء", "كركوك", "ميسان",
                "المثنى", "النجف", "نينوى", "صلاح الدين", "السليمانية", "واسط"
            };
        }

        private List<string> GetEducations()
        {
            return new List<string>
            {
                "ابتدائي", "متوسط", "إعدادي", "ثانوي",
                "دبلوم", "بكالوريوس", "ماجستير", "دكتوراه"
            };
        }

        // GET: /Register/CheckDatabaseSchema
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> CheckDatabaseSchema()
        {
            try
            {
                var html = "<h3>🔍 فحص قاعدة البيانات</h3>";

                var canConnect = await _context.Database.CanConnectAsync();
                html += $"<p>✅ الاتصال بقاعدة البيانات: {(canConnect ? "ناجح" : "فاشل")}</p>";

                return Content(html, "text/html");
            }
            catch (Exception ex)
            {
                return Content($"❌ خطأ: {ex.Message}", "text/html");
            }
        }

        // GET: /Register/TestSave
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> TestSave(string name = "اختبار", string phone = "07700000000")
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Content("❌ يجب تسجيل الدخول أولاً");
                }

                var identify = new Identify
                {
                    FullName = name,
                    Date = DateTime.Now,
                    Gender = "ذكر",
                    PhoneNumber = phone,
                    UserId = user.Id,
                    IdentityCardN = 123456,
                    identityDate = DateTime.Now,
                    RationN = 0,
                    RationCenter = 0
                };

                _context.Identifies.Add(identify);
                await _context.SaveChangesAsync();

                return Content($"✅ تم الحفظ بنجاح!<br>" +
                              $"ID: {identify.Id}<br>" +
                              $"الاسم: {identify.FullName}<br>" +
                              $"المستخدم: {user.Email}<br>" +
                              $"<a href='/Register/ViewData'>عرض البيانات</a>", "text/html");
            }
            catch (Exception ex)
            {
                return Content($"❌ فشل الحفظ: {ex.Message}<br>{ex.InnerException?.Message}", "text/html");
            }
        }

        // GET: /Register/ViewData
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> ViewData()
        {
            var userId = _userManager.GetUserId(User);

            var identifies = await _context.Identifies
                .Include(i => i.Address)
                .Where(i => i.UserId == userId)
                .ToListAsync();

            var html = "<h3>📊 بياناتك المحفوظة:</h3>";

            if (!identifies.Any())
            {
                html += "<p>❌ لا توجد بيانات محفوظة</p>";
            }
            else
            {
                html += "<table border='1' class='table table-bordered'><tr>" +
                       "<th>ID</th><th>الاسم</th><th>الهاتف</th><th>المحافظة</th><th>التاريخ</th>" +
                       "</tr>";
                foreach (var item in identifies)
                {
                    var addressInfo = item.Address != null ? item.Address.Governorate : "لا يوجد";
                    html += $"<tr>" +
                           $"<td>{item.Id}</td>" +
                           $"<td>{item.FullName}</td>" +
                           $"<td>{item.PhoneNumber}</td>" +
                           $"<td>{addressInfo}</td>" +
                           $"<td>{item.Date:yyyy-MM-dd}</td>" +
                           $"</tr>";
                }
                html += "</table>";
            }

            html += $"<p><a href='/Register/CompleteProfile' class='btn btn-primary'>إكمال البيانات</a></p>";
            html += $"<p><a href='/Register/CheckDatabaseSchema' class='btn btn-info'>فحص قاعدة البيانات</a></p>";

            return Content(html, "text/html");
        }

        // GET: /Register/DebugModelState
        [HttpGet]
        [Authorize]
        [Route("/register/debug-modelstate")]
        public IActionResult DebugModelState()
        {
            var model = new PersonalProfileViewModel
            {
                UserId = "test-user-id-123",
                FullName = "أحمد محمد",
                DateOfBirth = DateTime.Now.AddYears(-25),
                Gender = "ذكر",
                PhoneNumber = "07701234567",
                IdentityCardN = 123456789,
                IdentityDate = DateTime.Now.AddYears(-5),
                Governorate = "بغداد",
                District = "الكرخ",
                Street = "الرشيد",
                House = "15",
                RationN = 12345,
                RationCenter = 123
            };

            var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(model);
            var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            bool isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(model, validationContext, validationResults, true);

            var html = "<h3>🔍 فحص نموذج PersonalProfileViewModel</h3>";
            html += "<div class='alert " + (isValid ? "alert-success" : "alert-danger") + "'>";
            html += $"<strong>الحالة:</strong> {(isValid ? "✅ النموذج صالح" : "❌ النموذج غير صالح")}";
            html += "</div>";

            if (!isValid)
            {
                html += "<h4>❌ الأخطاء التي تم اكتشافها:</h4>";
                html += "<ul class='list-group'>";
                foreach (var error in validationResults)
                {
                    html += $"<li class='list-group-item list-group-item-danger'>{error.ErrorMessage}</li>";
                }
                html += "</ul>";
            }

            html += "<hr>";
            html += "<a href='/Register/CompleteProfile' class='btn btn-primary'>العودة إلى صفحة إكمال الملف</a>";

            return Content($"<div class='container mt-4'>{html}</div>", "text/html");
        }
    }
}