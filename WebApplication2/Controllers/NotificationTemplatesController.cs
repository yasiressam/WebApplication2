using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WebApplication2.Data;
using WebApplication2.Models;

namespace WebApplication2.Controllers
{
    [Authorize(Roles = clsRoles.SuperAdmin)]
    public class NotificationTemplatesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public NotificationTemplatesController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<IActionResult> Index()
        {
            if (!IsAllowedEmail())
            {
                return Forbid();
            }

            var settings = await GetOrCreateSettingsAsync();
            return View(settings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(SiteSettings model)
        {
            if (!IsAllowedEmail())
            {
                return Forbid();
            }

            var settings = await GetOrCreateSettingsAsync();

            settings.PromotionApprovedTitle = model.PromotionApprovedTitle;
            settings.PromotionApprovedMessage = model.PromotionApprovedMessage;
            settings.PromotionRejectedTitle = model.PromotionRejectedTitle;
            settings.PromotionRejectedMessage = model.PromotionRejectedMessage;
            settings.BasicInfoApprovedTitle = model.BasicInfoApprovedTitle;
            settings.BasicInfoApprovedMessage = model.BasicInfoApprovedMessage;
            settings.BasicInfoRejectedTitle = model.BasicInfoRejectedTitle;
            settings.BasicInfoRejectedMessage = model.BasicInfoRejectedMessage;
            settings.DirectAssignmentTitle = model.DirectAssignmentTitle;
            settings.DirectAssignmentMessage = model.DirectAssignmentMessage;
            settings.AssignmentFormTitle = model.AssignmentFormTitle;
            settings.AssignmentFormMessage = model.AssignmentFormMessage;
            settings.AssignmentSubmittedTitle = model.AssignmentSubmittedTitle;
            settings.AssignmentSubmittedMessage = model.AssignmentSubmittedMessage;
            settings.AssignmentApprovedTitle = model.AssignmentApprovedTitle;
            settings.AssignmentApprovedMessage = model.AssignmentApprovedMessage;
            settings.AssignmentRejectedTitle = model.AssignmentRejectedTitle;
            settings.AssignmentRejectedMessage = model.AssignmentRejectedMessage;
            settings.AssignmentRemovedTitle = model.AssignmentRemovedTitle;
            settings.AssignmentRemovedMessage = model.AssignmentRemovedMessage;
            settings.SuperAdminAssignedTitle = model.SuperAdminAssignedTitle;
            settings.SuperAdminAssignedMessage = model.SuperAdminAssignedMessage;
            settings.AdminAssignedTitle = model.AdminAssignedTitle;
            settings.AdminAssignedMessage = model.AdminAssignedMessage;
            settings.NewsEditorAssignedTitle = model.NewsEditorAssignedTitle;
            settings.NewsEditorAssignedMessage = model.NewsEditorAssignedMessage;
            settings.MapViewerAssignedTitle = model.MapViewerAssignedTitle;
            settings.MapViewerAssignedMessage = model.MapViewerAssignedMessage;
            settings.MemberAssignedTitle = model.MemberAssignedTitle;
            settings.MemberAssignedMessage = model.MemberAssignedMessage;
            settings.ProfileUpdatedTitle = model.ProfileUpdatedTitle;
            settings.ProfileUpdatedMessage = model.ProfileUpdatedMessage;
            settings.LastUpdated = DateTime.Now;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "✅ تم حفظ قوالب الإشعارات بنجاح";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Reset()
        {
            if (!IsAllowedEmail())
            {
                return Forbid();
            }

            var defaults = new SiteSettings();
            var settings = await GetOrCreateSettingsAsync();

            settings.PromotionApprovedTitle = defaults.PromotionApprovedTitle;
            settings.PromotionApprovedMessage = defaults.PromotionApprovedMessage;
            settings.PromotionRejectedTitle = defaults.PromotionRejectedTitle;
            settings.PromotionRejectedMessage = defaults.PromotionRejectedMessage;
            settings.BasicInfoApprovedTitle = defaults.BasicInfoApprovedTitle;
            settings.BasicInfoApprovedMessage = defaults.BasicInfoApprovedMessage;
            settings.BasicInfoRejectedTitle = defaults.BasicInfoRejectedTitle;
            settings.BasicInfoRejectedMessage = defaults.BasicInfoRejectedMessage;
            settings.DirectAssignmentTitle = defaults.DirectAssignmentTitle;
            settings.DirectAssignmentMessage = defaults.DirectAssignmentMessage;
            settings.AssignmentFormTitle = defaults.AssignmentFormTitle;
            settings.AssignmentFormMessage = defaults.AssignmentFormMessage;
            settings.AssignmentSubmittedTitle = defaults.AssignmentSubmittedTitle;
            settings.AssignmentSubmittedMessage = defaults.AssignmentSubmittedMessage;
            settings.AssignmentApprovedTitle = defaults.AssignmentApprovedTitle;
            settings.AssignmentApprovedMessage = defaults.AssignmentApprovedMessage;
            settings.AssignmentRejectedTitle = defaults.AssignmentRejectedTitle;
            settings.AssignmentRejectedMessage = defaults.AssignmentRejectedMessage;
            settings.AssignmentRemovedTitle = defaults.AssignmentRemovedTitle;
            settings.AssignmentRemovedMessage = defaults.AssignmentRemovedMessage;
            settings.SuperAdminAssignedTitle = defaults.SuperAdminAssignedTitle;
            settings.SuperAdminAssignedMessage = defaults.SuperAdminAssignedMessage;
            settings.AdminAssignedTitle = defaults.AdminAssignedTitle;
            settings.AdminAssignedMessage = defaults.AdminAssignedMessage;
            settings.NewsEditorAssignedTitle = defaults.NewsEditorAssignedTitle;
            settings.NewsEditorAssignedMessage = defaults.NewsEditorAssignedMessage;
            settings.MapViewerAssignedTitle = defaults.MapViewerAssignedTitle;
            settings.MapViewerAssignedMessage = defaults.MapViewerAssignedMessage;
            settings.MemberAssignedTitle = defaults.MemberAssignedTitle;
            settings.MemberAssignedMessage = defaults.MemberAssignedMessage;
            settings.ProfileUpdatedTitle = defaults.ProfileUpdatedTitle;
            settings.ProfileUpdatedMessage = defaults.ProfileUpdatedMessage;
            settings.LastUpdated = DateTime.Now;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "✅ تم استعادة قوالب الإشعارات الافتراضية";
            return RedirectToAction(nameof(Index));
        }

        private async Task<SiteSettings> GetOrCreateSettingsAsync()
        {
            var settings = await _context.SiteSettings.FirstOrDefaultAsync();
            if (settings != null)
                return settings;

            settings = new SiteSettings();
            _context.SiteSettings.Add(settings);
            await _context.SaveChangesAsync();
            return settings;
        }

        private bool IsAllowedEmail()
        {
            var currentEmail = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrWhiteSpace(currentEmail))
            {
                return false;
            }

            var allowedEmails = _configuration
                .GetSection("SuperAdminPasswordReset:AllowedEmails")
                .Get<string[]>() ?? Array.Empty<string>();

            return allowedEmails.Contains(currentEmail, StringComparer.OrdinalIgnoreCase);
        }
    }
}
