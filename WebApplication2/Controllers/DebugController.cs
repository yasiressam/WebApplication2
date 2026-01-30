using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using WebApplication2.Models;

namespace WebApplication2.Controllers
{
    public class DebugController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<DebugController> _logger;

        public DebugController(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            ILogger<DebugController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpGet]
        [Route("/debug/check-db")]
        public async Task<IActionResult> CheckDatabase()
        {
            var results = new List<string>();

            try
            {
                // 1. التحقق من الاتصال
                var canConnect = await _context.Database.CanConnectAsync();
                results.Add($"✅ الاتصال بقاعدة البيانات: {canConnect}");

                if (canConnect)
                {
                    // 2. التحقق من الجداول
                    var tables = new[] { "Addresses", "Identifies", "News" };
                    foreach (var table in tables)
                    {
                        try
                        {
                            // محاولة قراءة من الجدول
                            if (table == "Addresses")
                            {
                                var count = await _context.Addresses.CountAsync();
                                results.Add($"✅ جدول {table}: موجود ({count} سجل)");
                            }
                            else if (table == "Identifies")
                            {
                                var count = await _context.Identifies.CountAsync();
                                results.Add($"✅ جدول {table}: موجود ({count} سجل)");
                            }
                        }
                        catch (Exception ex)
                        {
                            results.Add($"❌ جدول {table}: خطأ - {ex.Message}");
                        }
                    }

                    // 3. التحقق من الهيكل
                    results.Add("<h4>هيكل جدول Identifies:</h4>");
                    var columns = await _context.Identifies
                        .Take(1)
                        .Select(x => new
                        {
                            HasUserId = x.UserId != null,
                            HasAddressId = x.AddressId != null,
                            HasFullName = x.FullName != null
                        })
                        .FirstOrDefaultAsync();

                    if (columns != null)
                    {
                        results.Add($"- UserId: {(columns.HasUserId ? "✅" : "❌")}");
                        results.Add($"- AddressId: {(columns.HasAddressId ? "✅" : "❌")}");
                        results.Add($"- FullName: {(columns.HasFullName ? "✅" : "❌")}");
                    }
                }
            }
            catch (Exception ex)
            {
                results.Add($"❌ خطأ: {ex.Message}");
            }

            return Content(string.Join("<br>", results), "text/html");
        }

        [HttpGet]
        [Route("/debug/test-simple-save")]
        [Authorize]
        public async Task<IActionResult> TestSimpleSave()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                    return Content("❌ يجب تسجيل الدخول");

                // 1. حفظ عنوان بسيط
                var address = new Address
                {
                    Governorate = "بغداد",
                    Street = "الرشيد",
                    House = "10"
                };

                _context.Addresses.Add(address);
                await _context.SaveChangesAsync();

                // 2. حفظ بيانات شخصية بسيطة
                var identify = new Identify
                {
                    FullName = "اختبار",
                    Date = DateTime.Now,
                    Gender = "ذكر",
                    PhoneNumber = "07700000000",
                    IdentityCardN = 123456,
                    identityDate = DateTime.Now,
                    RationN = 0,
                    RationCenter = 0,
                    UserId = user.Id,
                    AddressId = address.Id
                };

                _context.Identifies.Add(identify);
                await _context.SaveChangesAsync();

                return Content($"✅ تم الحفظ بنجاح!<br>" +
                              $"Address ID: {address.Id}<br>" +
                              $"Identify ID: {identify.Id}<br>" +
                              $"<a href='/debug/view-data'>عرض البيانات</a>", "text/html");
            }
            catch (Exception ex)
            {
                return Content($"❌ فشل الحفظ:<br>{ex.Message}<br>{ex.InnerException?.Message}", "text/html");
            }
        }

        [HttpGet]
        [Route("/debug/view-data")]
        public async Task<IActionResult> ViewData()
        {
            var addresses = await _context.Addresses.ToListAsync();
            var identifies = await _context.Identifies.ToListAsync();

            var html = "<h3>📊 البيانات المحفوظة</h3>";

            html += "<h4>العناوين:</h4>";
            html += "<table border='1'><tr><th>ID</th><th>المحافظة</th><th>الشارع</th><th>المنزل</th></tr>";
            foreach (var addr in addresses)
            {
                html += $"<tr><td>{addr.Id}</td><td>{addr.Governorate}</td><td>{addr.Street}</td><td>{addr.House}</td></tr>";
            }
            html += "</table>";

            html += "<h4>المستخدمين:</h4>";
            html += "<table border='1'><tr><th>ID</th><th>الاسم</th><th>الهاتف</th><th>المستخدم ID</th><th>العنوان ID</th></tr>";
            foreach (var id in identifies)
            {
                html += $"<tr><td>{id.Id}</td><td>{id.FullName}</td><td>{id.PhoneNumber}</td><td>{id.UserId}</td><td>{id.AddressId}</td></tr>";
            }
            html += "</table>";

            return Content(html, "text/html");
        }

        [HttpGet]
        [Route("/debug/check-user")]
        [Authorize]
        public async Task<IActionResult> CheckUser()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Content("❌ لا يوجد مستخدم مسجل دخول");

            return Content($"👤 معلومات المستخدم:<br>" +
                          $"ID: {user.Id}<br>" +
                          $"Email: {user.Email}<br>" +
                          $"Phone: {user.PhoneNumber}<br>" +
                          $"EmailConfirmed: {user.EmailConfirmed}<br>" +
                          $"<a href='/debug/test-simple-save'>اختبار الحفظ</a>", "text/html");
        }
    }
}