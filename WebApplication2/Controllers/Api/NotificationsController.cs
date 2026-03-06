using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;
using WebApplication2.Services;

namespace WebApplication2.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ApplicationDbContext _context;

        public NotificationsController(
            INotificationService notificationService,
            UserManager<IdentityUser> userManager,
            ApplicationDbContext context)
        {
            _notificationService = notificationService;
            _userManager = userManager;
            _context = context;
        }

        [HttpGet("get")]
        public async Task<IActionResult> GetNotifications()
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                if (string.IsNullOrEmpty(userId))
                {
                    return Ok(new { notifications = new List<object>(), unreadCount = 0 });
                }

                // 🔥 نجلب كل الإشعارات (غير مقروءة ومقروءة)
                var notifications = await _context.Notifications
                    .Where(n => n.IsForAll || n.TargetUserId == userId)
                    .OrderByDescending(n => n.SentAt)
                    .Take(50)
                    .ToListAsync();

                var unreadCount = notifications.Count(n => !n.IsRead);

                var result = notifications.Select(n => new
                {
                    id = n.Id,
                    title = n.Title ?? "إشعار",
                    message = n.Message ?? "",
                    icon = n.Icon ?? "bi-bell",
                    time = GetRelativeTime(n.SentAt),
                    read = n.IsRead, // 🔥 مهم جداً: نحدد إذا كان مقروءاً
                    clickUrl = n.ClickUrl ?? ""
                });

                return Ok(new
                {
                    notifications = result,
                    unreadCount = unreadCount
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { notifications = new List<object>(), unreadCount = 0, error = ex.Message });
            }
        }

        [HttpPost("mark-read")]
        public async Task<IActionResult> MarkAsRead([FromBody] int id)
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _notificationService.MarkAsRead(id, userId);
                return result ? Ok(new { success = true }) : NotFound(new { success = false });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("mark-all-read")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                await _notificationService.MarkAllAsRead(userId);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("register-device")]
        public async Task<IActionResult> RegisterDevice([FromBody] RegisterDeviceModel model)
        {
            try
            {
                if (!User.Identity.IsAuthenticated)
                    return Ok(new { message = "مستخدم غير مسجل" });

                var userId = _userManager.GetUserId(User);
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var existingDevice = await _context.UserDevices
                    .FirstOrDefaultAsync(d => d.PlayerId == model.PlayerId);

                if (existingDevice != null)
                {
                    existingDevice.LastActive = DateTime.Now;
                    existingDevice.IsSubscribed = true;
                    existingDevice.UserId = userId;
                }
                else
                {
                    var device = new UserDevice
                    {
                        UserId = userId,
                        PlayerId = model.PlayerId,
                        DeviceType = model.DeviceType ?? "web",
                        Browser = model.Browser,
                        OperatingSystem = model.OperatingSystem,
                        LastActive = DateTime.Now,
                        IsSubscribed = true
                    };
                    _context.UserDevices.Add(device);
                }

                await _context.SaveChangesAsync();
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("send-test")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> SendTest([FromBody] SendTestNotificationModel model)
        {
            try
            {
                var notification = await _notificationService.CreateNotification(
                    model.Title,
                    model.Message,
                    model.TargetUserId,
                    model.Icon,
                    model.ClickUrl
                );

                var result = await _notificationService.SendToOneSignal(notification);

                if (result)
                    return Ok(new { success = true, message = "تم الإرسال بنجاح" });
                else
                    return StatusCode(500, new { success = false, message = "فشل الإرسال" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        private string GetRelativeTime(DateTime dateTime)
        {
            var timeSpan = DateTime.Now - dateTime;

            if (timeSpan.TotalSeconds < 60)
                return "الآن";
            if (timeSpan.TotalMinutes < 60)
                return $"منذ {Math.Max(1, (int)timeSpan.TotalMinutes)} دقيقة";
            if (timeSpan.TotalHours < 24)
                return $"منذ {Math.Max(1, (int)timeSpan.TotalHours)} ساعة";
            if (timeSpan.TotalDays < 30)
                return $"منذ {Math.Max(1, (int)timeSpan.TotalDays)} يوم";
            if (timeSpan.TotalDays < 365)
                return $"منذ {Math.Max(1, (int)(timeSpan.TotalDays / 30))} شهر";

            return dateTime.ToString("yyyy/MM/dd");
        }
    }

    public class RegisterDeviceModel
    {
        public string PlayerId { get; set; } = string.Empty;
        public string? DeviceType { get; set; }
        public string? Browser { get; set; }
        public string? OperatingSystem { get; set; }
    }

    public class SendTestNotificationModel
    {
        public string Title { get; set; } = "إشعار تجريبي";
        public string Message { get; set; } = "هذا إشعار تجريبي";
        public string? TargetUserId { get; set; }
        public string? Icon { get; set; } = "bi-bell";
        public string? ClickUrl { get; set; }
    }
}