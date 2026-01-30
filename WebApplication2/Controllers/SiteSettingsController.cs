using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;

namespace WebApplication2.Controllers
{
    [Authorize(Roles = clsRoles.SuperAdmin)]
    public class SiteSettingsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SiteSettingsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: SiteSettings
        public async Task<IActionResult> Index()
        {
            var settings = await _context.SiteSettings.FirstOrDefaultAsync();

            if (settings == null)
            {
                settings = new SiteSettings();
                _context.SiteSettings.Add(settings);
                await _context.SaveChangesAsync();
            }

            return View(settings);
        }

        // POST: SiteSettings
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(SiteSettings model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var existing = await _context.SiteSettings.FirstOrDefaultAsync();

                    if (existing != null)
                    {
                        // تحديث جميع الحقول
                        existing.ContactEmail = model.ContactEmail ?? "";
                        existing.ContactPhone = model.ContactPhone ?? "";
                        existing.SiteAddress = model.SiteAddress ?? "";
                        existing.SiteDescription = model.SiteDescription ?? "";

                        // وسائل التواصل الاجتماعي (الثلاثة فقط)
                        existing.FacebookUrl = model.FacebookUrl ?? "";
                        existing.InstagramUrl = model.InstagramUrl ?? "";
                        existing.WhatsAppNumber = model.WhatsAppNumber ?? "";

                        existing.LastUpdated = DateTime.Now;

                        _context.SiteSettings.Update(existing);
                    }
                    else
                    {
                        model.LastUpdated = DateTime.Now;
                        _context.SiteSettings.Add(model);
                    }

                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "✅ تم حفظ إعدادات الموقع بنجاح!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"❌ حدث خطأ: {ex.Message}";
                }
            }
            else
            {
                TempData["ErrorMessage"] = "❌ يوجد أخطاء في البيانات المدخلة";
            }

            return View(model);
        }

        // GET: SiteSettings/Reset
        public async Task<IActionResult> Reset()
        {
            var settings = await _context.SiteSettings.FirstOrDefaultAsync();

            if (settings != null)
            {
                settings.ContactEmail = "info@iraqinews.com";
                settings.ContactPhone = "+964 770 000 0000";
                settings.SiteAddress = "العراق - بغداد";
                settings.SiteDescription = "منصة إلكترونية متكاملة تهدف إلى توفير خدمات إلكترونية للمواطنين العراقيين بأسلوب عصري وسهل.";
                settings.FacebookUrl = "";
                settings.InstagramUrl = "";
                settings.WhatsAppNumber = "";
                settings.LastUpdated = DateTime.Now;

                _context.Update(settings);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "✅ تم استعادة الإعدادات الافتراضية!";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}