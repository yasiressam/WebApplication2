#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Services;

namespace WebApplication2.Areas.Identity.Pages.Account
{
    public class ResetPasswordWhatsAppModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IOtpService _otpService;

        public ResetPasswordWhatsAppModel(
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

        public class InputModel
        {
            [Required]
            public string PhoneNumber { get; set; }

            [Required(ErrorMessage = "كود التحقق مطلوب")]
            [Display(Name = "كود التحقق")]
            public string Code { get; set; }

            [Required(ErrorMessage = "كلمة المرور الجديدة مطلوبة")]
            [StringLength(100, ErrorMessage = "كلمة المرور يجب أن تكون 6 أحرف على الأقل.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "كلمة المرور الجديدة")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "تأكيد كلمة المرور")]
            [Compare("Password", ErrorMessage = "كلمة المرور وتأكيدها غير متطابقين.")]
            public string ConfirmPassword { get; set; }
        }

        public IActionResult OnGet(string phoneNumber)
        {
            var normalizedPhone = NormalizeIraqPhoneNumber(phoneNumber);
            if (string.IsNullOrWhiteSpace(normalizedPhone))
                return RedirectToPage("./ForgotPassword");

            Input = new InputModel { PhoneNumber = normalizedPhone };
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            Input.PhoneNumber = NormalizeIraqPhoneNumber(Input.PhoneNumber);

            if (!ModelState.IsValid)
                return Page();

            var verifyResult = await _otpService.ValidateOtpAsync(Input.PhoneNumber, Input.Code);
            if (!verifyResult.Success)
            {
                ModelState.AddModelError(nameof(Input.Code), verifyResult.Message);
                return Page();
            }

            var user = await FindVerifiedWhatsAppUserAsync(Input.PhoneNumber);
            if (user == null)
                return RedirectToPage("./ResetPasswordConfirmation");

            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, resetToken, Input.Password);
            if (result.Succeeded)
                return RedirectToPage("./ResetPasswordConfirmation");

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }

        public async Task<IActionResult> OnPostResendAsync()
        {
            var normalizedPhone = NormalizeIraqPhoneNumber(Input.PhoneNumber);
            if (string.IsNullOrWhiteSpace(normalizedPhone))
                return RedirectToPage("./ForgotPassword");

            var user = await FindVerifiedWhatsAppUserAsync(normalizedPhone);
            if (user != null)
            {
                var result = await _otpService.SendResetPasswordCodeAsync(normalizedPhone);
                TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Success
                    ? "تم إرسال كود جديد لإعادة تعيين كلمة المرور."
                    : result.Message;
            }
            else
            {
                TempData["ErrorMessage"] = "لم يتم العثور على رقم واتساب مؤكد لهذا الحساب.";
            }

            return RedirectToPage("./ResetPasswordWhatsApp", new { phoneNumber = normalizedPhone });
        }

        private async Task<IdentityUser> FindVerifiedWhatsAppUserAsync(string normalizedPhone)
        {
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
                return null;

            return await _userManager.FindByIdAsync(identify.UserId);
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
