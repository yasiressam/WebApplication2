// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WebApplication2.Data;
using WebApplication2.Models.Audit;
using WebApplication2.Services;

namespace WebApplication2.Areas.Identity.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<LoginModel> _logger;
        private readonly IAuditTrailService _auditTrailService;

        public LoginModel(
            SignInManager<IdentityUser> signInManager,
            UserManager<IdentityUser> userManager,
            ApplicationDbContext context,
            ILogger<LoginModel> logger,
            IAuditTrailService auditTrailService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
            _logger = logger;
            _auditTrailService = auditTrailService;
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
        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string ReturnUrl { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [TempData]
        public string ErrorMessage { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public class InputModel
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [Display(Name = "البريد الإلكتروني أو رقم الواتساب")]
            public string Email { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");

            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {
                var loginIdentifier = Input.Email.Trim();
                var loginByPhone = !loginIdentifier.Contains('@');

                IdentityUser user;
                if (loginByPhone)
                {
                    var normalizedPhone = NormalizeIraqPhoneNumber(loginIdentifier);
                    var identify = await _context.Identifies
                        .FirstOrDefaultAsync(i =>
                            i.IsWhatsAppVerified &&
                            i.WhatsAppNumber == normalizedPhone);

                    user = identify != null
                        ? await _userManager.FindByIdAsync(identify.UserId)
                        : null;
                }
                else
                {
                    user = await _userManager.FindByEmailAsync(loginIdentifier);
                }

                if (user == null)
                {
                    await LogLoginAttemptAsync(loginIdentifier, false, "محاولة دخول بحساب غير موجود");
                    ModelState.AddModelError(string.Empty, "بيانات الدخول أو كلمة المرور غير صحيحة.");
                    return Page();
                }

                // This doesn't count login failures towards account lockout
                // To enable password failures to trigger account lockout, set lockoutOnFailure: true
                var result = await _signInManager.PasswordSignInAsync(user.UserName, Input.Password, Input.RememberMe, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    _logger.LogInformation("User logged in.");
                    await LogLoginAttemptAsync(user.Email ?? loginIdentifier, true, "تم تسجيل الدخول بنجاح", user);
                    return LocalRedirect(returnUrl);
                }
                if (result.RequiresTwoFactor)
                {
                    await LogLoginAttemptAsync(user.Email ?? loginIdentifier, true, "تمت إحالة المستخدم إلى التحقق الثنائي", user);
                    return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
                }
                if (result.IsLockedOut)
                {
                    _logger.LogWarning("User account locked out.");
                    await LogLoginAttemptAsync(user.Email ?? loginIdentifier, false, "الحساب مقفل مؤقتاً", user);
                    return RedirectToPage("./Lockout");
                }
                else
                {
                    await LogLoginAttemptAsync(user.Email ?? loginIdentifier, false, "فشل تسجيل الدخول بسبب كلمة مرور غير صحيحة", user);
                    ModelState.AddModelError(string.Empty, "بيانات الدخول أو كلمة المرور غير صحيحة.");
                    return Page();
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }

        private async Task LogLoginAttemptAsync(string identifier, bool success, string message, IdentityUser? user = null)
        {
            await _auditTrailService.LogLoginAsync(new AuditLogEntry
            {
                TimestampUtc = DateTime.UtcNow,
                Severity = success ? "Information" : "Warning",
                EventType = success ? "LoginSuccess" : "LoginFailed",
                Message = message,
                UserId = user?.Id,
                UserEmail = user?.Email ?? identifier,
                UserName = user?.UserName,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                Path = HttpContext.Request.Path,
                HttpMethod = HttpContext.Request.Method,
                Details = $"تذكرني: {(Input.RememberMe ? "نعم" : "لا")}"
            });
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
    }
}
