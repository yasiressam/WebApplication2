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

            var existingProfile = await _context.Identifies
                .Include(i => i.Address)
                .FirstOrDefaultAsync(i => i.UserId == userId);

            if (existingProfile != null)
            {
                TempData["InfoMessage"] = "ملفك الشخصي مكتمل بالفعل.";
                return RedirectToAction("Index", "Home");
            }

            var model = new PersonalProfileViewModel
            {
                UserId = userId,
                // تعيين قيم افتراضية واضحة
                FullName = "",
                DateOfBirth = DateTime.Now.AddYears(-20),
                Gender = "ذكر",
                PhoneNumber = "",
                IdentityCardN = 100000, // قيمة ابتدائية صحيحة
                IdentityDate = DateTime.Now,
                Governorate = "بغداد",
                Governorates = GetGovernorates()
            };

            return View(model);
        }

        // POST: /Register/CompleteProfile
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteProfile(PersonalProfileViewModel model)
        {
            _logger.LogInformation("=== 🚀 بدء حفظ الملف الشخصي ===");

            // تسجيل جميع القيم المرسلة
            _logger.LogInformation($"المستخدم: {model.UserId}");
            _logger.LogInformation($"الاسم: {model.FullName}");
            _logger.LogInformation($"الهاتف: {model.PhoneNumber}");
            _logger.LogInformation($"المحافظة: {model.Governorate}");
            _logger.LogInformation($"رقم البطاقة: {model.IdentityCardN}");
            _logger.LogInformation($"تاريخ الميلاد: {model.DateOfBirth}");
            _logger.LogInformation($"تاريخ البطاقة: {model.IdentityDate}");

            // 🔥 التحقق المفصل من ModelState
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("⚠️ ModelState غير صالح! تفاصيل الأخطاء:");

                var errors = new Dictionary<string, List<string>>();
                foreach (var state in ModelState)
                {
                    var key = state.Key;
                    var entry = state.Value;

                    if (entry.Errors.Count > 0)
                    {
                        _logger.LogWarning($"❌ حقل '{key}':");
                        var errorMessages = new List<string>();
                        foreach (var error in entry.Errors)
                        {
                            _logger.LogWarning($"   - {error.ErrorMessage}");
                            errorMessages.Add(error.ErrorMessage);
                        }
                        errors[key] = errorMessages;
                    }
                }

                // تخزين الأخطاء لعرضها في View
                ViewBag.ValidationErrors = errors;
                ViewBag.ErrorMessage = "يوجد أخطاء في البيانات المدخلة. يرجى تصحيحها.";

                model.Governorates = GetGovernorates();
                return View(model);
            }

            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogError("❌ لم يتم العثور على المستخدم!");
                    TempData["ErrorMessage"] = "لم يتم العثور على المستخدم. يرجى تسجيل الدخول مرة أخرى.";
                    return RedirectToAction("Login", "Account");
                }

                // التحقق من تطابق المستخدم
                if (user.Id != model.UserId)
                {
                    _logger.LogError($"❌ عدم تطابق ID: {user.Id} != {model.UserId}");
                    TempData["ErrorMessage"] = "خطأ في المصادقة.";
                    return RedirectToAction("Login", "Account");
                }

                _logger.LogInformation("📦 بدء حفظ البيانات في قاعدة البيانات...");

                // 1. حفظ العنوان
                var address = new Address
                {
                    Governorate = model.Governorate ?? "غير محدد",
                    District = model.District,
                    SubDistrict = model.SubDistrict,
                    Alley = model.Alley,
                    Street = model.Street,
                    House = model.House,
                    NearestPoint = model.NearestPoint
                };

                _logger.LogInformation($"📌 عنوان جديد: {address.Governorate}");
                _context.Addresses.Add(address);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"✅ تم حفظ العنوان، ID: {address.Id}");

                // 2. حفظ الملف الشخصي
                var identify = new Identify
                {
                    FullName = model.FullName,
                    LastName = model.LastName,
                    MotherName = model.MotherName,
                    Date = model.DateOfBirth,
                    Gender = model.Gender,
                    MozakeName = model.MozakeName,
                    Education = model.Education,
                    Specialization = model.Specialization,
                    PhoneNumber = model.PhoneNumber,
                    IdentityCardN = model.IdentityCardN,
                    identityDate = model.IdentityDate,
                    RationN = model.RationN,
                    RationCenter = model.RationCenter,
                    UserId = user.Id,
                    AddressId = address.Id
                };

                _logger.LogInformation($"📌 ملف شخصي جديد: {identify.FullName}");
                _context.Identifies.Add(identify);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"✅ تم حفظ الملف الشخصي، ID: {identify.Id}");

                // 3. تحديث الهاتف في Identity
                if (!string.IsNullOrEmpty(model.PhoneNumber) && user.PhoneNumber != model.PhoneNumber)
                {
                    user.PhoneNumber = model.PhoneNumber;
                    var updateResult = await _userManager.UpdateAsync(user);

                    if (updateResult.Succeeded)
                    {
                        _logger.LogInformation($"✅ تم تحديث رقم الهاتف: {model.PhoneNumber}");
                    }
                }

                _logger.LogInformation("🎉 تم حفظ جميع البيانات بنجاح!");

                TempData["SuccessMessage"] = "✅ تم إكمال ملفك الشخصي بنجاح! يمكنك الآن استخدام الموقع.";
                return RedirectToAction("Index", "Home");
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "❌ خطأ في قاعدة البيانات");
                _logger.LogError($"تفاصيل الخطأ: {dbEx.InnerException?.Message}");

                ViewBag.ErrorMessage = "حدث خطأ في قاعدة البيانات. تأكد من صحة جميع البيانات المدخلة.";
                ModelState.AddModelError("", "خطأ في الحفظ: " + dbEx.InnerException?.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ غير متوقع");
                _logger.LogError($"تفاصيل: {ex.Message}");

                ViewBag.ErrorMessage = "حدث خطأ غير متوقع. يرجى المحاولة مرة أخرى.";
                ModelState.AddModelError("", "خطأ: " + ex.Message);
            }

            // في حالة الخطأ
            model.Governorates = GetGovernorates();
            return View(model);
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
            html += $"<p><a href='/Register/TestSave?name=اختبار2&phone=07701111111' class='btn btn-warning'>اختبار الحفظ</a></p>";
            html += $"<p><a href='/register/debug-modelstate' class='btn btn-info'>فحص النموذج</a></p>";

            return Content(html, "text/html");
        }

        // GET: /Register/DebugModelState
        [HttpGet]
        [Authorize]
        [Route("/register/debug-modelstate")]
        public IActionResult DebugModelState()
        {
            // إنشاء نموذج تجريبي مع قيم افتراضية
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
                House = "15"
            };

            // اختبار التحقق من الصحة
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
            html += "<h4>📋 قيم النموذج التجريبية:</h4>";
            html += "<table class='table table-bordered'>";
            html += "<thead><tr><th>الحقل</th><th>القيمة</th><th>الحالة</th></tr></thead>";
            html += "<tbody>";

            // فحص كل حقل
            var fields = new[]
            {
                new { Name = "FullName", Value = model.FullName, Required = true },
                new { Name = "DateOfBirth", Value = model.DateOfBirth.ToString("yyyy-MM-dd"), Required = true },
                new { Name = "Gender", Value = model.Gender, Required = true },
                new { Name = "PhoneNumber", Value = model.PhoneNumber, Required = true },
                new { Name = "IdentityCardN", Value = model.IdentityCardN.ToString(), Required = true },
                new { Name = "IdentityDate", Value = model.IdentityDate.ToString("yyyy-MM-dd"), Required = true },
                new { Name = "Governorate", Value = model.Governorate, Required = true }
            };

            foreach (var field in fields)
            {
                bool fieldValid = !field.Required || !string.IsNullOrEmpty(field.Value?.ToString());
                html += $"<tr>";
                html += $"<td>{field.Name}</td>";
                html += $"<td><code>{field.Value}</code></td>";
                html += $"<td>{(fieldValid ? "✅" : "❌")}</td>";
                html += $"</tr>";
            }

            html += "</tbody></table>";

            html += "<hr>";
            html += "<h4>🔧 إصلاحات سريعة:</h4>";
            html += "<ol>";
            html += "<li>تأكد من تعبئة جميع الحقول المطلوبة (*)</li>";
            html += "<li>رقم الهاتف يجب أن يبدأ بـ 07 ويحتوي على 11 رقم</li>";
            html += "<li>رقم البطاقة الوطنية يجب أن يكون بين 100,000 و 999,999,999</li>";
            html += "<li>التواريخ يجب أن تكون صحيحة</li>";
            html += "</ol>";

            html += "<a href='/Register/CompleteProfile' class='btn btn-primary'>العودة إلى صفحة إكمال الملف</a>";

            return Content($"<div class='container mt-4'>{html}</div>", "text/html");
        }

        // GET: /Register/QuickTest
        [HttpGet]
        [Authorize]
        [Route("/register/quick-test")]
        public async Task<IActionResult> QuickTest()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var userId = user?.Id;

                var html = "<h3>⚡ اختبار سريع</h3>";
                html += $"<p>المستخدم: {userId}</p>";
                html += $"<p>البريد: {user?.Email}</p>";

                // اختبار حفظ بسيط
                var testAddress = new Address
                {
                    Governorate = "اختبار",
                    Street = "شارع اختباري"
                };

                _context.Addresses.Add(testAddress);
                await _context.SaveChangesAsync();

                html += $"<p>✅ تم حفظ عنوان اختباري، ID: {testAddress.Id}</p>";

                // محاولة حذفه
                _context.Addresses.Remove(testAddress);
                await _context.SaveChangesAsync();

                html += $"<p>✅ تم حذف العنوان الاختباري</p>";
                html += $"<p><a href='/Register/CompleteProfile' class='btn btn-success'>جرب الآن</a></p>";

                return Content(html, "text/html");
            }
            catch (Exception ex)
            {
                return Content($"❌ فشل الاختبار: {ex.Message}", "text/html");
            }
        }
    }
}