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

namespace WebApplication2.Controllers
{
    [Authorize(Roles = clsRoles.SuperAdmin)]
    public class SuperAdminController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ApplicationDbContext _context;

        public SuperAdminController(
            UserManager<IdentityUser> userManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
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
                    // 👇 **هنا التعديل: إذا كان أدمن، نعرض المحافظة التي يديرها**
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

                // الحصول على الأدوار الحالية
                var currentRoles = await _userManager.GetRolesAsync(user);

                // إزالة الأدوار الحالية
                if (currentRoles.Any())
                {
                    var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
                    if (!removeResult.Succeeded)
                    {
                        var errors = string.Join(", ", removeResult.Errors.Select(e => e.Description));
                        return Json(new { success = false, message = "فشل في إزالة الأدوار الحالية: " + errors });
                    }
                }

                // إضافة الأدوار الجديدة
                if (request.SelectedRoles != null && request.SelectedRoles.Any())
                {
                    var addResult = await _userManager.AddToRolesAsync(user, request.SelectedRoles);
                    if (!addResult.Succeeded)
                    {
                        var errors = string.Join(", ", addResult.Errors.Select(e => e.Description));
                        return Json(new { success = false, message = "فشل في إضافة الأدوار الجديدة: " + errors });
                    }
                }

                // 👇 **إذا أصبح أدمن، نعطيه محافظته تلقائيًا**
                var isAdminNow = request.SelectedRoles != null && request.SelectedRoles.Contains(clsRoles.Admin);
                if (isAdminNow)
                {
                    var userProfile = await _context.Identifies
                        .Include(i => i.Address)
                        .FirstOrDefaultAsync(i => i.UserId == request.UserId);

                    if (userProfile != null)
                    {
                        string governorate = null;

                        // الحصول على محافظة المستخدم من عنوانه
                        if (userProfile.Address != null && !string.IsNullOrEmpty(userProfile.Address.Governorate))
                        {
                            governorate = userProfile.Address.Governorate;
                        }
                        else
                        {
                            // إذا لم يكن له عنوان، نعطيه محافظة افتراضية
                            governorate = "بغداد";
                        }

                        userProfile.ManagedGovernorate = governorate;
                        _context.Update(userProfile);
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        // إذا لم يكن له ملف شخصي، ننشئ واحدًا
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
                    // 👇 **إذا لم يعد أدمن، نحذف محافظته**
                    var userProfile = await _context.Identifies
                        .FirstOrDefaultAsync(i => i.UserId == request.UserId);

                    if (userProfile != null)
                    {
                        userProfile.ManagedGovernorate = null;
                        _context.Update(userProfile);
                        await _context.SaveChangesAsync();
                    }
                }

                // الحصول على الأدوار النهائية
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

        // GET: عرض تفاصيل مستخدم (للسوبر أدمن)
        public async Task<IActionResult> UserDetails(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    TempData["ErrorMessage"] = "معرف المستخدم مطلوب";
                    return RedirectToAction(nameof(Users));
                }

                // الحصول على المستخدم من Identity
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "المستخدم غير موجود";
                    return RedirectToAction(nameof(Users));
                }

                // الحصول على الملف الشخصي
                var userProfile = await _context.Identifies
                    .Include(i => i.Address)
                    .FirstOrDefaultAsync(i => i.UserId == id);

                // الحصول على الأدوار
                var roles = await _userManager.GetRolesAsync(user);

                // ViewModel للعرض
                var viewModel = new SuperAdminUserDetailsVM
                {
                    UserId = user.Id,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber ?? "",
                    IsActive = user.EmailConfirmed,
                    Roles = string.Join(", ", roles),

                    // المعلومات الشخصية
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

                    // معلومات العنوان
                    Address = userProfile?.Address,

                    // المحافظة المدارة (إذا كان أدمن)
                    ManagedGovernorate = userProfile?.ManagedGovernorate
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                // Log the error
                TempData["ErrorMessage"] = "حدث خطأ في تحميل البيانات";
                return RedirectToAction(nameof(Users));
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

    public class UpdateRolesRequest
    {
        public string UserId { get; set; }
        public List<string> SelectedRoles { get; set; }
    }

    public class ToggleStatusRequest
    {
        public string UserId { get; set; }
    }
}