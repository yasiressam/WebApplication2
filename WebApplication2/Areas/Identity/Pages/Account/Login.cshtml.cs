// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication2.Models.Audit;
using WebApplication2.Services;

namespace WebApplication2.Areas.Identity.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<LoginModel> _logger;
        private readonly IAuditTrailService _auditTrailService;

        public LoginModel(
            SignInManager<IdentityUser> signInManager,
            UserManager<IdentityUser> userManager,
            ILogger<LoginModel> logger,
            IAuditTrailService auditTrailService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
            _auditTrailService = auditTrailService;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required]
            [Display(Name = "البريد الإلكتروني")]
            public string Email { get; set; }

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

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
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var loginIdentifier = Input.Email.Trim();
            var user = await _userManager.FindByEmailAsync(loginIdentifier);

            if (user == null)
            {
                await LogLoginAttemptAsync(loginIdentifier, false, "محاولة دخول بحساب غير موجود");
                ModelState.AddModelError(string.Empty, "بيانات الدخول أو كلمة المرور غير صحيحة.");
                return Page();
            }

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

            await LogLoginAttemptAsync(user.Email ?? loginIdentifier, false, "فشل تسجيل الدخول بسبب كلمة مرور غير صحيحة", user);
            ModelState.AddModelError(string.Empty, "بيانات الدخول أو كلمة المرور غير صحيحة.");
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
    }
}
