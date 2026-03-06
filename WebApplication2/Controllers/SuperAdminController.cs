using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebApplication2.Data;
using WebApplication2.Models;
using WebApplication2.Services;

namespace WebApplication2.Controllers
{
    [Authorize(Roles = clsRoles.SuperAdmin)]
    public class SuperAdminController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;
        private readonly ILogger<SuperAdminController> _logger;

        public SuperAdminController(
            UserManager<IdentityUser> userManager,
            ApplicationDbContext context,
            INotificationService notificationService,
            ILogger<SuperAdminController> logger)
        {
            _userManager = userManager;
            _context = context;
            _notificationService = notificationService;
            _logger = logger;
        }

        // GET: عرض جميع المستخدمين
        public async Task<IActionResult> Users()
        {
            var users = _userManager.Users.ToList();
            var list = new List<SuperAdminUserVM>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var governorate = "غير محدد";
                string managedGovernorate = null;

                var userProfile = await _context.Identifies
                    .Include(i => i.Address)
                    .FirstOrDefaultAsync(i => i.UserId == user.Id);

                if (userProfile != null)
                {
                    if (roles.Contains(clsRoles.Admin) && !string.IsNullOrEmpty(userProfile.ManagedGovernorate))
                    {
                        managedGovernorate = userProfile.ManagedGovernorate;
                        governorate = $"👑 {managedGovernorate} (مدير)";
                    }
                    else if (userProfile.Address != null)
                    {
                        governorate = userProfile.Address.Governorate ?? "غير محدد";
                    }
                }

                list.Add(new SuperAdminUserVM
                {
                    Id = user.Id,
                    Email = user.Email,
                    Roles = string.Join(", ", roles),
                    Governorate = governorate,
                    ManagedGovernorate = managedGovernorate,
                    IsActive = user.EmailConfirmed,
                    FullName = userProfile?.FullName ?? "غير مكتمل"
                });
            }

            return View(list);
        }

        // GET: /SuperAdmin/SendNotification
        [HttpGet]
        public async Task<IActionResult> SendNotification(string? userId = null)
        {
            var users = await _userManager.Users.ToListAsync();
            ViewBag.Users = users.Select(u => new
            {
                u.Id,
                u.Email,
                FullName = _context.Identifies.FirstOrDefault(i => i.UserId == u.Id)?.FullName ?? u.Email
            }).ToList();

            if (!string.IsNullOrEmpty(userId))
            {
                var targetUser = await _userManager.FindByIdAsync(userId);
                if (targetUser != null)
                {
                    ViewBag.TargetUserId = userId;
                    ViewBag.TargetUserEmail = targetUser.Email;
                    ViewBag.TargetUserName = _context.Identifies
                        .FirstOrDefault(i => i.UserId == userId)?.FullName ?? targetUser.Email;
                }
            }

            return View();
        }

        // POST: /SuperAdmin/SendNotification
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendNotification(SendNotificationViewModel model)
        {
            try
            {
                // التحقق من صحة البيانات
                if (string.IsNullOrEmpty(model.Title) || string.IsNullOrEmpty(model.Message))
                {
                    TempData["ErrorMessage"] = "❌ العنوان والرسالة مطلوبان";
                    return RedirectToAction("SendNotification");
                }

                _logger.LogInformation("🚀 بدء إرسال إشعار جديد");
                _logger.LogInformation($"📝 العنوان: {model.Title}");
                _logger.LogInformation($"📝 الرسالة: {model.Message}");
                _logger.LogInformation($"👤 المستلم: {model.TargetUserId ?? "الكل"}");

                // 1. إنشاء الإشعار في قاعدة البيانات
                var notification = await _notificationService.CreateNotification(
                    model.Title,
                    model.Message,
                    model.TargetUserId,
                    model.Icon ?? "bi-bell",
                    model.ClickUrl
                );

                _logger.LogInformation($"✅ تم إنشاء الإشعار في قاعدة البيانات: ID = {notification.Id}");

                // 2. إرسال عبر OneSignal
                bool oneSignalResult = false;
                string oneSignalMessage = "";

                try
                {
                    if (!string.IsNullOrEmpty(model.TargetUserId))
                    {
                        // إرسال لمستخدم معين
                        _logger.LogInformation($"📱 البحث عن أجهزة للمستخدم: {model.TargetUserId}");

                        var playerIds = await _context.UserDevices
                            .Where(d => d.UserId == model.TargetUserId && d.IsSubscribed)
                            .Select(d => d.PlayerId)
                            .ToListAsync();

                        _logger.LogInformation($"📱 تم العثور على {playerIds.Count} جهاز");

                        if (playerIds.Any())
                        {
                            oneSignalResult = await _notificationService.SendToOneSignal(notification, playerIds);
                            oneSignalMessage = $"تم الإرسال إلى {playerIds.Count} جهاز";
                        }
                        else
                        {
                            _logger.LogInformation("👤 لا توجد أجهزة مسجلة، إرسال باستخدام external_user_id");
                            oneSignalResult = await _notificationService.SendToOneSignal(notification, null, model.TargetUserId);
                            oneSignalMessage = "تم الإرسال باستخدام معرف المستخدم";
                        }
                    }
                    else
                    {
                        // إرسال للجميع
                        _logger.LogInformation("🌍 إرسال للجميع");
                        oneSignalResult = await _notificationService.SendToOneSignal(notification);
                        oneSignalMessage = "تم الإرسال لجميع المستخدمين";
                    }

                    if (oneSignalResult)
                    {
                        _logger.LogInformation("✅ تم إرسال الإشعار عبر OneSignal بنجاح");
                        TempData["SuccessMessage"] = $"✅ تم إرسال الإشعار بنجاح. {oneSignalMessage}";
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ فشل إرسال الإشعار عبر OneSignal");
                        TempData["WarningMessage"] = "⚠️ تم حفظ الإشعار في قاعدة البيانات ولكن فشل الإرسال عبر OneSignal. يرجى التحقق من إعدادات OneSignal.";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ خطأ في إرسال OneSignal");
                    TempData["WarningMessage"] = $"⚠️ تم حفظ الإشعار ولكن حدث خطأ في الإرسال: {ex.Message}";
                }

                return RedirectToAction("Users");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ عام في إرسال الإشعار");
                TempData["ErrorMessage"] = $"❌ حدث خطأ: {ex.Message}";
                return RedirectToAction("SendNotification");
            }
        }

        // GET: جلب أدوار المستخدم
        [HttpGet]
        public async Task<IActionResult> GetUserRoles(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                    return Json(new { success = false, message = "معرف المستخدم مطلوب" });

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                    return Json(new { success = false, message = "المستخدم غير موجود" });

                var userRoles = await _userManager.GetRolesAsync(user);
                var allRoles = new List<string> { "User", "Admin", "SuperAdmin" };

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        UserId = user.Id,
                        UserEmail = user.Email,
                        CurrentRoles = userRoles,
                        AllRoles = allRoles
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: تحديث أدوار المستخدم
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateUserRoles([FromBody] UpdateRolesRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.UserId))
                    return Json(new { success = false, message = "بيانات غير صالحة" });

                var user = await _userManager.FindByIdAsync(request.UserId);
                if (user == null)
                    return Json(new
                    {
                        success = false,
                        message = $"المستخدم غير موجود. ID: {request.UserId}"
                    });

                var currentRoles = await _userManager.GetRolesAsync(user);

                if (currentRoles.Any())
                {
                    var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
                    if (!removeResult.Succeeded)
                    {
                        var errors = string.Join(", ", removeResult.Errors.Select(e => e.Description));
                        return Json(new { success = false, message = "فشل في إزالة الأدوار الحالية: " + errors });
                    }
                }

                if (request.SelectedRoles != null && request.SelectedRoles.Any())
                {
                    var addResult = await _userManager.AddToRolesAsync(user, request.SelectedRoles);
                    if (!addResult.Succeeded)
                    {
                        var errors = string.Join(", ", addResult.Errors.Select(e => e.Description));
                        return Json(new { success = false, message = "فشل في إضافة الأدوار الجديدة: " + errors });
                    }
                }

                var isAdminNow = request.SelectedRoles != null && request.SelectedRoles.Contains(clsRoles.Admin);
                if (isAdminNow)
                {
                    var userProfile = await _context.Identifies
                        .Include(i => i.Address)
                        .FirstOrDefaultAsync(i => i.UserId == request.UserId);

                    if (userProfile != null)
                    {
                        string governorate = null;

                        if (userProfile.Address != null && !string.IsNullOrEmpty(userProfile.Address.Governorate))
                        {
                            governorate = userProfile.Address.Governorate;
                        }
                        else
                        {
                            governorate = "بغداد";
                        }

                        userProfile.ManagedGovernorate = governorate;
                        _context.Update(userProfile);
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        userProfile = new Identify
                        {
                            UserId = request.UserId,
                            FullName = "مدير نظام",
                            ManagedGovernorate = "بغداد",
                            Date = DateTime.Now,
                            Gender = "ذكر",
                            PhoneNumber = "",
                            IdentityCardN = 0,
                            identityDate = DateTime.Now
                        };
                        _context.Identifies.Add(userProfile);
                        await _context.SaveChangesAsync();
                    }
                }
                else if (request.SelectedRoles != null && !request.SelectedRoles.Contains(clsRoles.Admin))
                {
                    var userProfile = await _context.Identifies
                        .FirstOrDefaultAsync(i => i.UserId == request.UserId);

                    if (userProfile != null)
                    {
                        userProfile.ManagedGovernorate = null;
                        _context.Update(userProfile);
                        await _context.SaveChangesAsync();
                    }
                }

                var finalRoles = await _userManager.GetRolesAsync(user);

                return Json(new
                {
                    success = true,
                    message = "✅ تم تحديث أدوار المستخدم بنجاح",
                    newRoles = finalRoles.ToList(),
                    userEmail = user.Email
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"❌ حدث خطأ: {ex.Message}" });
            }
        }

        // GET: عرض تفاصيل مستخدم
        public async Task<IActionResult> UserDetails(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    TempData["ErrorMessage"] = "معرف المستخدم مطلوب";
                    return RedirectToAction(nameof(Users));
                }

                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "المستخدم غير موجود";
                    return RedirectToAction(nameof(Users));
                }

                var userProfile = await _context.Identifies
                    .Include(i => i.Address)
                    .FirstOrDefaultAsync(i => i.UserId == id);

                var roles = await _userManager.GetRolesAsync(user);

                var viewModel = new SuperAdminUserDetailsVM
                {
                    UserId = user.Id,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber ?? "",
                    IsActive = user.EmailConfirmed,
                    Roles = string.Join(", ", roles),
                    FullName = userProfile?.FullName ?? "غير مكتمل",
                    LastName = userProfile?.LastName ?? "",
                    MotherName = userProfile?.MotherName ?? "",
                    DateOfBirth = userProfile?.Date ?? DateTime.MinValue,
                    Gender = userProfile?.Gender ?? "غير محدد",
                    MozakeName = userProfile?.MozakeName ?? "",
                    Education = userProfile?.Education ?? "",
                    Specialization = userProfile?.Specialization ?? "",
                    IdentityCardN = userProfile?.IdentityCardN ?? 0,
                    IdentityDate = userProfile?.identityDate ?? DateTime.MinValue,
                    RationN = userProfile?.RationN ?? 0,
                    RationCenter = userProfile?.RationCenter ?? 0,
                    Address = userProfile?.Address,
                    ManagedGovernorate = userProfile?.ManagedGovernorate
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "حدث خطأ في تحميل البيانات";
                return RedirectToAction(nameof(Users));
            }
        }

        // POST: حذف مستخدم
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser([FromBody] DeleteUserRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.UserId))
                    return Json(new { success = false, message = "معرف المستخدم مطلوب" });

                var user = await _userManager.FindByIdAsync(request.UserId);
                if (user == null)
                    return Json(new { success = false, message = "المستخدم غير موجود" });

                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains(clsRoles.SuperAdmin))
                {
                    return Json(new { success = false, message = "لا يمكن حذف حساب سوبر أدمن" });
                }

                var userProfile = await _context.Identifies
                    .Include(i => i.Address)
                    .FirstOrDefaultAsync(i => i.UserId == request.UserId);

                if (userProfile != null)
                {
                    if (userProfile.Address != null)
                    {
                        _context.Addresses.Remove(userProfile.Address);
                    }
                    _context.Identifies.Remove(userProfile);
                    await _context.SaveChangesAsync();
                }

                var result = await _userManager.DeleteAsync(user);

                if (result.Succeeded)
                {
                    return Json(new
                    {
                        success = true,
                        message = "✅ تم حذف المستخدم وجميع بياناته بنجاح"
                    });
                }

                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return Json(new { success = false, message = $"❌ فشل في حذف المستخدم: {errors}" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"❌ حدث خطأ: {ex.Message}" });
            }
        }

        // POST: تفعيل/تعطيل المستخدم
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUserStatus([FromBody] ToggleStatusRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.UserId))
                    return Json(new { success = false, message = "معرف المستخدم مطلوب" });

                var user = await _userManager.FindByIdAsync(request.UserId);
                if (user == null)
                    return Json(new { success = false, message = "المستخدم غير موجود" });

                user.EmailConfirmed = !user.EmailConfirmed;
                var result = await _userManager.UpdateAsync(user);

                if (result.Succeeded)
                {
                    var status = user.EmailConfirmed ? "مفعل" : "معطل";
                    return Json(new
                    {
                        success = true,
                        message = $"✅ تم {status} حساب المستخدم",
                        isActive = user.EmailConfirmed
                    });
                }

                return Json(new { success = false, message = "❌ فشل في تغيير حالة المستخدم" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"❌ حدث خطأ: {ex.Message}" });
            }
        }
    }
}