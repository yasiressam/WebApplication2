// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
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

        public ForgotPasswordModel(
            UserManager<IdentityUser> userManager,
            IEmailSender emailSender,
            ApplicationDbContext context,
            IOtpService otpService)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _context = context;
            _otpService = otpService;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            [Display(Name = "البريد الإلكتروني أو رقم الواتساب")]
            public string Identifier { get; set; }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var identifier = Input.Identifier?.Trim() ?? string.Empty;
            var user = await _userManager.FindByEmailAsync(identifier);
            if (user != null)
            {
                if (!(await _userManager.IsEmailConfirmedAsync(user)))
                {
                    return RedirectToPage("./ForgotPasswordConfirmation");
                }

                var code = await _userManager.GeneratePasswordResetTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                var callbackUrl = Url.Page(
                    "/Account/ResetPassword",
                    pageHandler: null,
                    values: new { area = "Identity", code, identifier, mode = "email" },
                    protocol: Request.Scheme);

                await _emailSender.SendEmailAsync(
                    identifier,
                    "إعادة تعيين كلمة المرور",
                    $"يرجى إعادة تعيين كلمة المرور من خلال <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>الضغط هنا</a>.");

                return RedirectToPage("./ForgotPasswordConfirmation");
            }

            var normalizedPhone = NormalizeIraqPhoneNumber(identifier);
            if (string.IsNullOrWhiteSpace(normalizedPhone))
            {
                return RedirectToPage("./ForgotPasswordConfirmation");
            }

            var localPhone = ToLocalIraqPhoneNumber(normalizedPhone);
            var profile = await _context.Identifies
                .AsNoTracking()
                .Where(i => i.WhatsAppNumber == normalizedPhone || i.PhoneNumber == localPhone)
                .Select(i => new { i.UserId })
                .FirstOrDefaultAsync();

            if (profile == null)
            {
                return RedirectToPage("./ForgotPasswordConfirmation");
            }

            var whatsappUser = await _userManager.FindByIdAsync(profile.UserId);
            if (whatsappUser == null)
            {
                return RedirectToPage("./ForgotPasswordConfirmation");
            }

            var otpResult = await _otpService.SendResetPasswordCodeAsync(normalizedPhone);
            if (!otpResult.Success)
            {
                ModelState.AddModelError(string.Empty, otpResult.Message);
                return Page();
            }

            return RedirectToPage("./ResetPassword", new
            {
                area = "Identity",
                identifier = normalizedPhone,
                mode = "whatsapp"
            });
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
