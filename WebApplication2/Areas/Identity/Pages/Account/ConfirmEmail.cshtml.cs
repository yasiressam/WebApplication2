// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Services;

namespace WebApplication2.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ConfirmEmailModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<ConfirmEmailModel> _logger;
        private readonly ApplicationDbContext _context;
        private readonly IOtpService _otpService;

        public ConfirmEmailModel(
            UserManager<IdentityUser> userManager,
            ILogger<ConfirmEmailModel> logger,
            ApplicationDbContext context,
            IOtpService otpService)
        {
            _userManager = userManager;
            _logger = logger;
            _context = context;
            _otpService = otpService;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        [TempData]
        public string SuccessMessage { get; set; }

        public bool IsWhatsAppMode =>
            string.Equals(Input?.Mode, "whatsapp", StringComparison.OrdinalIgnoreCase);

        public class InputModel
        {
            [Required]
            public string UserId { get; set; }

            [Required]
            public string PhoneNumber { get; set; }

            [Required]
            [Display(Name = "كود التأكيد")]
            public string OtpCode { get; set; }

            public string Mode { get; set; } = "email";
        }

        public async Task<IActionResult> OnGetAsync(string userId, string code, string phoneNumber = null, string mode = null)
        {
            var normalizedMode = string.Equals(mode, "whatsapp", StringComparison.OrdinalIgnoreCase)
                ? "whatsapp"
                : "email";

            if (normalizedMode == "whatsapp")
            {
                if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(phoneNumber))
                {
                    StatusMessage = "بيانات تأكيد الواتساب غير صالحة";
                    return Page();
                }

                Input = new InputModel
                {
                    UserId = userId,
                    PhoneNumber = NormalizeIraqPhoneNumber(phoneNumber),
                    Mode = "whatsapp",
                    OtpCode = string.Empty
                };

                return Page();
            }

            if (userId == null || code == null)
            {
                StatusMessage = "رابط التأكيد غير صالح";
                return Page();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                StatusMessage = "لم يتم العثور على المستخدم";
                return Page();
            }

            try
            {
                var decodedCode = code;
                if (code.Contains("%", StringComparison.Ordinal))
                {
                    decodedCode = System.Web.HttpUtility.UrlDecode(code);
                }

                string finalCode;
                try
                {
                    finalCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(decodedCode));
                }
                catch
                {
                    finalCode = decodedCode;
                }

                var result = await _userManager.ConfirmEmailAsync(user, finalCode);
                if (result.Succeeded)
                {
                    SuccessMessage = "تم تأكيد بريدك الإلكتروني بنجاح. يمكنك الآن تسجيل الدخول.";
                    return RedirectToPage("/Account/Login", new { area = "Identity" });
                }

                StatusMessage = "فشل تأكيد البريد الإلكتروني. الرابط قد يكون منتهي الصلاحية أو غير صالح.";
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في تأكيد البريد الإلكتروني");
                StatusMessage = "حدث خطأ أثناء تأكيد البريد الإلكتروني";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            if (!string.Equals(Input.Mode, "whatsapp", StringComparison.OrdinalIgnoreCase))
            {
                StatusMessage = "طلب التأكيد غير صالح.";
                return Page();
            }

            var normalizedPhone = NormalizeIraqPhoneNumber(Input.PhoneNumber);
            var validateResult = await _otpService.ValidateOtpAsync(normalizedPhone, Input.OtpCode);
            if (!validateResult.Success)
            {
                ModelState.AddModelError(string.Empty, validateResult.Message);
                return Page();
            }

            var user = await _userManager.FindByIdAsync(Input.UserId);
            if (user == null)
            {
                StatusMessage = "لم يتم العثور على المستخدم";
                return Page();
            }

            var localPhone = ToLocalIraqPhoneNumber(normalizedPhone);
            var profile = await _context.Identifies
                .FirstOrDefaultAsync(i => i.UserId == user.Id && (i.WhatsAppNumber == normalizedPhone || i.PhoneNumber == localPhone));

            if (profile == null)
            {
                StatusMessage = "لم يتم العثور على ملف المستخدم";
                return Page();
            }

            profile.IsWhatsAppVerified = true;
            profile.WhatsAppVerifiedAt = DateTime.UtcNow;
            user.EmailConfirmed = true;

            await _context.SaveChangesAsync();
            await _userManager.UpdateAsync(user);

            SuccessMessage = "تم تأكيد حسابك بنجاح. يمكنك الآن تسجيل الدخول.";
            return RedirectToPage("/Account/Login", new { area = "Identity" });
        }

        private static string NormalizeIraqPhoneNumber(string phoneNumber)
        {
            var digits = new string((phoneNumber ?? string.Empty).Where(char.IsDigit).ToArray());
            if (string.IsNullOrWhiteSpace(digits))
            {
                return string.Empty;
            }

            if (digits.StartsWith("00", StringComparison.Ordinal))
            {
                digits = digits[2..];
            }

            if (digits.StartsWith("964", StringComparison.Ordinal))
            {
                return digits;
            }

            if (digits.StartsWith("0", StringComparison.Ordinal))
            {
                return "964" + digits[1..];
            }

            if (digits.StartsWith("7", StringComparison.Ordinal))
            {
                return "964" + digits;
            }

            return digits;
        }

        private static string ToLocalIraqPhoneNumber(string phoneNumber)
        {
            var normalizedPhone = NormalizeIraqPhoneNumber(phoneNumber);
            if (normalizedPhone.StartsWith("9647", StringComparison.Ordinal))
            {
                return "0" + normalizedPhone[3..];
            }

            return phoneNumber;
        }
    }
}
