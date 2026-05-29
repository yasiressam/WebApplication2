// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace WebApplication2.Areas.Identity.Pages.Account
{
    public class ConfirmEmailModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<ConfirmEmailModel> _logger;

        public ConfirmEmailModel(UserManager<IdentityUser> userManager, ILogger<ConfirmEmailModel> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

        [TempData]
        public string StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(string userId, string code)
        {
            _logger.LogInformation($"=== بدء تأكيد البريد الإلكتروني ===");
            _logger.LogInformation($"UserId: {userId}");
            _logger.LogInformation($"Code (original): {code}");

            if (userId == null || code == null)
            {
                StatusMessage = "رابط التأكيد غير صالح";
                return Page();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                StatusMessage = $"لم يتم العثور على المستخدم";
                return Page();
            }

            try
            {
                string decodedCode = code;

                // 1. إذا كان الكود يحتوي على %، فك تشفيره من URL
                if (code.Contains("%"))
                {
                    decodedCode = System.Web.HttpUtility.UrlDecode(code);
                    _logger.LogInformation($"تم فك ترميز URL: {decodedCode}");
                }

                // 2. محاولة فك التشفير باستخدام Base64Url
                string finalCode = decodedCode;
                try
                {
                    // بعض الأحيان الكود يأتي مشفراً بـ Base64Url
                    var base64Decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(decodedCode));
                    finalCode = base64Decoded;
                    _logger.LogInformation($"تم فك ترميز Base64Url بنجاح");
                }
                catch (Exception ex)
                {
                    _logger.LogInformation($"الكود ليس بصيغة Base64Url: {ex.Message}");
                    // استخدم الكود الأصلي
                    finalCode = decodedCode;
                }

                _logger.LogInformation($"Final code for confirmation: {finalCode}");

                var result = await _userManager.ConfirmEmailAsync(user, finalCode);

                if (result.Succeeded)
                {
                    _logger.LogInformation($"✅ تم تأكيد البريد الإلكتروني للمستخدم: {userId}");
                    StatusMessage = "✅ تم تأكيد بريدك الإلكتروني بنجاح! يمكنك الآن تسجيل الدخول.";
                    return RedirectToPage("/Account/Login", new { area = "Identity" });
                }
                else
                {
                    _logger.LogWarning($"❌ فشل تأكيد البريد للمستخدم: {userId}");
                    foreach (var error in result.Errors)
                    {
                        _logger.LogWarning($"خطأ: {error.Description}");
                    }
                    StatusMessage = "❌ فشل تأكيد البريد الإلكتروني. الرابط قد يكون منتهي الصلاحية أو غير صالح.";
                    return Page();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في تأكيد البريد الإلكتروني");
                StatusMessage = "حدث خطأ أثناء تأكيد البريد الإلكتروني";
                return Page();
            }
        }
    }
}