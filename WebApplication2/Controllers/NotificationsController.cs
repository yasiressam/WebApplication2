using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WebApplication2.Models;
using WebApplication2.Services;

namespace WebApplication2.Controllers
{
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly INotificationService _notificationService;
        private readonly UserManager<IdentityUser> _userManager;

        public NotificationsController(
            INotificationService notificationService,
            UserManager<IdentityUser> userManager)
        {
            _notificationService = notificationService;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            var notifications = await _notificationService.GetUserNotifications(userId);
            return View(notifications);
        }

        public async Task<IActionResult> Details(int id)
        {
            var userId = _userManager.GetUserId(User);
            var notifications = await _notificationService.GetUserNotifications(userId);
            var notification = notifications.FirstOrDefault(n => n.Id == id);

            if (notification == null)
                return NotFound();

            if (!notification.IsRead)
            {
                await _notificationService.MarkAsRead(id, userId);
            }

            return View(notification);
        }
    }
}