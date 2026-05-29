// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Services;

namespace WebApplication2.Areas.Identity.Pages.Account
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly ApplicationDbContext _context;
        private readonly IOtpService _otpService;
        private readonly IWhatsAppService _whatsAppService;

        public ForgotPasswordModel(
            UserManager<IdentityUser> userManager,
            IEmailSender emailSender,
            ApplicationDbContext context,
            IOtpService otpService,
            IWhatsAppService whatsAppService)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _context = context;
            _otpService = otpService;
            _whatsAppService = whatsAppService;
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public class InputModel
        {
            [Required]
            [Display(Name = "البريد الإلكتروني أو رقم الواتساب")]
            public string Identifier { get; set; }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (ModelState.IsValid)
            {
                var identifier = Input.Identifier?.Trim() ?? string.Empty;
                if (!identifier.Contains("@", StringComparison.Ordinal) && LooksLikePhoneNumber(identifier))
                {
                    var normalizedPhone = NormalizeIraqPhoneNumber(identifier);
                    var resetPhone = await TrySendWhatsAppResetCodeAsync(normalizedPhone);
                    if (string.IsNullOrWhiteSpace(resetPhone))
                    {
                        TempData["ErrorMessage"] = "لم يتم إرسال كود واتساب. تأكد أن الرقم مربوط ومؤكد في حسابك أو تحقق من إعدادات خدمة الواتساب.";
                        resetPhone = normalizedPhone;
                    }

                    return RedirectToPage("./ResetPasswordWhatsApp", new { phoneNumber = resetPhone });
                }

                var user = await _userManager.FindByEmailAsync(identifier);
                if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
                {
                    // Don't reveal that the user does not exist or is not confirmed
                    return RedirectToPage("./ForgotPasswordConfirmation");
                }

                // For more information on how to enable account confirmation and password reset please
                // visit https://go.microsoft.com/fwlink/?LinkID=532713
                var code = await _userManager.GeneratePasswordResetTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                var callbackUrl = Url.Page(
                    "/Account/ResetPassword",
                    pageHandler: null,
                    values: new { area = "Identity", code },
                    protocol: Request.Scheme);

                await _emailSender.SendEmailAsync(
                    identifier,
                    "Reset Password",
                    $"Please reset your password by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

                return RedirectToPage("./ForgotPasswordConfirmation");
            }

            return Page();
        }

        private async Task<string> TrySendWhatsAppResetCodeAsync(string normalizedPhone)
        {
            if (string.IsNullOrWhiteSpace(normalizedPhone))
                return string.Empty;

            var localPhone = ToLocalIraqPhoneNumber(normalizedPhone);
            var possibleMatches = await _context.Identifies
                .AsNoTracking()
                .Where(i => i.IsWhatsAppVerified)
                .Select(i => new { i.UserId, i.WhatsAppNumber, i.PhoneNumber })
                .ToListAsync();

            var identify = possibleMatches.FirstOrDefault(i =>
                NormalizeIraqPhoneNumber(i.WhatsAppNumber ?? string.Empty) == normalizedPhone ||
                NormalizeIraqPhoneNumber(i.PhoneNumber ?? string.Empty) == normalizedPhone ||
                i.PhoneNumber == localPhone);

            if (identify == null)
                return string.Empty;

            var user = await _userManager.FindByIdAsync(identify.UserId);
            if (user == null)
                return string.Empty;

            var verifiedWhatsAppPhone = NormalizeIraqPhoneNumber(identify.WhatsAppNumber ?? string.Empty);
            if (string.IsNullOrWhiteSpace(verifiedWhatsAppPhone))
                return string.Empty;

            var code = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
            _otpService.StoreOtp(verifiedWhatsAppPhone, code);

            var sent = await _whatsAppService.SendMessageAsync(
                verifiedWhatsAppPhone,
                $"كود إعادة تعيين كلمة المرور هو: {code}");

            return sent ? verifiedWhatsAppPhone : string.Empty;
        }

        private static bool LooksLikePhoneNumber(string value)
        {
            var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
            return digits.Length >= 10;
        }

        private static string NormalizeIraqPhoneNumber(string phoneNumber)
        {
            var digits = new string((phoneNumber ?? string.Empty).Where(char.IsDigit).ToArray());
            if (string.IsNullOrWhiteSpace(digits))
                return string.Empty;

            if (digits.StartsWith("00", StringComparison.Ordinal))
                digits = digits[2..];

            if (digits.StartsWith("964", StringComparison.Ordinal))
                return digits;

            if (digits.StartsWith("0", StringComparison.Ordinal))
                return "964" + digits[1..];

            if (digits.StartsWith("7", StringComparison.Ordinal))
                return "964" + digits;

            return digits;
        }

        private static string ToLocalIraqPhoneNumber(string phoneNumber)
        {
            var normalizedPhone = NormalizeIraqPhoneNumber(phoneNumber);
            if (normalizedPhone.StartsWith("9647", StringComparison.Ordinal))
                return "0" + normalizedPhone[3..];

            return phoneNumber;
        }
    }
}
