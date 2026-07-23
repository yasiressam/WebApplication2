// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Services;

namespace WebApplication2.Areas.Identity.Pages.Account
{
    public class ResetPasswordModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IOtpService _otpService;

        public ResetPasswordModel(
            UserManager<IdentityUser> userManager,
            ApplicationDbContext context,
            IOtpService otpService)
        {
            _userManager = userManager;
            _context = context;
            _otpService = otpService;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public bool IsWhatsAppMode =>
            string.Equals(Input?.Mode, "whatsapp", StringComparison.OrdinalIgnoreCase);

        public class InputModel
        {
            [Required]
            [Display(Name = "البريد الإلكتروني أو رقم الواتساب")]
            public string Identifier { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "كلمة المرور يجب أن تكون بين 6 و100 حرف.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "كلمة المرور الجديدة")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "تأكيد كلمة المرور")]
            [Compare("Password", ErrorMessage = "كلمتا المرور غير متطابقتين.")]
            public string ConfirmPassword { get; set; }

            [Required]
            public string Code { get; set; }

            public string Mode { get; set; } = "email";
        }

        public IActionResult OnGet(string code = null, string identifier = null, string mode = null)
        {
            var normalizedMode = string.Equals(mode, "whatsapp", StringComparison.OrdinalIgnoreCase)
                ? "whatsapp"
                : "email";

            if (normalizedMode == "email")
            {
                if (code == null)
                {
                    return BadRequest("A code must be supplied for password reset.");
                }

                Input = new InputModel
                {
                    Identifier = identifier,
                    Mode = "email",
                    Code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code))
                };

                return Page();
            }

            if (string.IsNullOrWhiteSpace(identifier))
            {
                return RedirectToPage("./ForgotPassword");
            }

            Input = new InputModel
            {
                Identifier = NormalizeIraqPhoneNumber(identifier),
                Mode = "whatsapp",
                Code = string.Empty
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            if (string.Equals(Input.Mode, "whatsapp", StringComparison.OrdinalIgnoreCase))
            {
                var normalizedPhone = NormalizeIraqPhoneNumber(Input.Identifier);
                if (string.IsNullOrWhiteSpace(normalizedPhone))
                {
                    ModelState.AddModelError(string.Empty, "رقم الواتساب غير صالح.");
                    return Page();
                }

                var localPhone = ToLocalIraqPhoneNumber(normalizedPhone);
                var profile = await _context.Identifies
                    .AsNoTracking()
                    .Where(i => i.WhatsAppNumber == normalizedPhone || i.PhoneNumber == localPhone)
                    .Select(i => new { i.UserId })
                    .FirstOrDefaultAsync();

                if (profile == null)
                {
                    return RedirectToPage("./ResetPasswordConfirmation");
                }

                var validateResult = await _otpService.ValidateOtpAsync(normalizedPhone, Input.Code);
                if (!validateResult.Success)
                {
                    ModelState.AddModelError(string.Empty, validateResult.Message);
                    return Page();
                }

                var userByPhone = await _userManager.FindByIdAsync(profile.UserId);
                if (userByPhone == null)
                {
                    return RedirectToPage("./ResetPasswordConfirmation");
                }

                var resetToken = await _userManager.GeneratePasswordResetTokenAsync(userByPhone);
                var resetResult = await _userManager.ResetPasswordAsync(userByPhone, resetToken, Input.Password);
                if (resetResult.Succeeded)
                {
                    return RedirectToPage("./ResetPasswordConfirmation");
                }

                foreach (var error in resetResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return Page();
            }

            var user = await _userManager.FindByEmailAsync(Input.Identifier);
            if (user == null)
            {
                return RedirectToPage("./ResetPasswordConfirmation");
            }

            var result = await _userManager.ResetPasswordAsync(user, Input.Code, Input.Password);
            if (result.Succeeded)
            {
                return RedirectToPage("./ResetPasswordConfirmation");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
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
