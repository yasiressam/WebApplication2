using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using WebApplication2.Data;
using WebApplication2.Models;
using WebApplication2.Models.Helpers;
using WebApplication2.Models.Profile;
using WebApplication2.Services;

namespace WebApplication2.Controllers.Api
{
    [Route("api/register")]
    [ApiController]
    public class RegisterApiController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<RegisterApiController> _logger;
        private readonly IOtpService _otpService;

        public RegisterApiController(
            UserManager<IdentityUser> userManager,
            IEmailSender emailSender,
            ApplicationDbContext context,
            ILogger<RegisterApiController> logger,
            IOtpService otpService)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _context = context;
            _logger = logger;
            _otpService = otpService;
        }

        [HttpGet("options")]
        [AllowAnonymous]
        public async Task<IActionResult> Options()
        {
            return Ok(new
            {
                success = true,
                data = new
                {
                    governorates = GetGovernorates(),
                    genders = GetGenders(),
                    educations = GetEducations(),
                    ministries = GetMinistries(),
                    employmentStatuses = GetEmploymentStatuses(),
                    baghdadDistricts = new[] { "الكرخ", "الرصافة" },
                    studyStages = GetStudyStages(),
                    jobGrades = GetJobGrades(),
                    affiliationEntities = await _context.AffiliationEntities.Select(e => e.Name).ToListAsync(),
                    divisions = await _context.Divisions.Select(d => d.Name).Distinct().ToListAsync(),
                    sections = await _context.Sections.Select(s => s.Name).Distinct().ToListAsync(),
                    groups = await _context.Groups.Select(g => g.Name).Distinct().ToListAsync(),
                    unions = await _context.Unions.Select(u => u.Name).ToListAsync(),
                    federations = await _context.Federations.Select(f => f.Name).ToListAsync(),
                    associations = await _context.Associations.Select(a => a.Name).ToListAsync(),
                    ngos = await _context.Ngos.Select(n => n.Name).ToListAsync()
                }
            });
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterViewModel model)
        {
            if (model.Password != model.ConfirmPassword)
            {
                return BadRequest(new { success = false, message = "كلمة المرور وتأكيدها غير متطابقين" });
            }

            return await RegisterWithEmailAsync(model);
        }

        [HttpGet("basic-info")]
        [Authorize]
        public async Task<IActionResult> BasicInfo()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized(new { success = false, message = "يجب تسجيل الدخول أولاً" });
            }

            var profile = await EnsureProfileAsync(user);
            var address = await _context.Addresses.FirstOrDefaultAsync(a => a.UserId == user.Id);
            var workLocation = await _context.WorkLocations.FirstOrDefaultAsync(w => w.IdentifyId == profile.Id);

            return Ok(new
            {
                success = true,
                status = GetProfileStatus(profile),
                data = new
                {
                    userId = user.Id,
                    email = user.Email,
                    profile,
                    address,
                    workLocation,
                    options = await BuildOptionsAsync()
                }
            });
        }

        [HttpPost("basic-info")]
        [Authorize]
        public async Task<IActionResult> SaveBasicInfo([FromBody] BasicInfoViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized(new { success = false, message = "يجب تسجيل الدخول أولاً" });
            }

            if (string.IsNullOrWhiteSpace(model.UserId))
            {
                model.UserId = user.Id;
            }

            if (model.UserId != user.Id)
            {
                return Forbid();
            }

            var validationErrors = ValidateBasicInfo(model);
            if (validationErrors.Count > 0)
            {
                return BadRequest(new { success = false, message = "البيانات الأساسية غير مكتملة", errors = validationErrors });
            }

            var profile = await EnsureProfileAsync(user);
            ApplyBasicInfo(profile, model, user);

            await _context.SaveChangesAsync();
            await UpdateOrCreateWorkLocationAsync(profile, model.WorkLocation);
            await UpdateOrCreateAddressAsync(model.UserId, model.Address);

            profile.IsBasicInfoApproved = false;
            profile.BasicInfoRequestedAt = IraqTime.Now();
            profile.BasicInfoApprovedBy = null;
            profile.BasicInfoApprovalDate = null;
            profile.BasicInfoRejectionReason = null;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "تم حفظ البيانات الأساسية. بانتظار مراجعة الإدارة",
                nextStep = "basic-info-pending"
            });
        }

        [HttpPost("basic-info/cover-image")]
        [Authorize]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadBasicInfoCoverImage([FromForm] RegisterCoverImageRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized(new { success = false, message = "يجب تسجيل الدخول أولاً" });
            }

            if (request.CoverImageFile == null || request.CoverImageFile.Length == 0)
            {
                return BadRequest(new { success = false, message = "الصورة الشخصية مطلوبة" });
            }

            var profile = await EnsureProfileAsync(user);
            profile.CoverImage = await SaveCoverImageAsync(request.CoverImageFile);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "تم رفع الصورة الشخصية بنجاح",
                coverImage = profile.CoverImage
            });
        }

        [HttpGet("basic-info/pending")]
        [Authorize]
        public async Task<IActionResult> BasicInfoPending()
        {
            var userId = _userManager.GetUserId(User);
            var profile = await _context.Identifies.FirstOrDefaultAsync(i => i.UserId == userId);
            if (profile == null)
            {
                return NotFound(new { success = false, message = "لم يتم العثور على الملف الشخصي", nextStep = "basic-info" });
            }

            return Ok(new
            {
                success = true,
                isApproved = profile.IsBasicInfoApproved,
                message = profile.IsBasicInfoApproved
                    ? "تمت الموافقة على معلوماتك الأساسية. أكمل البيانات الإضافية"
                    : "طلبك قيد مراجعة الإدارة",
                nextStep = profile.IsBasicInfoApproved ? "additional-info" : "wait"
            });
        }

        [HttpGet("additional-info")]
        [Authorize]
        public async Task<IActionResult> AdditionalInfo()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized(new { success = false, message = "يجب تسجيل الدخول أولاً" });
            }

            var profile = await _context.Identifies.FirstOrDefaultAsync(i => i.UserId == user.Id);
            if (profile == null)
            {
                return NotFound(new { success = false, message = "أكمل البيانات الأساسية أولاً", nextStep = "basic-info" });
            }

            return Ok(new
            {
                success = true,
                status = GetProfileStatus(profile),
                data = new
                {
                    userId = user.Id,
                    email = user.Email,
                    voterCard = await _context.VoterCards.FirstOrDefaultAsync(v => v.UserId == user.Id),
                    affiliation = await _context.AffiliationInfos
                        .Include(a => a.AffiliationEntity)
                        .Include(a => a.Division)
                        .Include(a => a.Section)
                        .Include(a => a.Group)
                        .FirstOrDefaultAsync(a => a.UserId == user.Id),
                    union = await _context.UnionMemberships.FirstOrDefaultAsync(u => u.UserId == user.Id),
                    federation = await _context.FederationMemberships
                        .Include(f => f.Federation)
                        .Include(f => f.FederationDivision)
                        .Include(f => f.FederationSection)
                        .Include(f => f.FederationGroup)
                        .FirstOrDefaultAsync(f => f.UserId == user.Id),
                    association = await _context.AssociationMemberships.FirstOrDefaultAsync(a => a.UserId == user.Id),
                    ngo = await _context.NgoMemberships.FirstOrDefaultAsync(n => n.UserId == user.Id),
                    options = await BuildOptionsAsync()
                }
            });
        }

        [HttpPost("additional-info")]
        [Authorize]
        public async Task<IActionResult> SaveAdditionalInfo([FromBody] AdditionalInfoViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized(new { success = false, message = "يجب تسجيل الدخول أولاً" });
            }

            if (string.IsNullOrWhiteSpace(model.UserId))
            {
                model.UserId = user.Id;
            }

            if (model.UserId != user.Id)
            {
                return Forbid();
            }

            var profile = await _context.Identifies.FirstOrDefaultAsync(i => i.UserId == user.Id);
            if (profile == null)
            {
                return NotFound(new { success = false, message = "أكمل البيانات الأساسية أولاً", nextStep = "basic-info" });
            }

            if (!profile.IsBasicInfoApproved)
            {
                return BadRequest(new { success = false, message = "معلوماتك الأساسية لم توافق عليها بعد", nextStep = "basic-info-pending" });
            }

            var validationErrors = await ValidateAdditionalInfoAsync(model);
            if (validationErrors.Count > 0)
            {
                return BadRequest(new { success = false, message = "البيانات الإضافية غير مكتملة", errors = validationErrors });
            }

            await UpdateOrCreateVoterCardAsync(user.Id, model.Documents);
            await UpdateOrCreateAffiliationInfoAsync(user.Id, model.Affiliation);
            await UpdateOrCreateUnionAsync(user.Id, model.Memberships);
            await UpdateOrCreateFederationAsync(user.Id, model.Memberships);
            await UpdateOrCreateAssociationAsync(user.Id, model.Memberships);
            await UpdateOrCreateNgoAsync(user.Id, model.Memberships);

            profile.RequestedPromotion = true;
            profile.RequestedPromotionDate = DateTime.Now;
            profile.AccountType = "مكتمل";
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "تم حفظ المعلومات الإضافية. بانتظار مراجعة الإدارة",
                nextStep = "promotion-pending"
            });
        }

        [HttpGet("profile")]
        [Authorize]
        public async Task<IActionResult> ProfileDetails()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized(new { success = false, message = "يجب تسجيل الدخول أولاً" });
            }

            var profile = await _context.Identifies.FirstOrDefaultAsync(i => i.UserId == user.Id);
            if (profile == null)
            {
                return NotFound(new { success = false, message = "أكمل بياناتك الأساسية أولاً", nextStep = "basic-info" });
            }

            var roles = await _userManager.GetRolesAsync(user);
            return Ok(new
            {
                success = true,
                data = new
                {
                    userId = user.Id,
                    user.Email,
                    roles,
                    profile,
                    address = await _context.Addresses.FirstOrDefaultAsync(a => a.UserId == user.Id),
                    workLocation = await _context.WorkLocations.FirstOrDefaultAsync(w => w.IdentifyId == profile.Id),
                    voterCard = await _context.VoterCards.FirstOrDefaultAsync(v => v.UserId == user.Id),
                    affiliation = await _context.AffiliationInfos
                        .Include(a => a.AffiliationEntity)
                        .Include(a => a.Division)
                        .Include(a => a.Section)
                        .Include(a => a.Group)
                        .FirstOrDefaultAsync(a => a.UserId == user.Id)
                }
            });
        }

        [HttpGet("promotion-pending")]
        [Authorize]
        public async Task<IActionResult> PromotionPending()
        {
            var userId = _userManager.GetUserId(User);
            var profile = await _context.Identifies.FirstOrDefaultAsync(i => i.UserId == userId);
            if (profile == null)
            {
                return NotFound(new { success = false, message = "أكمل البيانات الأساسية أولاً", nextStep = "basic-info" });
            }

            return Ok(new
            {
                success = true,
                requestedPromotion = profile.RequestedPromotion,
                isPromoted = profile.IsPromoted,
                requestedPromotionDate = profile.RequestedPromotionDate,
                rejectionReason = profile.RejectionReason,
                message = profile.IsPromoted ? "تمت الموافقة على عضويتك" : "طلبك قيد مراجعة الإدارة"
            });
        }

        [HttpGet("complete-profile")]
        [Authorize]
        public Task<IActionResult> CompleteProfile()
        {
            return ProfileDetails();
        }

        [HttpPost("complete-profile")]
        [Authorize]
        public async Task<IActionResult> SaveCompleteProfile([FromBody] CompleteProfileViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized(new { success = false, message = "يجب تسجيل الدخول أولاً" });
            }

            if (string.IsNullOrWhiteSpace(model.UserId))
            {
                model.UserId = user.Id;
            }

            if (model.UserId != user.Id)
            {
                return Forbid();
            }

            var basic = new BasicInfoViewModel
            {
                UserId = model.UserId,
                Email = model.Email,
                PersonalInfo = model.PersonalInfo,
                Address = model.Address,
                WorkLocation = model.WorkLocation,
                Employment = model.Employment,
                Documents = model.Documents,
                IdentityCardN = model.IdentityCardN,
                IdentityDate = model.IdentityDate == default ? DateTime.Now : model.IdentityDate
            };

            var basicResult = await SaveBasicInfoInternalAsync(user, basic);
            if (basicResult != null)
            {
                return basicResult;
            }

            if (model.Affiliation != null && model.Documents != null)
            {
                var additional = new AdditionalInfoViewModel
                {
                    UserId = model.UserId,
                    Email = model.Email,
                    Documents = model.Documents,
                    Affiliation = model.Affiliation,
                    Memberships = model.Memberships
                };

                var profile = await _context.Identifies.FirstOrDefaultAsync(i => i.UserId == user.Id);
                if (profile != null && profile.IsBasicInfoApproved)
                {
                    await UpdateOrCreateVoterCardAsync(user.Id, additional.Documents);
                    await UpdateOrCreateAffiliationInfoAsync(user.Id, additional.Affiliation);
                    await UpdateOrCreateUnionAsync(user.Id, additional.Memberships);
                    await UpdateOrCreateFederationAsync(user.Id, additional.Memberships);
                    await UpdateOrCreateAssociationAsync(user.Id, additional.Memberships);
                    await UpdateOrCreateNgoAsync(user.Id, additional.Memberships);
                }
            }

            return Ok(new { success = true, message = "تم حفظ الملف الشخصي بنجاح" });
        }

        [HttpGet("edit-profile")]
        [Authorize]
        public Task<IActionResult> EditProfile()
        {
            return ProfileDetails();
        }

        [HttpPut("edit-profile")]
        [Authorize]
        public Task<IActionResult> SaveEditProfile([FromBody] CompleteProfileViewModel model)
        {
            return SaveCompleteProfile(model);
        }

        [HttpGet("check-email")]
        [AllowAnonymous]
        public IActionResult CheckEmail([FromQuery] string email)
        {
            return Ok(new
            {
                success = true,
                email,
                message = "تم إنشاء حسابك. تحقق من بريدك الإلكتروني قبل تسجيل الدخول"
            });
        }

        private async Task<IActionResult> RegisterWithEmailAsync(RegisterViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Email))
            {
                return BadRequest(new { success = false, message = "البريد الإلكتروني مطلوب" });
            }

            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                return Conflict(new { success = false, message = "هذا البريد الإلكتروني مسجل مسبقاً" });
            }

            var user = new IdentityUser
            {
                UserName = model.Email,
                Email = model.Email,
                EmailConfirmed = false
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                return BadRequest(new { success = false, message = "تعذر إنشاء الحساب", errors = result.Errors.Select(e => e.Description) });
            }

            await _userManager.AddToRoleAsync(user, clsRoles.User);
            await SendConfirmationEmailAsync(user);

            _context.Identifies.Add(new Identify
            {
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow,
                BasicInfoRequestedAt = IraqTime.Now(),
                AccountType = "عادي",
                IsPromoted = false,
                FullName = "",
                MotherName = "",
                PhoneNumber = "",
                WhatsAppNumber = "",
                IsWhatsAppVerified = false,
                IdentityCardN = "",
                Date = DateTime.Now,
                IsBasicInfoApproved = false,
                Email = user.Email
            });
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "تم إنشاء حسابك بنجاح. تم إرسال رابط تأكيد إلى بريدك الإلكتروني",
                data = new { userId = user.Id, email = user.Email },
                nextStep = "check-email"
            });
        }

        private async Task<IActionResult> RegisterWithWhatsAppAsync(RegisterViewModel model)
        {
            var normalizedPhone = NormalizeIraqPhoneNumber(model.PhoneNumber);
            if (string.IsNullOrWhiteSpace(normalizedPhone) || normalizedPhone.Length < 10)
            {
                return BadRequest(new { success = false, message = "رقم الواتساب غير صحيح" });
            }

            var existingPhoneNumbers = await _context.Identifies
                .Where(i => !string.IsNullOrWhiteSpace(i.WhatsAppNumber) || !string.IsNullOrWhiteSpace(i.PhoneNumber))
                .Select(i => new { i.WhatsAppNumber, i.PhoneNumber })
                .ToListAsync();

            var phoneAlreadyExists = existingPhoneNumbers.Any(i =>
                NormalizeIraqPhoneNumber(i.WhatsAppNumber ?? string.Empty) == normalizedPhone ||
                NormalizeIraqPhoneNumber(i.PhoneNumber ?? string.Empty) == normalizedPhone);

            if (phoneAlreadyExists || await _userManager.FindByNameAsync(normalizedPhone) != null)
            {
                return Conflict(new { success = false, message = "رقم الواتساب مسجل مسبقاً" });
            }

            var syntheticEmail = $"{normalizedPhone}@whatsapp.local";
            var localPhone = ToLocalIraqPhoneNumber(normalizedPhone);
            var user = new IdentityUser
            {
                UserName = normalizedPhone,
                Email = syntheticEmail,
                PhoneNumber = localPhone,
                EmailConfirmed = false,
                PhoneNumberConfirmed = false
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                return BadRequest(new { success = false, message = "تعذر إنشاء الحساب", errors = result.Errors.Select(e => e.Description) });
            }

            await _userManager.AddToRoleAsync(user, clsRoles.User);

            _context.Identifies.Add(new Identify
            {
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow,
                BasicInfoRequestedAt = IraqTime.Now(),
                AccountType = "عادي",
                IsPromoted = false,
                FullName = "",
                MotherName = "",
                PhoneNumber = localPhone,
                WhatsAppNumber = normalizedPhone,
                IsWhatsAppVerified = false,
                IdentityCardN = "",
                Date = DateTime.Now,
                IsBasicInfoApproved = false,
                Email = ""
            });
            await _context.SaveChangesAsync();

            var sent = await SendWhatsAppVerificationCodeAsync(normalizedPhone);
            return Ok(new
            {
                success = true,
                message = sent
                    ? "تم إنشاء الحساب وإرسال كود واتساب"
                    : "تم إنشاء الحساب، لكن تعذر إرسال كود واتساب. يمكنك طلب إعادة الإرسال",
                data = new { userId = user.Id, phoneNumber = normalizedPhone },
                nextStep = "whatsapp-verify"
            });
        }

        private async Task<IActionResult?> SaveBasicInfoInternalAsync(IdentityUser user, BasicInfoViewModel model)
        {
            var validationErrors = ValidateBasicInfo(model);
            if (validationErrors.Count > 0)
            {
                return BadRequest(new { success = false, message = "البيانات الأساسية غير مكتملة", errors = validationErrors });
            }

            var profile = await EnsureProfileAsync(user);
            ApplyBasicInfo(profile, model, user);
            await _context.SaveChangesAsync();
            await UpdateOrCreateWorkLocationAsync(profile, model.WorkLocation);
            await UpdateOrCreateAddressAsync(model.UserId, model.Address);
            return null;
        }

        private async Task<Identify> EnsureProfileAsync(IdentityUser user)
        {
            var profile = await _context.Identifies.FirstOrDefaultAsync(i => i.UserId == user.Id);
            if (profile != null)
            {
                return profile;
            }

            profile = new Identify
            {
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow,
                BasicInfoRequestedAt = IraqTime.Now(),
                AccountType = "عادي",
                IsPromoted = false,
                Email = user.Email,
                FullName = "",
                MotherName = "",
                PhoneNumber = "",
                IdentityCardN = "",
                Date = DateTime.Now,
                IsBasicInfoApproved = false
            };
            _context.Identifies.Add(profile);
            await _context.SaveChangesAsync();
            return profile;
        }

        private static void ApplyBasicInfo(Identify profile, BasicInfoViewModel model, IdentityUser user)
        {
            profile.FullName = model.PersonalInfo.FullName ?? "";
            profile.LastName = model.PersonalInfo.LastName ?? "";
            profile.MotherName = model.PersonalInfo.MotherName ?? "";
            profile.Date = model.PersonalInfo.DateOfBirth;
            profile.Gender = model.PersonalInfo.Gender ?? "";
            profile.Education = model.PersonalInfo.Education ?? "";
            profile.Specialization = model.PersonalInfo.Specialization ?? "";
            profile.PhoneNumber = model.PersonalInfo.PhoneNumber ?? "";
            profile.MaritalStatus = model.PersonalInfo.MaritalStatus;
            profile.UniversityType = model.PersonalInfo.UniversityType;
            profile.InstitutionType = model.PersonalInfo.InstitutionType;
            profile.InstitutionName = model.PersonalInfo.InstitutionName;
            profile.FacultyDepartment = model.PersonalInfo.FacultyDepartment;
            profile.StudyType = model.PersonalInfo.StudyType;
            profile.StudyStage = model.PersonalInfo.StudyStage;
            profile.Email = user.Email;
            profile.IdentityCardN = model.IdentityCardN;
            profile.identityDate = model.IdentityDate;
            profile.WorkGovernorate = model.WorkLocation.Governorate;
            profile.WorkDistrict = null;
            profile.EmploymentStatus = model.Employment.EmploymentStatus;
            profile.Work = model.Employment.Work;
            profile.Ministry = model.Employment.Ministry;
            profile.Department = model.Employment.Department;
            profile.Position = model.Employment.Position;
            profile.JobTitle = model.Employment.JobTitle;
            profile.JobGrade = model.Employment.JobGrade;
        }

        private static List<string> ValidateBasicInfo(BasicInfoViewModel model)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(model.PersonalInfo.FullName)) errors.Add("الاسم الرباعي مطلوب");
            if (string.IsNullOrWhiteSpace(model.PersonalInfo.LastName)) errors.Add("اللقب مطلوب");
            if (string.IsNullOrWhiteSpace(model.PersonalInfo.MotherName)) errors.Add("اسم الأم مطلوب");
            if (string.IsNullOrWhiteSpace(model.PersonalInfo.Gender)) errors.Add("الجنس مطلوب");
            if (string.IsNullOrWhiteSpace(model.PersonalInfo.MaritalStatus)) errors.Add("الحالة الاجتماعية مطلوبة");
            if (string.IsNullOrWhiteSpace(model.PersonalInfo.Education)) errors.Add("التحصيل الدراسي مطلوب");
            if (string.IsNullOrWhiteSpace(model.PersonalInfo.Specialization)) errors.Add("الاختصاص مطلوب");
            if (string.IsNullOrWhiteSpace(model.PersonalInfo.PhoneNumber)) errors.Add("رقم الهاتف مطلوب");
            if (string.IsNullOrWhiteSpace(model.IdentityCardN) || model.IdentityCardN.Length != 12) errors.Add("رقم البطاقة الموحدة يجب أن يكون 12 رقم");
            if (string.IsNullOrWhiteSpace(model.WorkLocation.Governorate)) errors.Add("محافظة العمل التنظيمي مطلوبة");
            if (string.IsNullOrWhiteSpace(model.Address.Governorate)) errors.Add("محافظة السكن مطلوبة");
            if (model.Address.Governorate == "بغداد" && string.IsNullOrWhiteSpace(model.Address.District)) errors.Add("القضاء مطلوب عند اختيار محافظة السكن بغداد");
            if (string.IsNullOrWhiteSpace(model.Address.Area)) errors.Add("المنطقة مطلوبة");
            if (string.IsNullOrWhiteSpace(model.Address.Alley)) errors.Add("المحلة مطلوبة");
            if (string.IsNullOrWhiteSpace(model.Address.Street)) errors.Add("الزقاق مطلوب");
            if (string.IsNullOrWhiteSpace(model.Address.House)) errors.Add("رقم الدار مطلوب");
            if (string.IsNullOrWhiteSpace(model.Employment.EmploymentStatus)) errors.Add("الحالة الوظيفية مطلوبة");
            if (string.IsNullOrWhiteSpace(model.Employment.Work)) errors.Add("المهنة مطلوبة");

            if (model.PersonalInfo.Education == "طالب جامعي")
            {
                if (string.IsNullOrWhiteSpace(model.PersonalInfo.UniversityType)) errors.Add("نوع الجامعة مطلوب للطالب الجامعي");
                if (string.IsNullOrWhiteSpace(model.PersonalInfo.InstitutionType)) errors.Add("نوع المؤسسة مطلوب للطالب الجامعي");
                if (string.IsNullOrWhiteSpace(model.PersonalInfo.InstitutionName)) errors.Add("اسم الجامعة أو المعهد مطلوب للطالب الجامعي");
                if (string.IsNullOrWhiteSpace(model.PersonalInfo.FacultyDepartment)) errors.Add("الكلية أو القسم مطلوب للطالب الجامعي");
                if (string.IsNullOrWhiteSpace(model.PersonalInfo.StudyType)) errors.Add("نوع الدراسة مطلوب للطالب الجامعي");
                if (string.IsNullOrWhiteSpace(model.PersonalInfo.StudyStage)) errors.Add("المرحلة الدراسية مطلوبة للطالب الجامعي");
            }
            else
            {
                model.PersonalInfo.UniversityType = null;
                model.PersonalInfo.InstitutionType = null;
                model.PersonalInfo.InstitutionName = null;
                model.PersonalInfo.FacultyDepartment = null;
                model.PersonalInfo.StudyType = null;
                model.PersonalInfo.StudyStage = null;
            }

            if (model.Employment.EmploymentStatus == "موظف")
            {
                if (string.IsNullOrWhiteSpace(model.Employment.Ministry)) errors.Add("الوزارة مطلوبة للموظفين");
                if (string.IsNullOrWhiteSpace(model.Employment.Department)) errors.Add("الدائرة مطلوبة للموظفين");
                if (string.IsNullOrWhiteSpace(model.Employment.Position)) errors.Add("المنصب مطلوب للموظفين");
                if (string.IsNullOrWhiteSpace(model.Employment.JobTitle)) errors.Add("العنوان الوظيفي مطلوب للموظفين");
                if (string.IsNullOrWhiteSpace(model.Employment.JobGrade)) errors.Add("الدرجة الوظيفية مطلوبة للموظفين");
            }
            else
            {
                model.Employment.Ministry = null;
                model.Employment.Department = null;
                model.Employment.Position = null;
                model.Employment.JobTitle = null;
                model.Employment.JobGrade = null;
            }

            return errors;
        }

        private async Task<List<string>> ValidateAdditionalInfoAsync(AdditionalInfoViewModel model)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(model.Documents.VoterCardNumber)) errors.Add("رقم بطاقة الناخب مطلوب");
            else if (model.Documents.VoterCardNumber.Length != 8) errors.Add("رقم بطاقة الناخب يجب أن يكون 8 أرقام");
            if (string.IsNullOrWhiteSpace(model.Documents.PollingCenterNumber)) errors.Add("رقم مركز الاقتراع مطلوب");
            if (string.IsNullOrWhiteSpace(model.Affiliation.AffiliationEntity)) errors.Add("جهة الانتساب مطلوبة");
            if (string.IsNullOrWhiteSpace(model.Affiliation.Division)) errors.Add("القسم مطلوب");
            if (string.IsNullOrWhiteSpace(model.Affiliation.MozakeName)) errors.Add("اسم المزكي مطلوب");
            if (string.IsNullOrWhiteSpace(model.Affiliation.BadgeNumber)) errors.Add("رقم الباج مطلوب");
            if (model.Affiliation.AffiliationDate == null || model.Affiliation.AffiliationDate == default) errors.Add("تاريخ الانتماء مطلوب");

            if (!string.IsNullOrWhiteSpace(model.Affiliation.AffiliationEntity) &&
                !string.IsNullOrWhiteSpace(model.Affiliation.Division))
            {
                var entity = await _context.AffiliationEntities.FirstOrDefaultAsync(e => e.Name == model.Affiliation.AffiliationEntity);
                if (entity != null)
                {
                    var division = await _context.Divisions.FirstOrDefaultAsync(d => d.AffiliationEntityId == entity.Id && d.Name == model.Affiliation.Division);
                    if (division != null && await _context.Sections.AnyAsync(s => s.DivisionId == division.Id))
                    {
                        if (string.IsNullOrWhiteSpace(model.Affiliation.Section))
                        {
                            errors.Add("الشعبة مطلوبة لهذا القسم");
                        }
                        else if (model.Affiliation.Section.Trim() == "التجمعات التخصصية" &&
                                 string.IsNullOrWhiteSpace(model.Affiliation.Group))
                        {
                            errors.Add("الوحدة مطلوبة عند اختيار شعبة التجمعات التخصصية");
                        }
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(model.Memberships.UnionName) &&
                (model.Memberships.UnionAffiliationDate == null || model.Memberships.UnionAffiliationDate == default))
            {
                errors.Add("تاريخ النفاذ للنقابة مطلوب");
            }

            if (!string.IsNullOrWhiteSpace(model.Memberships.FederationName) &&
                (model.Memberships.FederationAffiliationDate == null || model.Memberships.FederationAffiliationDate == default))
            {
                errors.Add("تاريخ النفاذ للاتحاد مطلوب");
            }

            if (!string.IsNullOrWhiteSpace(model.Memberships.AssociationName) &&
                (model.Memberships.AssociationAffiliationDate == null || model.Memberships.AssociationAffiliationDate == default))
            {
                errors.Add("تاريخ النفاذ للجمعية مطلوب");
            }

            if (!string.IsNullOrWhiteSpace(model.Memberships.NgoName) &&
                (model.Memberships.NgoAffiliationDate == null || model.Memberships.NgoAffiliationDate == default))
            {
                errors.Add("تاريخ النفاذ للمنظمة مطلوب");
            }

            return errors;
        }

        private async Task UpdateOrCreateAddressAsync(string userId, AddressViewModel model)
        {
            if (string.IsNullOrEmpty(model.Governorate)) return;

            var address = await _context.Addresses.FirstOrDefaultAsync(a => a.UserId == userId);
            if (address == null)
            {
                address = new Address { UserId = userId };
                _context.Addresses.Add(address);
            }

            address.Governorate = model.Governorate ?? "";
            address.District = model.District ?? "";
            address.Area = model.Area ?? "";
            address.Alley = model.Alley ?? "";
            address.Street = model.Street ?? "";
            address.House = model.House ?? "";
            address.NearestPoint = model.NearestPoint ?? "";
            await _context.SaveChangesAsync();
        }

        private async Task UpdateOrCreateWorkLocationAsync(Identify profile, WorkLocationViewModel model)
        {
            var workLocation = await _context.WorkLocations.FirstOrDefaultAsync(w => w.IdentifyId == profile.Id);
            if (workLocation == null)
            {
                workLocation = new WorkLocation { IdentifyId = profile.Id };
                _context.WorkLocations.Add(workLocation);
            }

            workLocation.Governorate = model.Governorate;
            workLocation.District = model.Governorate == "بغداد" ? model.District : null;
            profile.WorkGovernorate = workLocation.Governorate;
            profile.WorkDistrict = workLocation.District;
            await _context.SaveChangesAsync();
        }

        private async Task UpdateOrCreateVoterCardAsync(string userId, DocumentsViewModel model)
        {
            if (string.IsNullOrEmpty(model.VoterCardNumber) && string.IsNullOrEmpty(model.PollingCenterNumber)) return;

            var voterCard = await _context.VoterCards.FirstOrDefaultAsync(v => v.UserId == userId);
            if (voterCard == null)
            {
                voterCard = new VoterCard { UserId = userId };
                _context.VoterCards.Add(voterCard);
            }

            voterCard.VoterCardNumber = model.VoterCardNumber;
            voterCard.PollingCenterNumber = model.PollingCenterNumber;
            await _context.SaveChangesAsync();
        }

        private async Task UpdateOrCreateAffiliationInfoAsync(string userId, AffiliationViewModel model)
        {
            var affiliationEntityId = await FindAffiliationEntityIdAsync(model.AffiliationEntity);
            var divisionId = affiliationEntityId.HasValue ? await FindDivisionIdAsync(model.Division, affiliationEntityId.Value) : null;
            var sectionId = divisionId.HasValue ? await FindSectionIdAsync(model.Section, divisionId.Value) : null;
            var groupId = sectionId.HasValue ? await FindGroupIdAsync(model.Group, sectionId.Value) : null;

            var affiliation = await _context.AffiliationInfos.FirstOrDefaultAsync(a => a.UserId == userId);
            if (affiliation == null)
            {
                affiliation = new AffiliationInfo { UserId = userId };
                _context.AffiliationInfos.Add(affiliation);
            }

            affiliation.AffiliationEntityId = affiliationEntityId;
            affiliation.DivisionId = divisionId;
            affiliation.SectionId = sectionId;
            affiliation.GroupId = groupId;
            affiliation.MozakeName = model.MozakeName;
            affiliation.MozakePhoneNumber = model.MozakePhoneNumber;
            affiliation.BadgeNumber = model.BadgeNumber;
            affiliation.AffiliationDate = model.AffiliationDate;
            await _context.SaveChangesAsync();
        }

        private async Task UpdateOrCreateUnionAsync(string userId, MembershipViewModel model)
        {
            if (string.IsNullOrEmpty(model.UnionName)) return;
            var membership = await _context.UnionMemberships.FirstOrDefaultAsync(u => u.UserId == userId);
            if (membership == null)
            {
                membership = new UnionMembership { UserId = userId };
                _context.UnionMemberships.Add(membership);
            }

            membership.UnionName = model.UnionName;
            membership.Position = model.UnionPosition;
            membership.IdNumber = model.UnionIdNumber;
            membership.AffiliationDate = model.UnionAffiliationDate;
            await _context.SaveChangesAsync();
        }

        private async Task UpdateOrCreateFederationAsync(string userId, MembershipViewModel model)
        {
            if (string.IsNullOrEmpty(model.FederationName)) return;

            var membership = await _context.FederationMemberships.FirstOrDefaultAsync(f => f.UserId == userId);
            if (membership == null)
            {
                membership = new FederationMembership { UserId = userId };
                _context.FederationMemberships.Add(membership);
            }

            var federationName = model.FederationName;
            var divisionName = model.FederationDivisionName;
            var sectionName = model.FederationSectionName;
            var groupName = model.FederationGroupName;

            var parts = model.FederationName.Split(new[] { " - " }, StringSplitOptions.None);
            if (parts.Length >= 1) federationName = parts[0];
            if (parts.Length >= 2) divisionName = parts[1];
            if (parts.Length >= 3) sectionName = parts[2];
            if (parts.Length >= 4) groupName = parts[3];

            var federation = await _context.Federations.FirstOrDefaultAsync(f => f.Name == federationName);
            membership.FederationId = federation?.Id;
            membership.FederationDivisionId = federation != null && !string.IsNullOrWhiteSpace(divisionName)
                ? (await _context.FederationDivisions.FirstOrDefaultAsync(d => d.FederationId == federation.Id && d.Name == divisionName))?.Id
                : null;
            membership.FederationSectionId = membership.FederationDivisionId.HasValue && !string.IsNullOrWhiteSpace(sectionName)
                ? (await _context.FederationSections.FirstOrDefaultAsync(s => s.FederationDivisionId == membership.FederationDivisionId.Value && s.Name == sectionName))?.Id
                : null;
            membership.FederationGroupId = membership.FederationSectionId.HasValue && !string.IsNullOrWhiteSpace(groupName)
                ? (await _context.FederationGroups.FirstOrDefaultAsync(g => g.FederationSectionId == membership.FederationSectionId.Value && g.Name == groupName))?.Id
                : null;
            membership.Position = model.FederationPosition;
            membership.IdNumber = model.FederationIdNumber;
            membership.AffiliationDate = model.FederationAffiliationDate;
            await _context.SaveChangesAsync();
        }

        private async Task UpdateOrCreateAssociationAsync(string userId, MembershipViewModel model)
        {
            if (string.IsNullOrEmpty(model.AssociationName)) return;
            var membership = await _context.AssociationMemberships.FirstOrDefaultAsync(a => a.UserId == userId);
            if (membership == null)
            {
                membership = new AssociationMembership { UserId = userId };
                _context.AssociationMemberships.Add(membership);
            }

            membership.AssociationName = model.AssociationName;
            membership.Position = model.AssociationPosition;
            membership.IdNumber = model.AssociationIdNumber;
            membership.AffiliationDate = model.AssociationAffiliationDate;
            await _context.SaveChangesAsync();
        }

        private async Task UpdateOrCreateNgoAsync(string userId, MembershipViewModel model)
        {
            if (string.IsNullOrEmpty(model.NgoName)) return;
            var membership = await _context.NgoMemberships.FirstOrDefaultAsync(n => n.UserId == userId);
            if (membership == null)
            {
                membership = new NgoMembership { UserId = userId };
                _context.NgoMemberships.Add(membership);
            }

            membership.NgoName = model.NgoName;
            membership.Position = model.NgoPosition;
            membership.IdNumber = model.NgoIdNumber;
            membership.AffiliationDate = model.NgoAffiliationDate;
            await _context.SaveChangesAsync();
        }

        private async Task<int?> FindAffiliationEntityIdAsync(string? name)
        {
            return string.IsNullOrWhiteSpace(name)
                ? null
                : (await _context.AffiliationEntities.FirstOrDefaultAsync(e => e.Name == name))?.Id;
        }

        private async Task<int?> FindDivisionIdAsync(string? name, int entityId)
        {
            return string.IsNullOrWhiteSpace(name)
                ? null
                : (await _context.Divisions.FirstOrDefaultAsync(d => d.AffiliationEntityId == entityId && d.Name == name))?.Id;
        }

        private async Task<int?> FindSectionIdAsync(string? name, int divisionId)
        {
            return string.IsNullOrWhiteSpace(name)
                ? null
                : (await _context.Sections.FirstOrDefaultAsync(s => s.DivisionId == divisionId && s.Name == name))?.Id;
        }

        private async Task<int?> FindGroupIdAsync(string? name, int sectionId)
        {
            return string.IsNullOrWhiteSpace(name)
                ? null
                : (await _context.Groups.FirstOrDefaultAsync(g => g.SectionId == sectionId && g.Name == name))?.Id;
        }

        private async Task<bool> SendWhatsAppVerificationCodeAsync(string phoneNumber)
        {
            var result = await _otpService.GenerateAndSendOtp(phoneNumber);
            return result.Success;
        }

        private async Task SendConfirmationEmailAsync(IdentityUser user)
        {
            if (string.IsNullOrWhiteSpace(user.Email)) return;

            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var confirmationUrl = Url.Action(
                "ConfirmEmail",
                "Account",
                new { userId = user.Id, token = encodedToken },
                Request.Scheme);

            await _emailSender.SendEmailAsync(
                user.Email,
                "تأكيد البريد الإلكتروني",
                $"يرجى تأكيد بريدك الإلكتروني عبر الرابط التالي: {confirmationUrl}");
        }

        private async Task<object> BuildOptionsAsync()
        {
            return new
            {
                governorates = GetGovernorates(),
                genders = GetGenders(),
                educations = GetEducations(),
                ministries = GetMinistries(),
                employmentStatuses = GetEmploymentStatuses(),
                baghdadDistricts = new[] { "الكرخ", "الرصافة" },
                studyStages = GetStudyStages(),
                jobGrades = GetJobGrades(),
                affiliationEntities = await _context.AffiliationEntities.Select(e => e.Name).ToListAsync(),
                divisions = await _context.Divisions.Select(d => d.Name).Distinct().ToListAsync(),
                sections = await _context.Sections.Select(s => s.Name).Distinct().ToListAsync(),
                groups = await _context.Groups.Select(g => g.Name).Distinct().ToListAsync(),
                unions = await _context.Unions.Select(u => u.Name).ToListAsync(),
                federations = await _context.Federations.Select(f => f.Name).ToListAsync(),
                associations = await _context.Associations.Select(a => a.Name).ToListAsync(),
                ngos = await _context.Ngos.Select(n => n.Name).ToListAsync()
            };
        }

        private static string GetProfileStatus(Identify profile)
        {
            if (profile.IsPromoted) return "promoted";
            if (profile.RequestedPromotion) return "promotion-pending";
            if (!profile.IsBasicInfoApproved && IsBasicInfoComplete(profile)) return "basic-info-pending";
            if (profile.IsBasicInfoApproved) return "additional-info";
            return "basic-info";
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

        private static async Task<string> SaveCoverImageAsync(IFormFile coverImageFile)
        {
            var uploadsFolder = Path.Combine("C:\\Users", "Public", "MyApp_Uploads", "Profiles");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = Guid.NewGuid() + "_" + Path.GetFileName(coverImageFile.FileName);
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);
            using var fileStream = new FileStream(filePath, FileMode.Create);
            await coverImageFile.CopyToAsync(fileStream);
            return "/MyApp_Uploads/Profiles/" + uniqueFileName;
        }

        private static string NormalizeIraqPhoneNumber(string? phoneNumber)
        {
            var digits = new string((phoneNumber ?? string.Empty).Where(char.IsDigit).ToArray());
            if (string.IsNullOrWhiteSpace(digits)) return string.Empty;
            if (digits.StartsWith("00", StringComparison.Ordinal)) digits = digits[2..];
            if (digits.StartsWith("964", StringComparison.Ordinal)) return digits;
            if (digits.StartsWith("0", StringComparison.Ordinal)) return "964" + digits[1..];
            if (digits.StartsWith("7", StringComparison.Ordinal)) return "964" + digits;
            return digits;
        }

        private static string ToLocalIraqPhoneNumber(string phoneNumber)
        {
            var normalizedPhone = NormalizeIraqPhoneNumber(phoneNumber);
            return normalizedPhone.StartsWith("9647", StringComparison.Ordinal)
                ? "0" + normalizedPhone[3..]
                : phoneNumber;
        }

        private static List<string> GetGovernorates() => new()
        {
            "بغداد", "الأنبار", "بابل", "البصرة", "ذي قار", "القادسية", "ديالى", "دهوك", "أربيل",
            "كربلاء", "كركوك", "ميسان", "المثنى", "النجف", "نينوى", "صلاح الدين", "السليمانية", "واسط"
        };

        private static List<string> GetGenders() => new() { "ذكر", "أنثى" };
        private static List<string> GetEducations() => new() { "آمي", "ابتدائي", "متوسط", "إعدادي", "معهد", "طالب جامعي", "دبلوم", "بكالوريوس", "ماجستير", "دكتوراه" };
        private static List<string> GetMinistries() => IraqiGovernmentEntities.GetMinistries();
        private static List<string> GetEmploymentStatuses() => new() { "موظف", "كاسب", "متقاعد", "طالب", "قطاع خاص" };
        private static List<string> GetStudyStages() => new() { "المرحلة الأولى", "المرحلة الثانية", "المرحلة الثالثة", "المرحلة الرابعة", "المرحلة الخامسة", "المرحلة السادسة" };
        private static List<string> GetJobGrades() => new()
        {
            "الدرجة العاشرة", "الدرجة التاسعة", "الدرجة الثامنة", "الدرجة السابعة", "الدرجة السادسة",
            "الدرجة الخامسة", "الدرجة الرابعة", "الدرجة الثالثة", "الدرجة الثانية", "الدرجة الأولى"
        };
    }

    public class RegisterWhatsAppCodeRequest
    {
        public string? UserId { get; set; }
        public string? PhoneNumber { get; set; }
    }

    public class RegisterPhoneRequest
    {
        public string? PhoneNumber { get; set; }
    }

    public class ConfirmProfileWhatsAppRequest
    {
        public string? PhoneNumber { get; set; }
        public string? Code { get; set; }
    }

    public class RegisterCoverImageRequest
    {
        public IFormFile? CoverImageFile { get; set; }
    }
}

