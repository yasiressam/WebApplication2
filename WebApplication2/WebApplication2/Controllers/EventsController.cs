using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;

namespace WebApplication2.Controllers
{
    [Authorize(Roles = clsRoles.SuperAdmin + "," + clsRoles.Admin)]
    public class EventsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<EventsController> _logger;

        public EventsController(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            ILogger<EventsController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        // ========== عرض قائمة الأحداث ==========
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var events = await _context.Events
                .OrderByDescending(e => e.StartDate)
                .ToListAsync();
            return View(events);
        }

        // ========== جلب حدث للتعديل ==========
        [HttpGet]
        public async Task<IActionResult> Get(int id)
        {
            var eventItem = await _context.Events.FindAsync(id);
            if (eventItem == null)
                return Json(new { success = false, message = "الحدث غير موجود" });

            return Json(new
            {
                id = eventItem.Id,
                title = eventItem.Title,
                eventType = eventItem.EventType,
                startDate = eventItem.StartDate.ToString("yyyy-MM-ddTHH:mm"),
                endDate = eventItem.EndDate.ToString("yyyy-MM-ddTHH:mm"),
                location = eventItem.Location,
                governorate = eventItem.Governorate,
                description = eventItem.Description,
                isUrgent = eventItem.IsUrgent,
                isMandatory = eventItem.IsMandatory,
                targetCategory = eventItem.TargetCategory
            });
        }

        // ========== إنشاء حدث جديد ==========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] Event model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    return Json(new { success = false, message = string.Join(", ", errors) });
                }

                model.CreatedBy = _userManager.GetUserId(User);
                model.CreatedAt = DateTime.Now;
                model.IsActive = true;

                _context.Events.Add(model);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "تم إضافة الحدث بنجاح" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في إضافة حدث");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ========== تعديل حدث ==========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([FromBody] Event model)
        {
            try
            {
                var eventItem = await _context.Events.FindAsync(model.Id);
                if (eventItem == null)
                    return Json(new { success = false, message = "الحدث غير موجود" });

                eventItem.Title = model.Title;
                eventItem.EventType = model.EventType;
                eventItem.StartDate = model.StartDate;
                eventItem.EndDate = model.EndDate;
                eventItem.Location = model.Location;
                eventItem.Governorate = model.Governorate;
                eventItem.Description = model.Description;
                eventItem.IsUrgent = model.IsUrgent;
                eventItem.IsMandatory = model.IsMandatory;
                eventItem.TargetCategory = model.TargetCategory;

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "تم تعديل الحدث بنجاح" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في تعديل حدث");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ========== حذف حدث ==========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete([FromBody] int id)
        {
            try
            {
                var eventItem = await _context.Events.FindAsync(id);
                if (eventItem == null)
                    return Json(new { success = false, message = "الحدث غير موجود" });

                // حذف سجلات الحضور المرتبطة من جداول الحضور
                var politicalAttendances = await _context.PoliticalForumAttendances
                    .Where(a => a.EventId == id)
                    .ToListAsync();
                if (politicalAttendances.Any())
                    _context.PoliticalForumAttendances.RemoveRange(politicalAttendances);

                var periodicAttendances = await _context.PeriodicMeetingAttendances
                    .Where(a => a.EventId == id)
                    .ToListAsync();
                if (periodicAttendances.Any())
                    _context.PeriodicMeetingAttendances.RemoveRange(periodicAttendances);

                _context.Events.Remove(eventItem);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "تم حذف الحدث بنجاح" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في حذف حدث");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ========== تفاصيل الحدث ==========
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var eventItem = await _context.Events.FindAsync(id);
            if (eventItem == null)
                return NotFound();

            return View(eventItem);
        }
    }
}