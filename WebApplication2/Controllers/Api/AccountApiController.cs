using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;
using WebApplication2.Models.Helpers;

namespace WebApplication2.Controllers.Api
{
    [Route("api/account")]
    [ApiController]
    public class AccountApiController : ControllerBase
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AccountApiController> _logger;

        public AccountApiController(
            SignInManager<IdentityUser> signInManager,
            UserManager<IdentityUser> userManager,
            ApplicationDbContext context,
            ILogger<AccountApiController> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
            _logger = logger;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] AccountLoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { success = false, message = "البريد الإلكتروني وكلمة المرور مطلوبان" });
            }

            var result = await _signInManager.PasswordSignInAsync(
                request.Email,
                request.Password,
                request.RememberMe,
                lockoutOnFailure: true);

            if (result.Succeeded)
            {
                _logger.LogInformation("تم تسجيل الدخول عبر API: {Email}", request.Email);

                var user = await _userManager.FindByNameAsync(request.Email);
                if (user == null)
                {
                    user = await _userManager.FindByEmailAsync(request.Email);
                }

                if (user == null)
                {
                    return Unauthorized(new { success = false, message = "بيانات الدخول أو كلمة المرور غير صحيحة" });
                }

                var roles = await _userManager.GetRolesAsync(user);

                return Ok(new
                {
                    success = true,
                    message = "تم تسجيل الدخول بنجاح",
                    data = new
                    {
                        userId = user.Id,
                        email = user.Email,
                        userName = user.UserName,
                        phoneNumber = user.PhoneNumber,
                        roles
                    }
                });
            }

            if (result.IsLockedOut)
            {
                return StatusCode(423, new { success = false, message = "تم قفل الحساب مؤقتاً بسبب محاولات فاشلة متكررة" });
            }

            if (result.RequiresTwoFactor)
            {
                return BadRequest(new { success = false, message = "بيانات الدخول أو كلمة المرور غير صحيحة" });
            }

            if (result.IsNotAllowed)
            {
                return BadRequest(new { success = false, message = "بيانات الدخول أو كلمة المرور غير صحيحة" });
            }

            return Unauthorized(new { success = false, message = "بيانات الدخول أو كلمة المرور غير صحيحة" });
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            _logger.LogInformation("تم تسجيل الخروج عبر API");

            return Ok(new { success = true, message = "تم تسجيل الخروج بنجاح" });
        }

        [HttpGet("force-complete-profile")]
        [Authorize]
        public async Task<IActionResult> ForceCompleteProfile()
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { success = false, message = "المستخدم غير مسجل الدخول" });
            }

            var existingProfile = await _context.Identifies
                .FirstOrDefaultAsync(i => i.UserId == userId);

            if (existingProfile != null)
            {
                return Ok(new
                {
                    success = true,
                    isProfileComplete = true,
                    message = "ملفك الشخصي مكتمل بالفعل"
                });
            }

            return Ok(new
            {
                success = true,
                isProfileComplete = false,
                message = "يرجى إكمال بياناتك الشخصية",
                userId,
                completeProfileUrl = $"/Register/CompleteProfile?userId={Uri.EscapeDataString(userId)}"
            });
        }

        [HttpGet("my-profile")]
        [Authorize]
        public async Task<IActionResult> MyProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized(new { success = false, message = "المستخدم غير مسجل الدخول" });
            }

            var profile = await _context.Identifies
                .FirstOrDefaultAsync(i => i.UserId == user.Id);
            var address = await _context.Addresses
                .FirstOrDefaultAsync(a => a.UserId == user.Id);
            var voterCard = await _context.VoterCards
                .FirstOrDefaultAsync(v => v.UserId == user.Id);
            var roles = await _userManager.GetRolesAsync(user);

            if (profile == null)
            {
                return Ok(new
                {
                    success = true,
                    isProfileComplete = false,
                    message = "يرجى إكمال ملفك الشخصي أولاً",
                    completeProfileUrl = "/Register/BasicInfo"
                });
            }

            return Ok(new
            {
                success = true,
                isProfileComplete = IsBasicInfoComplete(profile),
                isAdditionalInfoComplete = IsAdditionalInfoComplete(voterCard),
                data = new
                {
                    userId = user.Id,
                    email = user.Email,
                    phoneNumber = user.PhoneNumber ?? profile.PhoneNumber,
                    fullName = profile.FullName,
                    governorate = profile.WorkGovernorate ?? "غير محدد",
                    district = profile.WorkDistrict,
                    role = roles.FirstOrDefault() ?? clsRoles.User,
                    roles,
                    registrationDate = profile.Date,
                    address = address == null ? null : new
                    {
                        address.Governorate,
                        address.District,
                        address.Area,
                        address.Alley,
                        address.Street,
                        address.House,
                        address.NearestPoint
                    }
                }
            });
        }

        [HttpPost("create-user-by-admin")]
        [Authorize(Roles = clsRoles.SystemManager + "," + clsRoles.SuperAdmin + "," + clsRoles.Admin + "," + clsRoles.DistrictAdmin)]
        public async Task<IActionResult> CreateUserByAdmin([FromBody] CreateUserByAdminRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new { success = false, message = "البريد الإلكتروني مطلوب" });
            }

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { success = false, message = "كلمة المرور مطلوبة" });
            }

            if (request.Password != request.ConfirmPassword)
            {
                return BadRequest(new { success = false, message = "كلمة المرور وتأكيدها غير متطابقين" });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized(new { success = false, message = "المستخدم الحالي غير موجود" });
            }

            var currentUserRoles = await _userManager.GetRolesAsync(currentUser);
            var roleToAssign = ResolveAllowedRole(request.Role, currentUserRoles);

            if (roleToAssign == null)
            {
                return Forbid();
            }

            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser != null)
            {
                return Conflict(new { success = false, message = "البريد الإلكتروني مسجل مسبقاً" });
            }

            if (roleToAssign == clsRoles.Admin && string.IsNullOrWhiteSpace(request.ManagedGovernorate))
            {
                return BadRequest(new { success = false, message = "يجب تحديد المحافظة للأدمن" });
            }

            var user = new IdentityUser
            {
                UserName = request.Email,
                Email = request.Email,
                EmailConfirmed = true,
                PhoneNumber = request.PhoneNumber
            };

            var createResult = await _userManager.CreateAsync(user, request.Password);

            if (!createResult.Succeeded)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "تعذر إنشاء المستخدم",
                    errors = createResult.Errors.Select(e => e.Description)
                });
            }

            try
            {
                await _userManager.AddToRoleAsync(user, roleToAssign);

                var currentUserProfile = await _context.Identifies
                    .FirstOrDefaultAsync(i => i.UserId == currentUser.Id);
                var adminGovernorate = currentUserProfile?.ManagedGovernorate ?? "بغداد";

                var identify = roleToAssign == clsRoles.Admin
                    ? CreateAdminIdentify(user, request.ManagedGovernorate!)
                    : CreateRegularIdentify(user, adminGovernorate, request.FullName);

                _context.Identifies.Add(identify);
                await _context.SaveChangesAsync();

                _logger.LogInformation("تم إنشاء مستخدم عبر API: {Email} بدور {Role}", request.Email, roleToAssign);

                return Ok(new
                {
                    success = true,
                    message = "تم إنشاء المستخدم بنجاح",
                    data = new
                    {
                        userId = user.Id,
                        user.Email,
                        role = roleToAssign,
                        managedGovernorate = identify.ManagedGovernorate,
                        workGovernorate = identify.WorkGovernorate
                    }
                });
            }
            catch
            {
                await _userManager.DeleteAsync(user);
                throw;
            }
        }

        [HttpGet("governorates")]
        [AllowAnonymous]
        public IActionResult GetGovernorates()
        {
            return Ok(new { success = true, data = Governorates });
        }

        private static Identify CreateAdminIdentify(IdentityUser user, string managedGovernorate)
        {
            return new Identify
            {
                UserId = user.Id,
                FullName = "مدير محافظة",
                ManagedGovernorate = managedGovernorate,
                ManagedDistrict = null,
                Date = DateTime.Now,
                Gender = "ذكر",
                PhoneNumber = "",
                IdentityCardN = "",
                identityDate = DateTime.Now,
                CreatedAt = DateTime.UtcNow,
                BasicInfoRequestedAt = IraqTime.Now(),
                AccountType = "عادي",
                IsPromoted = false,
                Email = user.Email ?? string.Empty,
                IsBasicInfoApproved = false
            };
        }

        private static Identify CreateRegularIdentify(IdentityUser user, string adminGovernorate, string? fullName)
        {
            return new Identify
            {
                UserId = user.Id,
                FullName = fullName ?? string.Empty,
                Date = DateTime.Now,
                Gender = "",
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                IdentityCardN = "",
                identityDate = DateTime.Now,
                CreatedAt = DateTime.UtcNow,
                BasicInfoRequestedAt = IraqTime.Now(),
                AccountType = "عادي",
                IsPromoted = false,
                Email = user.Email ?? string.Empty,
                IsBasicInfoApproved = false,
                WorkGovernorate = adminGovernorate,
                WorkDistrict = null
            };
        }

        private static bool IsBasicInfoComplete(Identify profile)
        {
            if (string.IsNullOrWhiteSpace(profile.FullName)) return false;
            if (string.IsNullOrWhiteSpace(profile.MotherName)) return false;
            if (profile.Date == DateTime.MinValue) return false;
            if (string.IsNullOrWhiteSpace(profile.Gender)) return false;
            if (string.IsNullOrWhiteSpace(profile.PhoneNumber)) return false;
            if (string.IsNullOrWhiteSpace(profile.IdentityCardN)) return false;
            if (profile.IdentityCardN.Length != 12) return false;
            if (string.IsNullOrWhiteSpace(profile.WorkGovernorate)) return false;

            return true;
        }

        private static bool IsAdditionalInfoComplete(VoterCard? voterCard)
        {
            return voterCard != null && !string.IsNullOrWhiteSpace(voterCard.VoterCardNumber);
        }

        private string? ResolveAllowedRole(string? requestedRole, IList<string> currentUserRoles)
        {
            var normalizedRole = string.IsNullOrWhiteSpace(requestedRole)
                ? clsRoles.User
                : requestedRole.Trim();

            if (currentUserRoles.Contains(clsRoles.SystemManager))
            {
                var systemManagerAllowedRoles = new HashSet<string>
                {
                    clsRoles.SystemManager,
                    clsRoles.SuperAdmin,
                    clsRoles.Admin,
                    clsRoles.DistrictAdmin,
                    clsRoles.User,
                    clsRoles.Member,
                    clsRoles.NewsEditor,
                    clsRoles.MapViewer,
                    clsRoles.Manager,
                    clsRoles.AssistantManager
                };

                return systemManagerAllowedRoles.Contains(normalizedRole) ? normalizedRole : null;
            }

            if (currentUserRoles.Contains(clsRoles.SuperAdmin))
            {
                var superAdminAllowedRoles = new HashSet<string>
                {
                    clsRoles.SuperAdmin,
                    clsRoles.Admin,
                    clsRoles.DistrictAdmin,
                    clsRoles.User,
                    clsRoles.Member,
                    clsRoles.NewsEditor,
                    clsRoles.MapViewer,
                    clsRoles.Manager,
                    clsRoles.AssistantManager
                };

                return superAdminAllowedRoles.Contains(normalizedRole) ? normalizedRole : null;
            }

            if (currentUserRoles.Contains(clsRoles.Admin) || currentUserRoles.Contains(clsRoles.DistrictAdmin))
            {
                return normalizedRole == clsRoles.User ? clsRoles.User : null;
            }

            return null;
        }

        private static readonly List<string> Governorates = new()
        {
            "بغداد", "الأنبار", "بابل", "البصرة", "ذي قار", "القادسية",
            "ديالى", "دهوك", "أربيل", "كربلاء", "كركوك", "ميسان",
            "المثنى", "النجف", "نينوى", "صلاح الدين", "السليمانية", "واسط"
        };
    }

    public class AccountLoginRequest
    {
        public string? Email { get; set; }
        public string? Password { get; set; }
        public bool RememberMe { get; set; }
    }

    public class CreateUserByAdminRequest
    {
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Password { get; set; }
        public string? ConfirmPassword { get; set; }
        public string? Role { get; set; } = clsRoles.User;
        public string? FullName { get; set; }
        public string? ManagedGovernorate { get; set; }
        public string? ManagedDistrict { get; set; }
    }
}
