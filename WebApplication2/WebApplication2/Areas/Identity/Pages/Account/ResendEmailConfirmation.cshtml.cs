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
    [AllowAnonymous]
    public class ResendEmailConfirmationModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly ApplicationDbContext _context;
        private readonly IOtpService _otpService;
        private readonly IWhatsAppService _whatsAppService;

        public ResendEmailConfirmationModel(
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

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var identifier = Input.Identifier?.Trim() ?? string.Empty;
            if (!identifier.Contains("@", StringComparison.Ordinal) && LooksLikePhoneNumber(identifier))
            {
                return await ResendWhatsAppConfirmationAsync(identifier);
            }

            var user = await _userManager.FindByEmailAsync(identifier);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "إذا كانت البيانات صحيحة، تم إرسال تعليمات التأكيد.");
                return Page();
            }

            if (await _userManager.IsEmailConfirmedAsync(user))
            {
                ModelState.AddModelError(string.Empty, "الحساب مؤكد مسبقاً.");
                return Page();
            }

            var userId = await _userManager.GetUserIdAsync(user);
            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var callbackUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new { userId = userId, code = code },
                protocol: Request.Scheme);
            await _emailSender.SendEmailAsync(
                identifier,
                "تأكيد البريد الإلكتروني",
                $"يرجى تأكيد حسابك من خلال الضغط على الرابط التالي: <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>تأكيد الحساب</a>.");

            ModelState.AddModelError(string.Empty, "تم إرسال رابط التأكيد. يرجى فحص بريدك الإلكتروني.");
            return Page();
        }

        private async Task<IActionResult> ResendWhatsAppConfirmationAsync(string phoneNumber)
        {
            var normalizedPhone = NormalizeIraqPhoneNumber(phoneNumber);
            var localPhone = ToLocalIraqPhoneNumber(normalizedPhone);

            var possibleMatches = await _context.Identifies
                .Where(i => !string.IsNullOrWhiteSpace(i.WhatsAppNumber) ||
                            !string.IsNullOrWhiteSpace(i.PhoneNumber))
                .Select(i => new
                {
                    i.UserId,
                    i.WhatsAppNumber,
                    i.PhoneNumber,
                    i.IsWhatsAppVerified
                })
                .ToListAsync();

            var identify = possibleMatches.FirstOrDefault(i =>
                NormalizeIraqPhoneNumber(i.WhatsAppNumber ?? string.Empty) == normalizedPhone ||
                NormalizeIraqPhoneNumber(i.PhoneNumber ?? string.Empty) == normalizedPhone ||
                i.PhoneNumber == localPhone);

            if (identify == null)
            {
                ModelState.AddModelError(string.Empty, "إذا كانت البيانات صحيحة، تم إرسال تعليمات التأكيد.");
                return Page();
            }

            var user = await _userManager.FindByIdAsync(identify.UserId);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "إذا كانت البيانات صحيحة، تم إرسال تعليمات التأكيد.");
                return Page();
            }

            var sendPhone = NormalizeIraqPhoneNumber(identify.WhatsAppNumber ?? string.Empty);
            if (string.IsNullOrWhiteSpace(sendPhone))
                sendPhone = normalizedPhone;

            if (identify.IsWhatsAppVerified && user.EmailConfirmed)
            {
                ModelState.AddModelError(string.Empty, "الحساب مؤكد مسبقاً.");
                return Page();
            }

            var code = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
            _otpService.StoreOtp(sendPhone, code);

            var sent = await _whatsAppService.SendMessageAsync(sendPhone, $"كود تأكيد حسابك هو: {code}");
            if (!sent)
            {
                ModelState.AddModelError(string.Empty, "تعذر إرسال كود واتساب. تحقق من الرقم أو إعدادات الخدمة.");
                return Page();
            }

            TempData["SuccessMessage"] = "تم إرسال كود التأكيد إلى واتساب.";
            return RedirectToAction("VerifyWhatsApp", "Register", new
            {
                userId = user.Id,
                phoneNumber = sendPhone
            });
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
