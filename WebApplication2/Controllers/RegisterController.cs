using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using WebApplication2.Data;
using WebApplication2.Models;
using WebApplication2.Models.Helpers;
using WebApplication2.Models.Profile;
using WebApplication2.Services;

namespace WebApplication2.Controllers
{
    public class RegisterController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<RegisterController> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly INotificationService _notificationService;
        private readonly IOtpService _otpService;

        private readonly ApplicationDbContext _context;
        private readonly string _profileUploadPath;

        public RegisterController(
            UserManager<IdentityUser> userManager,
            IEmailSender emailSender,
            ApplicationDbContext context,
            ILogger<RegisterController> logger,
            IWebHostEnvironment webHostEnvironment,
            INotificationService notificationService,
            IOtpService otpService)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _context = context;
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
            _notificationService = notificationService;
            _otpService = otpService;

            _profileUploadPath = Path.Combine("C:\\Users", "Public", "MyApp_Uploads", "Profiles");

            if (!Directory.Exists(_profileUploadPath))
            {
                Directory.CreateDirectory(_profileUploadPath);
            }
        }

        #region ========== عمليات التسجيل الأساسية ==========

        // GET: /Register
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Index()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }
            return View(new RegisterViewModel());
        }

        // POST: /Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> Index(RegisterViewModel model)
        {
            ModelState.Remove(nameof(RegisterViewModel.PhoneNumber));
            if (string.IsNullOrWhiteSpace(model.Email))
                ModelState.AddModelError(nameof(RegisterViewModel.Email), "البريد الإلكتروني مطلوب");

            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var existingUser = await _userManager.FindByEmailAsync(model.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError("Email", "هذا البريد الإلكتروني مسجل مسبقاً");
                    return View(model);
                }

                var user = new IdentityUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    EmailConfirmed = false
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    _logger.LogInformation("✅ تم إنشاء حساب جديد: {Email}", model.Email);
                    await _userManager.AddToRoleAsync(user, clsRoles.User);
                    await SendConfirmationEmailAsync(user);

                    var identify = new Identify
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
                    };

                    _context.Identifies.Add(identify);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "✅ تم إنشاء حسابك بنجاح! تم إرسال رابط تأكيد إلى بريدك الإلكتروني. يرجى تأكيد بريدك قبل تسجيل الدخول.";
                    return RedirectToAction("CheckEmail", "Register", new { email = model.Email });

                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                    _logger.LogError("❌ خطأ في التسجيل: {Error}", error.Description);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ أثناء التسجيل");
                ModelState.AddModelError("", "حدث خطأ. حاول مرة أخرى.");
            }

            return View(model);
        }

        #endregion

        #region ========== إدارة الملف الشخصي ==========

        // ===== دوال مساعدة لجلب البيانات المرتبطة باستخدام DbContext مباشرة =====
        private async Task<Address?> GetUserAddressAsync(string userId)
        {
            return await _context.Addresses
                .FirstOrDefaultAsync(a => a.UserId == userId);
        }

        private async Task<VoterCard?> GetUserVoterCardAsync(string userId)
        {
            return await _context.VoterCards
                .FirstOrDefaultAsync(v => v.UserId == userId);
        }

        private async Task<UnionMembership?> GetUserUnionAsync(string userId)
        {
            return await _context.UnionMemberships
                .FirstOrDefaultAsync(u => u.UserId == userId);
        }

        private async Task<FederationMembership?> GetUserFederationAsync(string userId)
        {
            return await _context.FederationMemberships
                .Include(f => f.Federation)
                .Include(f => f.FederationDivision)
                .Include(f => f.FederationSection)
                .Include(f => f.FederationGroup)
                .FirstOrDefaultAsync(f => f.UserId == userId);
        }

        private async Task<AssociationMembership?> GetUserAssociationAsync(string userId)
        {
            return await _context.AssociationMemberships
                .FirstOrDefaultAsync(a => a.UserId == userId);
        }

        private async Task<NgoMembership?> GetUserNgoAsync(string userId)
        {
            return await _context.NgoMemberships
                .FirstOrDefaultAsync(n => n.UserId == userId);
        }

        private async Task<AffiliationInfo?> GetUserAffiliationInfoAsync(string userId)
        {
            return await _context.AffiliationInfos
                .Include(a => a.AffiliationEntity)
                .Include(a => a.Division)
                .Include(a => a.Section)
                .Include(a => a.Group)
                .FirstOrDefaultAsync(a => a.UserId == userId);
        }

        // ===== دالة مساعدة لإنشاء BasicInfoViewModel =====
        private async Task<BasicInfoViewModel> CreateBasicInfoViewModelAsync(Identify profile, IdentityUser user)
        {
            var address = profile != null ? await GetUserAddressAsync(user.Id) : null;
            var workLocation = profile != null
                ? await _context.WorkLocations.FirstOrDefaultAsync(w => w.IdentifyId == profile.Id)
                : null;

            return new BasicInfoViewModel
            {
                UserId = user.Id,
                Email = user.Email,
                ExistingCoverImage = profile?.CoverImage,

                PersonalInfo = new PersonalInfoViewModel
                {
                    FullName = profile?.FullName,
                    LastName = profile?.LastName,
                    MotherName = profile?.MotherName,
                    DateOfBirth = profile?.Date ?? DateTime.Now,
                    Gender = profile?.Gender,
                    MaritalStatus = profile?.MaritalStatus,
                    Education = profile?.Education,
                    Specialization = profile?.Specialization,
                    PhoneNumber = profile?.PhoneNumber,
                    CoverImage = profile?.CoverImage,
                    UniversityType = profile?.UniversityType,
                    InstitutionType = profile?.InstitutionType,
                    InstitutionName = profile?.InstitutionName,
                    FacultyDepartment = profile?.FacultyDepartment,
                    StudyType = profile?.StudyType,
                    StudyStage = profile?.StudyStage,
                    StudyStagesList = GetStudyStages()
                },

                Address = new AddressViewModel
                {
                    Governorate = address?.Governorate,
                    District = address?.District,
                    Area = address?.Area,
                    Alley = address?.Alley,
                    Street = address?.Street,
                    House = address?.House,
                    NearestPoint = address?.NearestPoint
                },

                WorkLocation = new WorkLocationViewModel
                {
                    Governorate = workLocation?.Governorate ?? profile?.WorkGovernorate,
                    District = workLocation?.District ?? profile?.WorkDistrict
                },

                Documents = new DocumentsViewModel
                {
                    VoterCardNumber = null,
                    PollingCenterNumber = null
                },

                Employment = new EmploymentViewModel
                {
                    EmploymentStatus = profile?.EmploymentStatus,
                    Work = profile?.Work,
                    Ministry = profile?.Ministry,
                    Department = profile?.Department,
                    Position = profile?.Position,
                    JobTitle = profile?.JobTitle,
                    JobGrade = profile?.JobGrade,
                    JobGradesList = GetJobGrades()
                },

                Governorates = GetGovernorates(),
                Genders = GetGenders(),
                Educations = GetEducations(),
                Ministries = GetMinistries(),
                EmploymentStatuses = GetEmploymentStatuses(),
                BaghdadDistricts = new List<string> { "الكرخ", "الرصافة" }

            };

        }

        // GET: /Register/BasicInfo
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> BasicInfo()
        {
            var userId = _userManager.GetUserId(User);
            _logger.LogInformation("🔍 طلب البيانات الأساسية: {UserId}", userId);

            if (string.IsNullOrEmpty(userId))
            {
                TempData["ErrorMessage"] = "يجب تسجيل الدخول أولاً.";
                return RedirectToAction("Login", "Account");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("⚠️ تعذر تحميل المستخدم الحالي في BasicInfo GET رغم وجود UserId: {UserId}", userId);
                TempData["ErrorMessage"] = "تعذر تحميل بيانات حسابك الحالي. يرجى تسجيل الدخول مرة أخرى.";
                return RedirectToAction("Login", "Account");
            }

            var profile = await _context.Identifies
                .FirstOrDefaultAsync(i => i.UserId == userId);

            if (profile == null)
            {
                profile = new Identify
                {
                    UserId = userId,
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

                var viewModel = await CreateBasicInfoViewModelAsync(profile, user);
                return View(viewModel);
            }

            var address = await GetUserAddressAsync(userId);

            if (IsBasicInfoComplete(profile))
            {
                if (profile.IsBasicInfoApproved || profile.RequestedPromotion == true || profile.IsPromoted)
                {
                    TempData["WarningMessage"] = "⚠️ لقد أكملت بياناتك الأساسية بالفعل. لا يمكنك تعديلها مرة أخرى.";

                    if (profile.IsPromoted)
                    {
                        return RedirectToAction("ProfileDetails");
                    }
                    else if (profile.RequestedPromotion == true)
                    {
                        return RedirectToAction("PromotionPending");
                    }
                    else if (profile.IsBasicInfoApproved)
                    {
                        return RedirectToAction("AdditionalInfo");
                    }
                }

                return RedirectToAction("AdditionalInfo");
            }

            if (profile.IsPromoted)
            {
                TempData["InfoMessage"] = "مرحباً بعودتك! هذا ملفك الشخصي.";
                return RedirectToAction("ProfileDetails");
            }

            if (IsBasicInfoComplete(profile) && profile.IsBasicInfoApproved)
            {
                return RedirectToAction("AdditionalInfo");
            }

            if (IsBasicInfoComplete(profile) && !profile.IsBasicInfoApproved)
            {
                return RedirectToAction("BasicInfoPending");
            }

            var model = await CreateBasicInfoViewModelAsync(profile, user);
            // ✅ تأكد من تحميل جميع القوائم
            model.Governorates = GetGovernorates();
            model.Genders = GetGenders();
            model.Educations = GetEducations();
            model.Ministries = GetMinistries();
            model.EmploymentStatuses = GetEmploymentStatuses();
            model.BaghdadDistricts = new List<string> { "الكرخ", "الرصافة" };

            if (model.PersonalInfo != null)
            {
                model.PersonalInfo.StudyStagesList = GetStudyStages();
            }
            if (model.Employment != null)
            {
                model.Employment.JobGradesList = GetJobGrades();
            }
            return View(model);
        }

        // POST: /Register/BasicInfo
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BasicInfo(BasicInfoViewModel model)
        {
            _logger.LogInformation("=== 🚀 حفظ البيانات الأساسية ===");

            _logger.LogInformation($"📸 Request.Form.Files count: {Request.Form.Files.Count}");
            if (Request.Form.Files.Count > 0)
            {
                var file = Request.Form.Files[0];
                _logger.LogInformation($"📸 اسم الملف في الطلب: {file.FileName}, الحجم: {file.Length}");
            }
            else
            {
                _logger.LogWarning("⚠️ لا يوجد ملفات في الطلب!");
            }

            if (model.CoverImageFile == null && Request.Form.Files.Count > 0)
            {
                _logger.LogInformation($"📸 model.CoverImageFile: {(model.CoverImageFile != null ? model.CoverImageFile.FileName : "null")}");
                _logger.LogInformation($"📸 model.CoverImageFile Size: {(model.CoverImageFile != null ? model.CoverImageFile.Length.ToString() : "0")}");
                _logger.LogInformation($"📸 model.ExistingCoverImage: {model.ExistingCoverImage ?? "null"}");
                _logger.LogInformation($"📸 Request.Form.Files.Count: {Request.Form.Files.Count}");

                if (Request.Form.Files.Count > 0)
                {
                    var file = Request.Form.Files[0];
                    _logger.LogInformation($"📸 الملف في الطلب: {file.FileName}, الحجم: {file.Length}");
                    model.CoverImageFile = Request.Form.Files[0];
                    _logger.LogInformation("✅ تم تعيين CoverImageFile من Request.Form.Files");
                }
            }

            model.Governorates = GetGovernorates();
            model.Genders = GetGenders();
            model.Educations = GetEducations();
            model.Ministries = GetMinistries();
            model.EmploymentStatuses = GetEmploymentStatuses();

            if (ModelState.ContainsKey("PersonalInfo.CoverImage"))
            {
                ModelState["PersonalInfo.CoverImage"].Errors.Clear();
            }

            if (ModelState.ContainsKey("CoverImage"))
            {
                ModelState["CoverImage"].Errors.Clear();
            }

            var existingProfile = await _context.Identifies
                .FirstOrDefaultAsync(i => i.UserId == model.UserId);
            var hasExistingImage = !string.IsNullOrEmpty(model.ExistingCoverImage) || !string.IsNullOrEmpty(existingProfile?.CoverImage);

            if (model.CoverImageFile == null && !hasExistingImage)
            {
                ModelState.AddModelError("CoverImageFile", "الصورة الشخصية مطلوبة");
            }

            if (string.IsNullOrWhiteSpace(model.WorkLocation.Governorate))
            {
                ModelState.AddModelError("WorkLocation.Governorate", "محافظة العمل التنظيمي مطلوبة");
            }

            if (model.Address?.Governorate == "بغداد" && string.IsNullOrWhiteSpace(model.Address.District))
            {
                ModelState.AddModelError("Address.District", "القضاء مطلوب عند اختيار محافظة السكن بغداد");
            }

            if (model.PersonalInfo.Education == "طالب جامعي")
            {
                if (string.IsNullOrWhiteSpace(model.PersonalInfo.UniversityType))
                {
                    ModelState.AddModelError("PersonalInfo.UniversityType", "نوع الجامعة مطلوب للطالب الجامعي");
                }
                if (string.IsNullOrWhiteSpace(model.PersonalInfo.InstitutionType))
                {
                    ModelState.AddModelError("PersonalInfo.InstitutionType", "نوع المؤسسة مطلوب للطالب الجامعي");
                }
                if (string.IsNullOrWhiteSpace(model.PersonalInfo.InstitutionName))
                {
                    ModelState.AddModelError("PersonalInfo.InstitutionName", "اسم الجامعة أو المعهد مطلوب للطالب الجامعي");
                }
                if (string.IsNullOrWhiteSpace(model.PersonalInfo.FacultyDepartment))
                {
                    ModelState.AddModelError("PersonalInfo.FacultyDepartment", "الكلية أو القسم مطلوب للطالب الجامعي");
                }
                if (string.IsNullOrWhiteSpace(model.PersonalInfo.StudyType))
                {
                    ModelState.AddModelError("PersonalInfo.StudyType", "نوع الدراسة مطلوب للطالب الجامعي");
                }
                if (string.IsNullOrWhiteSpace(model.PersonalInfo.StudyStage))
                {
                    ModelState.AddModelError("PersonalInfo.StudyStage", "المرحلة الدراسية مطلوبة للطالب الجامعي");
                }
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
                if (string.IsNullOrWhiteSpace(model.Employment.Ministry))
                {
                    ModelState.AddModelError("Employment.Ministry", "الوزارة مطلوبة للموظفين");
                }
                if (string.IsNullOrWhiteSpace(model.Employment.Department))
                {
                    ModelState.AddModelError("Employment.Department", "الدائرة مطلوبة للموظفين");
                }
                if (string.IsNullOrWhiteSpace(model.Employment.Position))
                {
                    ModelState.AddModelError("Employment.Position", "المنصب مطلوب للموظفين");
                }
                if (string.IsNullOrWhiteSpace(model.Employment.JobTitle))
                {
                    ModelState.AddModelError("Employment.JobTitle", "العنوان الوظيفي مطلوب للموظفين");
                }
                if (string.IsNullOrWhiteSpace(model.Employment.JobGrade))
                {
                    ModelState.AddModelError("Employment.JobGrade", "الدرجة الوظيفية مطلوبة للموظفين");
                }
            }
            else
            {
                model.Employment.Ministry = null;
                model.Employment.Department = null;
                model.Employment.Position = null;
                model.Employment.JobTitle = null;
                model.Employment.JobGrade = null;
            }

            NormalizeWorkLocation(model.WorkLocation);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("❌ النموذج غير صالح");

                // ✅ أعد تحميل جميع القوائم المنسدلة
                model.Governorates = GetGovernorates();
                model.Genders = GetGenders();
                model.Educations = GetEducations();
                model.Ministries = GetMinistries();
                model.EmploymentStatuses = GetEmploymentStatuses();
                model.BaghdadDistricts = new List<string> { "الكرخ", "الرصافة" };

                // ✅ أعد تحميل قوائم الطالب والموظف
                if (model.PersonalInfo != null)
                {
                    model.PersonalInfo.StudyStagesList = GetStudyStages();
                }
                if (model.Employment != null)
                {
                    model.Employment.JobGradesList = GetJobGrades();
                }

                return View(model);
            }

            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "لم يتم العثور على المستخدم";
                    return RedirectToAction("Login", "Account");
                }

                string? coverImagePath = null;
                if (model.CoverImageFile != null && model.CoverImageFile.Length > 0)
                {
                    coverImagePath = await SaveCoverImage(model.CoverImageFile);
                }

                var profile = await _context.Identifies
                    .FirstOrDefaultAsync(i => i.UserId == model.UserId);

                if (profile == null)
                {
                    profile = new Identify { UserId = model.UserId };
                    _context.Identifies.Add(profile);
                }

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

                if (coverImagePath != null)
                {
                    if (!string.IsNullOrEmpty(profile.CoverImage))
                    {
                        var oldImagePath = Path.Combine(_webHostEnvironment.WebRootPath,
                            profile.CoverImage.TrimStart('/'));
                        if (System.IO.File.Exists(oldImagePath))
                        {
                            System.IO.File.Delete(oldImagePath);
                        }
                    }
                    profile.CoverImage = coverImagePath;
                }

                _context.Identifies.Update(profile);
                await _context.SaveChangesAsync();

                await UpdateOrCreateWorkLocation(profile.Id, model.WorkLocation);
                await UpdateOrCreateAddress(model.UserId, model.Address);

                profile.IsBasicInfoApproved = false;
                profile.BasicInfoRequestedAt = IraqTime.Now();
                profile.BasicInfoApprovedBy = null;
                profile.BasicInfoApprovalDate = null;
                profile.BasicInfoRejectionReason = null;

                _context.Identifies.Update(profile);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"✅ تم حفظ البيانات الأساسية للمستخدم {user.Email}");

                TempData["SuccessMessage"] = "✅ تم حفظ البيانات الأساسية! بانتظار مراجعة الإدارة.";
                return RedirectToAction("BasicInfoPending", "Register");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ في حفظ البيانات الأساسية");
                ViewBag.ErrorMessage = "حدث خطأ أثناء الحفظ: " + ex.Message;
                return View(model);
            }
        }

        private async Task UpdateOrCreateAddress(string userId, AddressViewModel model)
        {
            if (string.IsNullOrEmpty(model.Governorate))
                return;

            var existingAddress = await GetUserAddressAsync(userId);

            if (existingAddress != null)
            {
                existingAddress.Governorate = model.Governorate ?? "";
                existingAddress.District = model.District ?? "";
                existingAddress.Area = model.Area ?? "";
                existingAddress.Alley = model.Alley ?? "";
                existingAddress.Street = model.Street ?? "";
                existingAddress.House = model.House ?? "";
                existingAddress.NearestPoint = model.NearestPoint ?? "";
                _context.Addresses.Update(existingAddress);
            }
            else
            {
                var address = new Address
                {
                    UserId = userId,
                    Governorate = model.Governorate ?? "",
                    District = model.District ?? "",
                    Area = model.Area ?? "",
                    Alley = model.Alley ?? "",
                    Street = model.Street ?? "",
                    House = model.House ?? "",
                    NearestPoint = model.NearestPoint ?? ""
                };
                _context.Addresses.Add(address);
            }

            await _context.SaveChangesAsync();
        }

        // GET: /Register/BasicInfoPending
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> BasicInfoPending()
        {
            var userId = _userManager.GetUserId(User);
            var profile = await _context.Identifies
                .FirstOrDefaultAsync(i => i.UserId == userId);

            if (profile == null)
                return RedirectToAction("BasicInfo");

            if (profile.IsBasicInfoApproved)
            {
                TempData["SuccessMessage"] = "✅ تمت الموافقة على معلوماتك الأساسية! أكمل البيانات الإضافية.";
                return RedirectToAction("AdditionalInfo", "Register");
            }

            return View();
        }

        // GET: /Register/AdditionalInfo
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> AdditionalInfo()
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userManager.GetUserAsync(User);

            var profile = await _context.Identifies
                .FirstOrDefaultAsync(i => i.UserId == userId);

            if (profile == null)
            {
                return RedirectToAction("BasicInfo");
            }

            if (profile.IsPromoted)
            {
                TempData["WarningMessage"] = "⚠️ أنت بالفعل عضو معتمد. لا يمكنك تعديل البيانات الإضافية.";
                return RedirectToAction("ProfileDetails");
            }

            if (profile.RequestedPromotion == true)
            {
                TempData["WarningMessage"] = "⚠️ لقد قمت بإكمال البيانات الإضافية مسبقاً. طلبك قيد المراجعة.";
                return RedirectToAction("PromotionPending");
            }

            if (!profile.IsBasicInfoApproved)
            {
                TempData["WarningMessage"] = "معلوماتك الأساسية لم توافق عليها بعد.";
                return RedirectToAction("BasicInfoPending");
            }

            var existingVoterCard = await GetUserVoterCardAsync(userId);
            var existingAffiliation = await GetUserAffiliationInfoAsync(userId);

            if (existingVoterCard != null && !string.IsNullOrEmpty(existingVoterCard.VoterCardNumber) &&
                existingAffiliation != null && !string.IsNullOrEmpty(existingAffiliation.MozakeName))
            {
                if (profile.RequestedPromotion != true)
                {
                    profile.RequestedPromotion = true;
                    profile.RequestedPromotionDate = DateTime.Now;
                    profile.AccountType = "مكتمل";
                    _context.Identifies.Update(profile);
                    await _context.SaveChangesAsync();
                }

                TempData["WarningMessage"] = "⚠️ لقد قمت بإكمال البيانات الإضافية مسبقاً. طلبك قيد المراجعة.";
                return RedirectToAction("PromotionPending");
            }

            var voterCard = await GetUserVoterCardAsync(userId);
            var affiliationInfo = await GetUserAffiliationInfoAsync(userId);
            var union = await GetUserUnionAsync(userId);
            var federation = await GetUserFederationAsync(userId);
            var association = await GetUserAssociationAsync(userId);
            var ngo = await GetUserNgoAsync(userId);

            var viewModel = new AdditionalInfoViewModel
            {
                UserId = userId,
                Email = user.Email,

                AffiliationEntities = (await _context.AffiliationEntities.ToListAsync()).Select(e => e.Name).ToList(),
                DivisionsList = (await _context.Divisions.ToListAsync()).Select(d => d.Name).Distinct().ToList(),
                SectionsList = (await _context.Sections.ToListAsync()).Select(s => s.Name).Distinct().ToList(),
                GroupsList = (await _context.Groups.ToListAsync()).Select(g => g.Name).Distinct().ToList(),

                UnionsList = (await _context.Unions.ToListAsync()).Select(u => u.Name).ToList(),
                FederationsList = (await _context.Federations.ToListAsync()).Select(f => f.Name).ToList(),
                AssociationsList = (await _context.Associations.ToListAsync()).Select(a => a.Name).ToList(),
                NgosList = (await _context.Ngos.ToListAsync()).Select(n => n.Name).ToList()
            };

            if (voterCard != null)
            {
                viewModel.Documents.VoterCardNumber = voterCard.VoterCardNumber;
                viewModel.Documents.PollingCenterNumber = voterCard.PollingCenterNumber;
            }

            if (affiliationInfo != null)
            {
                if (affiliationInfo.AffiliationEntityId.HasValue)
                {
                    var entity = await _context.AffiliationEntities
                        .FirstOrDefaultAsync(e => e.Id == affiliationInfo.AffiliationEntityId.Value);
                    viewModel.Affiliation.AffiliationEntity = entity?.Name;
                }
                if (affiliationInfo.DivisionId.HasValue)
                {
                    var division = await _context.Divisions
                        .FirstOrDefaultAsync(d => d.Id == affiliationInfo.DivisionId.Value);
                    viewModel.Affiliation.Division = division?.Name;
                }
                if (affiliationInfo.SectionId.HasValue)
                {
                    var section = await _context.Sections
                        .FirstOrDefaultAsync(s => s.Id == affiliationInfo.SectionId.Value);
                    viewModel.Affiliation.Section = section?.Name;
                }
                if (affiliationInfo.GroupId.HasValue)
                {
                    var group = await _context.Groups
                        .FirstOrDefaultAsync(g => g.Id == affiliationInfo.GroupId.Value);
                    viewModel.Affiliation.Group = group?.Name;
                }
                viewModel.Affiliation.MozakeName = affiliationInfo.MozakeName;
                viewModel.Affiliation.MozakePhoneNumber = affiliationInfo.MozakePhoneNumber;
                viewModel.Affiliation.BadgeNumber = affiliationInfo.BadgeNumber;
                viewModel.Affiliation.AffiliationDate = affiliationInfo.AffiliationDate;
            }

            if (union != null)
            {
                viewModel.Memberships.UnionName = union.UnionName;
                viewModel.Memberships.UnionPosition = union.Position;
                viewModel.Memberships.UnionIdNumber = union.IdNumber;
                viewModel.Memberships.UnionAffiliationDate = union.AffiliationDate;
            }

            if (federation != null)
            {
                string federationFullName = "";

                if (federation.Federation != null)
                    federationFullName = federation.Federation.Name;

                if (federation.FederationDivision != null)
                    federationFullName += " - " + federation.FederationDivision.Name;

                if (federation.FederationSection != null)
                    federationFullName += " - " + federation.FederationSection.Name;

                if (federation.FederationGroup != null)
                    federationFullName += " - " + federation.FederationGroup.Name;

                viewModel.Memberships.FederationName = federationFullName;
                viewModel.Memberships.FederationPosition = federation.Position;
                viewModel.Memberships.FederationIdNumber = federation.IdNumber;
                viewModel.Memberships.FederationAffiliationDate = federation.AffiliationDate;
            }

            if (association != null)
            {
                viewModel.Memberships.AssociationName = association.AssociationName;
                viewModel.Memberships.AssociationPosition = association.Position;
                viewModel.Memberships.AssociationIdNumber = association.IdNumber;
                viewModel.Memberships.AssociationAffiliationDate = association.AffiliationDate;
            }

            if (ngo != null)
            {
                viewModel.Memberships.NgoName = ngo.NgoName;
                viewModel.Memberships.NgoPosition = ngo.Position;
                viewModel.Memberships.NgoIdNumber = ngo.IdNumber;
                viewModel.Memberships.NgoAffiliationDate = ngo.AffiliationDate;
            }

            await LoadAdditionalInfoLists(viewModel);
            return View(viewModel);
        }

        // POST: /Register/AdditionalInfo
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdditionalInfo(AdditionalInfoViewModel model)
        {
            System.Diagnostics.Debug.WriteLine("========== تم استدعاء AdditionalInfo POST ==========");
            await LoadAdditionalInfoLists(model);
            System.Diagnostics.Debug.WriteLine($"UserId: {model.UserId}");
            System.Diagnostics.Debug.WriteLine($"AffiliationDate: {model.Affiliation.AffiliationDate}");
            System.Diagnostics.Debug.WriteLine($"ModelState.IsValid: {ModelState.IsValid}");

            _logger.LogInformation("=== 🚀 حفظ المعلومات الإضافية ===");

            model.AffiliationEntities = await GetDistinctAffiliationEntitiesAsync();
            model.DivisionsList = await GetDistinctDivisionsAsync();
            model.SectionsList = await GetDistinctSectionsAsync();
            model.GroupsList = await GetDistinctGroupsAsync();

            model.UnionsList = await GetDistinctUnionsAsync();
            model.FederationsList = await GetDistinctFederationsAsync();
            model.AssociationsList = await GetDistinctAssociationsAsync();
            model.NgosList = await GetDistinctNgosAsync();

            if (string.IsNullOrWhiteSpace(model.Documents.VoterCardNumber))
            {
                ModelState.AddModelError("Documents.VoterCardNumber", "رقم بطاقة الناخب مطلوب");
            }
            else if (model.Documents.VoterCardNumber.Length != 8)
            {
                ModelState.AddModelError("Documents.VoterCardNumber", "رقم بطاقة الناخب يجب أن يكون 8 أرقام");
            }

            if (string.IsNullOrWhiteSpace(model.Documents.PollingCenterNumber))
            {
                ModelState.AddModelError("Documents.PollingCenterNumber", "رقم مركز الاقتراع مطلوب");
            }

            if (string.IsNullOrWhiteSpace(model.Affiliation.AffiliationEntity))
            {
                ModelState.AddModelError("Affiliation.AffiliationEntity", "جهة الانتساب مطلوبة");
            }

            if (string.IsNullOrWhiteSpace(model.Affiliation.Division))
            {
                ModelState.AddModelError("Affiliation.Division", "القسم مطلوب");
            }

            if (!string.IsNullOrWhiteSpace(model.Affiliation.AffiliationEntity) &&
                !string.IsNullOrWhiteSpace(model.Affiliation.Division))
            {
                var entity = await _context.AffiliationEntities
                    .FirstOrDefaultAsync(e => e.Name == model.Affiliation.AffiliationEntity);

                if (entity != null)
                {
                    var division = await _context.Divisions
                        .FirstOrDefaultAsync(d => d.AffiliationEntityId == entity.Id && d.Name == model.Affiliation.Division);

                    if (division != null)
                    {
                        var divisionHasSections = await _context.Sections
                            .AnyAsync(s => s.DivisionId == division.Id);

                        if (divisionHasSections)
                        {
                            if (string.IsNullOrWhiteSpace(model.Affiliation.Section))
                            {
                                ModelState.AddModelError("Affiliation.Section", "الشعبة مطلوبة لهذا القسم");
                            }
                            else if (string.Equals(model.Affiliation.Section.Trim(), "التجمعات التخصصية", StringComparison.Ordinal))
                            {
                                if (string.IsNullOrWhiteSpace(model.Affiliation.Group))
                                {
                                    ModelState.AddModelError("Affiliation.Group", "الوحدة مطلوبة عند اختيار شعبة التجمعات التخصصية");
                                }
                            }
                            else
                            {
                                model.Affiliation.Group = null;
                            }
                        }
                        else
                        {
                            model.Affiliation.Section = null;
                            model.Affiliation.Group = null;
                        }
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(model.Affiliation.MozakeName))
            {
                ModelState.AddModelError("Affiliation.MozakeName", "اسم المزكي مطلوب");
            }

            if (string.IsNullOrWhiteSpace(model.Affiliation.BadgeNumber))
            {
                ModelState.AddModelError("Affiliation.BadgeNumber", "رقم الباج مطلوب");
            }

            if (model.Affiliation.AffiliationDate == null || model.Affiliation.AffiliationDate == default)
            {
                ModelState.AddModelError("Affiliation.AffiliationDate", "تاريخ الانتماء مطلوب");
            }

            if (!string.IsNullOrWhiteSpace(model.Memberships.UnionName))
            {
                if (model.Memberships.UnionAffiliationDate == null || model.Memberships.UnionAffiliationDate == default)
                {
                    ModelState.AddModelError("Memberships.UnionAffiliationDate", "تاريخ النفاذ للنقابة مطلوب");
                }
            }

            if (!string.IsNullOrWhiteSpace(model.Memberships.FederationName))
            {
                if (model.Memberships.FederationAffiliationDate == null || model.Memberships.FederationAffiliationDate == default)
                {
                    ModelState.AddModelError("Memberships.FederationAffiliationDate", "تاريخ النفاذ للاتحاد مطلوب");
                }
            }

            if (!string.IsNullOrWhiteSpace(model.Memberships.AssociationName))
            {
                if (model.Memberships.AssociationAffiliationDate == null || model.Memberships.AssociationAffiliationDate == default)
                {
                    ModelState.AddModelError("Memberships.AssociationAffiliationDate", "تاريخ النفاذ للجمعية مطلوب");
                }
            }

            if (!string.IsNullOrWhiteSpace(model.Memberships.NgoName))
            {
                if (model.Memberships.NgoAffiliationDate == null || model.Memberships.NgoAffiliationDate == default)
                {
                    ModelState.AddModelError("Memberships.NgoAffiliationDate", "تاريخ النفاذ للمنظمة مطلوب");
                }
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("❌ النموذج الإضافي غير صالح");

                System.Diagnostics.Debug.WriteLine("========== أخطاء ModelState ==========");
                foreach (var key in ModelState.Keys)
                {
                    var errors = ModelState[key].Errors;
                    foreach (var error in errors)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ خطأ في {key}: {error.ErrorMessage}");
                    }
                }
                System.Diagnostics.Debug.WriteLine("======================================");
                await LoadAdditionalInfoLists(model);
                

                return View(model);
            }

            try
            {
                var user = await _userManager.GetUserAsync(User);
                var profile = await _context.Identifies
                    .FirstOrDefaultAsync(i => i.UserId == model.UserId);

                if (profile == null)
                {
                    return RedirectToAction("BasicInfo");
                }

                await UpdateOrCreateVoterCard(model.UserId, model.Documents);
                await UpdateOrCreateAffiliationInfo(model.UserId, model.Affiliation);
                await UpdateOrCreateUnion(model.UserId, model.Memberships);
                await UpdateOrCreateFederation(model.UserId, model.Memberships);
                await UpdateOrCreateAssociation(model.UserId, model.Memberships);
                await UpdateOrCreateNgo(model.UserId, model.Memberships);

                profile.RequestedPromotion = true;
                profile.RequestedPromotionDate = DateTime.Now;
                profile.AccountType = "مكتمل";

                _context.Identifies.Update(profile);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "✅ تم حفظ المعلومات الإضافية! بانتظار مراجعة الإدارة.";
                return RedirectToAction("PromotionPending", "Register");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ في حفظ المعلومات الإضافية");
                ViewBag.ErrorMessage = "حدث خطأ أثناء الحفظ";
                return View(model);
            }
        }

        private async Task UpdateOrCreateVoterCard(string userId, DocumentsViewModel model)
        {
            if (string.IsNullOrEmpty(model.VoterCardNumber) && string.IsNullOrEmpty(model.PollingCenterNumber))
                return;

            var existing = await GetUserVoterCardAsync(userId);

            if (existing != null)
            {
                existing.VoterCardNumber = model.VoterCardNumber;
                existing.PollingCenterNumber = model.PollingCenterNumber;
                _context.VoterCards.Update(existing);
            }
            else
            {
                var voterCard = new VoterCard
                {
                    UserId = userId,
                    VoterCardNumber = model.VoterCardNumber,
                    PollingCenterNumber = model.PollingCenterNumber
                };
                _context.VoterCards.Add(voterCard);
            }

            await _context.SaveChangesAsync();
        }

        private async Task UpdateOrCreateAffiliationInfo(string userId, AffiliationViewModel model)
        {
            if (string.IsNullOrEmpty(model.AffiliationEntity) && string.IsNullOrEmpty(model.Division) &&
                string.IsNullOrEmpty(model.Section) && string.IsNullOrEmpty(model.Group))
                return;

            var existing = await GetUserAffiliationInfoAsync(userId);

            int? affiliationEntityId = null;
            int? divisionId = null;
            int? sectionId = null;
            int? groupId = null;

            if (!string.IsNullOrEmpty(model.AffiliationEntity))
            {
                var entities = await _context.AffiliationEntities.ToListAsync();
                var foundEntity = entities.FirstOrDefault(e => e.Name == model.AffiliationEntity);
                affiliationEntityId = foundEntity?.Id;
            }

            if (!string.IsNullOrEmpty(model.Division) && affiliationEntityId.HasValue)
            {
                var divisions = await _context.Divisions
                    .Where(d => d.AffiliationEntityId == affiliationEntityId.Value)
                    .ToListAsync();
                var foundDivision = divisions.FirstOrDefault(d => d.Name == model.Division);
                divisionId = foundDivision?.Id;
            }

            if (!string.IsNullOrEmpty(model.Section) && divisionId.HasValue)
            {
                var sections = await _context.Sections
                    .Where(s => s.DivisionId == divisionId.Value)
                    .ToListAsync();
                var foundSection = sections.FirstOrDefault(s => s.Name == model.Section);
                sectionId = foundSection?.Id;
            }

            if (!string.IsNullOrEmpty(model.Group) && sectionId.HasValue)
            {
                var groups = await _context.Groups
                    .Where(g => g.SectionId == sectionId.Value)
                    .ToListAsync();
                var foundGroup = groups.FirstOrDefault(g => g.Name == model.Group);
                groupId = foundGroup?.Id;
            }

            if (existing != null)
            {
                existing.AffiliationEntityId = affiliationEntityId;
                existing.DivisionId = divisionId;
                existing.SectionId = sectionId;
                existing.GroupId = groupId;
                existing.MozakeName = model.MozakeName;
                existing.MozakePhoneNumber = model.MozakePhoneNumber;
                existing.BadgeNumber = model.BadgeNumber;
                existing.AffiliationDate = model.AffiliationDate;
                _context.AffiliationInfos.Update(existing);
            }
            else
            {
                var affiliationInfo = new AffiliationInfo
                {
                    UserId = userId,
                    AffiliationEntityId = affiliationEntityId,
                    DivisionId = divisionId,
                    SectionId = sectionId,
                    GroupId = groupId,
                    MozakeName = model.MozakeName,
                    MozakePhoneNumber = model.MozakePhoneNumber,
                    BadgeNumber = model.BadgeNumber,
                    AffiliationDate = model.AffiliationDate
                };
                _context.AffiliationInfos.Add(affiliationInfo);
            }

            await _context.SaveChangesAsync();
        }

        private async Task UpdateOrCreateUnion(string userId, MembershipViewModel model)
        {
            if (string.IsNullOrEmpty(model.UnionName))
                return;

            var existing = await GetUserUnionAsync(userId);

            if (existing != null)
            {
                existing.UnionName = model.UnionName;
                existing.Position = model.UnionPosition;
                existing.IdNumber = model.UnionIdNumber;
                existing.AffiliationDate = model.UnionAffiliationDate;
                _context.UnionMemberships.Update(existing);
            }
            else
            {
                var union = new UnionMembership
                {
                    UserId = userId,
                    UnionName = model.UnionName,
                    Position = model.UnionPosition,
                    IdNumber = model.UnionIdNumber,
                    AffiliationDate = model.UnionAffiliationDate,
                };
                _context.UnionMemberships.Add(union);
            }

            await _context.SaveChangesAsync();
        }

        private async Task UpdateOrCreateFederation(string userId, MembershipViewModel model)
        {
            if (string.IsNullOrEmpty(model.FederationName))
                return;

            var existing = await GetUserFederationAsync(userId);

            string federationName = model.FederationName;
            string? divisionName = null;
            string? sectionName = null;
            string? groupName = null;

            var parts = model.FederationName.Split(new[] { " - " }, StringSplitOptions.None);
            if (parts.Length >= 1) federationName = parts[0];
            if (parts.Length >= 2) divisionName = parts[1];
            if (parts.Length >= 3) sectionName = parts[2];
            if (parts.Length >= 4) groupName = parts[3];

            int? federationId = null;
            int? divisionId = null;
            int? sectionId = null;
            int? groupId = null;

            if (!string.IsNullOrEmpty(federationName))
            {
                var federationMaster = await _context.Federations
                    .FirstOrDefaultAsync(f => f.Name == federationName);
                federationId = federationMaster?.Id;
            }

            if (federationId.HasValue && !string.IsNullOrEmpty(divisionName))
            {
                var divisions = await _context.FederationDivisions
                    .Where(d => d.FederationId == federationId.Value)
                    .ToListAsync();
                var division = divisions.FirstOrDefault(d => d.Name == divisionName);
                divisionId = division?.Id;
            }

            if (divisionId.HasValue && !string.IsNullOrEmpty(sectionName))
            {
                var sections = await _context.FederationSections
                    .Where(s => s.FederationDivisionId == divisionId.Value)
                    .ToListAsync();
                var section = sections.FirstOrDefault(s => s.Name == sectionName);
                sectionId = section?.Id;
            }

            if (sectionId.HasValue && !string.IsNullOrEmpty(groupName))
            {
                var groups = await _context.FederationGroups
                    .Where(g => g.FederationSectionId == sectionId.Value)
                    .ToListAsync();
                var group = groups.FirstOrDefault(g => g.Name == groupName);
                groupId = group?.Id;
            }

            if (existing != null)
            {
                existing.FederationId = federationId;
                existing.FederationDivisionId = divisionId;
                existing.FederationSectionId = sectionId;
                existing.FederationGroupId = groupId;
                existing.Position = model.FederationPosition;
                existing.IdNumber = model.FederationIdNumber;
                existing.AffiliationDate = model.FederationAffiliationDate;
                _context.FederationMemberships.Update(existing);
            }
            else
            {
                var federation = new FederationMembership
                {
                    UserId = userId,
                    FederationId = federationId,
                    FederationDivisionId = divisionId,
                    FederationSectionId = sectionId,
                    FederationGroupId = groupId,
                    Position = model.FederationPosition,
                    IdNumber = model.FederationIdNumber,
                    AffiliationDate = model.FederationAffiliationDate,
                };
                _context.FederationMemberships.Add(federation);
            }

            await _context.SaveChangesAsync();
        }

        private async Task UpdateOrCreateAssociation(string userId, MembershipViewModel model)
        {
            if (string.IsNullOrEmpty(model.AssociationName))
                return;

            var existing = await GetUserAssociationAsync(userId);

            if (existing != null)
            {
                existing.AssociationName = model.AssociationName;
                existing.Position = model.AssociationPosition;
                existing.IdNumber = model.AssociationIdNumber;
                existing.AffiliationDate = model.AssociationAffiliationDate;
                _context.AssociationMemberships.Update(existing);
            }
            else
            {
                var association = new AssociationMembership
                {
                    UserId = userId,
                    AssociationName = model.AssociationName,
                    Position = model.AssociationPosition,
                    IdNumber = model.AssociationIdNumber,
                    AffiliationDate = model.AssociationAffiliationDate,
                };
                _context.AssociationMemberships.Add(association);
            }

            await _context.SaveChangesAsync();
        }

        private async Task UpdateOrCreateNgo(string userId, MembershipViewModel model)
        {
            if (string.IsNullOrEmpty(model.NgoName))
                return;

            var existing = await GetUserNgoAsync(userId);

            if (existing != null)
            {
                existing.NgoName = model.NgoName;
                existing.Position = model.NgoPosition;
                existing.IdNumber = model.NgoIdNumber;
                existing.AffiliationDate = model.NgoAffiliationDate;
                _context.NgoMemberships.Update(existing);
            }
            else
            {
                var ngo = new NgoMembership
                {
                    UserId = userId,
                    NgoName = model.NgoName,
                    Position = model.NgoPosition,
                    IdNumber = model.NgoIdNumber,
                    AffiliationDate = model.NgoAffiliationDate,
                };
                _context.NgoMemberships.Add(ngo);
            }

            await _context.SaveChangesAsync();
        }

        // GET: /Register/ProfileDetails
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> ProfileDetails()
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                TempData["ErrorMessage"] = "يجب تسجيل الدخول أولاً.";
                return RedirectToAction("Login", "Account");
            }

            var user = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(user);

            var profile = await _context.Identifies
                .FirstOrDefaultAsync(i => i.UserId == userId);

            if (profile == null)
            {
                return RedirectToAction("CompleteProfile");
            }

            var viewModel = await MapToCompleteProfileViewModelAsync(profile, user, roles.FirstOrDefault());
            return View(viewModel);
        }

        // GET: /Register/PromotionPending
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> PromotionPending()
        {
            var userId = _userManager.GetUserId(User);
            var profile = await _context.Identifies
                .FirstOrDefaultAsync(i => i.UserId == userId);

            if (profile == null)
                return RedirectToAction("BasicInfo");

            if (profile.IsPromoted)
            {
                TempData["SuccessMessage"] = "🎉 تهانينا! تم ترقية حسابك.";
                return RedirectToAction("ProfileDetails");
            }

            return View();
        }

        // GET: /Register/CompleteProfile
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> CompleteProfile()
        {
            var userId = _userManager.GetUserId(User);
            _logger.LogInformation("🔍 طلب إكمال الملف الشخصي: {UserId}", userId);

            if (string.IsNullOrEmpty(userId))
            {
                TempData["ErrorMessage"] = "يجب تسجيل الدخول أولاً.";
                return RedirectToAction("Login", "Account");
            }

            var user = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(user);

            var existingProfile = await _context.Identifies
                .FirstOrDefaultAsync(i => i.UserId == userId);

            var address = await GetUserAddressAsync(userId);
            var voterCard = await GetUserVoterCardAsync(userId);
            var union = await GetUserUnionAsync(userId);
            var federation = await GetUserFederationAsync(userId);
            var association = await GetUserAssociationAsync(userId);
            var ngo = await GetUserNgoAsync(userId);
            var affiliationInfo = await GetUserAffiliationInfoAsync(userId);

            if (existingProfile != null && IsProfileComplete(existingProfile, address, voterCard, affiliationInfo))
            {
                _logger.LogInformation("✅ الملف الشخصي مكتمل، توجيه إلى صفحة العرض");
                return RedirectToAction("ProfileDetails");
            }

            var viewModel = new CompleteProfileViewModel
            {
                UserId = userId,
                Email = user.Email,
                UserRole = roles.FirstOrDefault() ?? "User",
                IsEmailConfirmed = user.EmailConfirmed,
                CreatedAt = DateTime.UtcNow,

                Governorates = GetGovernorates(),
                Genders = GetGenders(),
                Educations = GetEducations(),
                Ministries = GetMinistries(),
                EmploymentStatuses = GetEmploymentStatuses(),

                AffiliationEntities = await GetDistinctAffiliationEntitiesAsync(),
                DivisionsList = await GetDistinctDivisionsAsync(),
                SectionsList = await GetDistinctSectionsAsync(),
                GroupsList = await GetDistinctGroupsAsync(),

                UnionsList = await GetDistinctUnionsAsync(),
                FederationsList = await GetDistinctFederationsAsync(),
                AssociationsList = await GetDistinctAssociationsAsync(),
                NgosList = await GetDistinctNgosAsync(),

                JobGradesList = GetJobGrades(),
                StudyStagesList = GetStudyStages()
            };

            if (existingProfile != null)
            {
                viewModel.PersonalInfo.FullName = existingProfile.FullName;
                viewModel.PersonalInfo.LastName = existingProfile.LastName;
                viewModel.PersonalInfo.MotherName = existingProfile.MotherName;
                viewModel.PersonalInfo.DateOfBirth = existingProfile.Date;
                viewModel.PersonalInfo.Gender = existingProfile.Gender;
                viewModel.PersonalInfo.MaritalStatus = existingProfile.MaritalStatus;
                viewModel.PersonalInfo.Education = existingProfile.Education;
                viewModel.PersonalInfo.Specialization = existingProfile.Specialization;
                viewModel.PersonalInfo.PhoneNumber = existingProfile.PhoneNumber;
                viewModel.PersonalInfo.CoverImage = existingProfile.CoverImage;
                viewModel.PersonalInfo.UniversityType = existingProfile.UniversityType;
                viewModel.PersonalInfo.InstitutionType = existingProfile.InstitutionType;
                viewModel.PersonalInfo.InstitutionName = existingProfile.InstitutionName;
                viewModel.PersonalInfo.FacultyDepartment = existingProfile.FacultyDepartment;
                viewModel.PersonalInfo.StudyType = existingProfile.StudyType;
                viewModel.PersonalInfo.StudyStage = existingProfile.StudyStage;

                if (address != null)
                {
                    viewModel.Address.Governorate = address.Governorate;
                    viewModel.Address.District = address.District;
                    viewModel.Address.Area = address.Area;
                    viewModel.Address.Alley = address.Alley;
                    viewModel.Address.Street = address.Street;
                    viewModel.Address.House = address.House;
                    viewModel.Address.NearestPoint = address.NearestPoint;
                }

                viewModel.IdentityCardN = existingProfile.IdentityCardN;
                viewModel.IdentityDate = existingProfile.identityDate;

                if (voterCard != null)
                {
                    viewModel.Documents.VoterCardNumber = voterCard.VoterCardNumber;
                    viewModel.Documents.PollingCenterNumber = voterCard.PollingCenterNumber;
                }

                var existingWorkLocation = await _context.WorkLocations
                    .FirstOrDefaultAsync(w => w.IdentifyId == existingProfile.Id);

                viewModel.Employment.EmploymentStatus = existingProfile.EmploymentStatus;
                viewModel.Employment.Work = existingProfile.Work;
                viewModel.Employment.Ministry = existingProfile.Ministry;
                viewModel.Employment.Department = existingProfile.Department;
                viewModel.Employment.Position = existingProfile.Position;
                viewModel.Employment.JobTitle = existingProfile.JobTitle;
                viewModel.Employment.JobGrade = existingProfile.JobGrade;

                viewModel.WorkLocation.Governorate = existingWorkLocation?.Governorate ?? existingProfile.WorkGovernorate;
                viewModel.WorkLocation.District = existingWorkLocation?.District ?? existingProfile.WorkDistrict;

                if (affiliationInfo != null)
                {
                    if (affiliationInfo.AffiliationEntityId.HasValue)
                    {
                        var entity = await _context.AffiliationEntities
                            .FirstOrDefaultAsync(e => e.Id == affiliationInfo.AffiliationEntityId.Value);
                        viewModel.Affiliation.AffiliationEntity = entity?.Name;
                    }
                    if (affiliationInfo.DivisionId.HasValue)
                    {
                        var division = await _context.Divisions
                            .FirstOrDefaultAsync(d => d.Id == affiliationInfo.DivisionId.Value);
                        viewModel.Affiliation.Division = division?.Name;
                    }
                    if (affiliationInfo.SectionId.HasValue)
                    {
                        var section = await _context.Sections
                            .FirstOrDefaultAsync(s => s.Id == affiliationInfo.SectionId.Value);
                        viewModel.Affiliation.Section = section?.Name;
                    }
                    if (affiliationInfo.GroupId.HasValue)
                    {
                        var group = await _context.Groups
                            .FirstOrDefaultAsync(g => g.Id == affiliationInfo.GroupId.Value);
                        viewModel.Affiliation.Group = group?.Name;
                    }
                    viewModel.Affiliation.MozakeName = affiliationInfo.MozakeName;
                    viewModel.Affiliation.MozakePhoneNumber = affiliationInfo.MozakePhoneNumber;
                    viewModel.Affiliation.BadgeNumber = affiliationInfo.BadgeNumber;
                    viewModel.Affiliation.AffiliationDate = affiliationInfo.AffiliationDate;
                }

                if (union != null)
                {
                    viewModel.Memberships.UnionName = union.UnionName;
                    viewModel.Memberships.UnionPosition = union.Position;
                    viewModel.Memberships.UnionIdNumber = union.IdNumber;
                    viewModel.Memberships.UnionAffiliationDate = union.AffiliationDate;
                }

                if (federation != null)
                {
                    string federationFullName = "";

                    if (federation.Federation != null)
                        federationFullName = federation.Federation.Name;

                    if (federation.FederationDivision != null)
                        federationFullName += " - " + federation.FederationDivision.Name;

                    if (federation.FederationSection != null)
                        federationFullName += " - " + federation.FederationSection.Name;

                    if (federation.FederationGroup != null)
                        federationFullName += " - " + federation.FederationGroup.Name;

                    viewModel.Memberships.FederationName = federationFullName;
                    viewModel.Memberships.FederationPosition = federation.Position;
                    viewModel.Memberships.FederationIdNumber = federation.IdNumber;
                    viewModel.Memberships.FederationAffiliationDate = federation.AffiliationDate;
                }

                if (association != null)
                {
                    viewModel.Memberships.AssociationName = association.AssociationName;
                    viewModel.Memberships.AssociationPosition = association.Position;
                    viewModel.Memberships.AssociationIdNumber = association.IdNumber;
                    viewModel.Memberships.AssociationAffiliationDate = association.AffiliationDate;
                }

                if (ngo != null)
                {
                    viewModel.Memberships.NgoName = ngo.NgoName;
                    viewModel.Memberships.NgoPosition = ngo.Position;
                    viewModel.Memberships.NgoIdNumber = ngo.IdNumber;
                    viewModel.Memberships.NgoAffiliationDate = ngo.AffiliationDate;
                }

                viewModel.AccountType = existingProfile.AccountType;
                viewModel.IsPromoted = existingProfile.IsPromoted;
                viewModel.PromotionDate = existingProfile.PromotionDate;
                viewModel.PromotedBy = existingProfile.PromotedBy;
            }

            return View(viewModel);
        }

        // POST: /Register/CompleteProfile
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteProfile(CompleteProfileViewModel model)
        {
            _logger.LogInformation("=== 🚀 بدء حفظ الملف الشخصي الكامل ===");

            await LoadListsIntoViewModel(model);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("❌ النموذج غير صالح");
                return View(model);
            }

            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "لم يتم العثور على المستخدم";
                    return RedirectToAction("Login", "Account");
                }

                string? coverImagePath = null;
                if (model.CoverImageFile != null && model.CoverImageFile.Length > 0)
                {
                    coverImagePath = await SaveCoverImage(model.CoverImageFile);
                }

                var existingIdentify = await _context.Identifies
                    .FirstOrDefaultAsync(i => i.UserId == model.UserId);

                if (existingIdentify != null)
                {
                    await UpdateExistingProfile(existingIdentify, model, coverImagePath);
                }
                else
                {
                    await CreateNewProfile(user.Id, model, coverImagePath);
                }

                if (!string.IsNullOrEmpty(model.PersonalInfo.PhoneNumber) && user.PhoneNumber != model.PersonalInfo.PhoneNumber)
                {
                    user.PhoneNumber = model.PersonalInfo.PhoneNumber;
                    await _userManager.UpdateAsync(user);
                }

                await _context.SaveChangesAsync();

                var updatedProfile = await _context.Identifies
                    .FirstOrDefaultAsync(i => i.UserId == model.UserId);
                var updatedAddress = await GetUserAddressAsync(model.UserId);
                var updatedVoterCard = await GetUserVoterCardAsync(model.UserId);
                var updatedAffiliation = await GetUserAffiliationInfoAsync(model.UserId);

                if (updatedProfile != null && IsProfileComplete(updatedProfile, updatedAddress, updatedVoterCard, updatedAffiliation))
                {
                    updatedProfile.AccountType = "مكتمل";
                    updatedProfile.RequestedPromotion = true;
                    updatedProfile.RequestedPromotionDate = DateTime.Now;

                    _context.Identifies.Update(updatedProfile);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"✅ المستخدم {user.Email} أكمل ملفه وتم إرسال الطلب تلقائياً");
                    TempData["SuccessMessage"] = "✅ تم حفظ الملف الشخصي بنجاح! تم إرسال طلبك للإدارة للمراجعة.";
                }
                else
                {
                    TempData["SuccessMessage"] = "✅ تم حفظ الملف الشخصي بنجاح! يرجى إكمال جميع الحقول المطلوبة.";
                }

                return RedirectToAction("ProfileDetails");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ في حفظ الملف الشخصي");
                ViewBag.ErrorMessage = "حدث خطأ أثناء الحفظ: " + ex.Message;
                return View(model);
            }
        }

        // GET: /Register/EditProfile
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> EditProfile()
        {
            var userId = _userManager.GetUserId(User);
            _logger.LogInformation("📝 طلب تعديل الملف الشخصي: {UserId}", userId);

            if (string.IsNullOrEmpty(userId))
            {
                TempData["ErrorMessage"] = "يجب تسجيل الدخول أولاً.";
                return RedirectToAction("Login", "Account");
            }

            var user = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(user);

            var profile = await _context.Identifies
                .FirstOrDefaultAsync(i => i.UserId == userId);

            if (profile == null)
            {
                return RedirectToAction("CompleteProfile");
            }

            var viewModel = await MapToCompleteProfileViewModelAsync(profile, user, roles.FirstOrDefault());
            await LoadListsIntoViewModel(viewModel);

            return View(viewModel);
        }

        // POST: /Register/EditProfile
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(CompleteProfileViewModel model)
        {
            _logger.LogInformation("=== 🚀 بدء تعديل الملف الشخصي ===");

            await LoadListsIntoViewModel(model);

            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "لم يتم العثور على المستخدم";
                    return RedirectToAction("Login", "Account");
                }

                string? coverImagePath = null;
                if (model.CoverImageFile != null && model.CoverImageFile.Length > 0)
                {
                    coverImagePath = await SaveCoverImage(model.CoverImageFile);
                }

                var existingIdentify = await _context.Identifies
                    .FirstOrDefaultAsync(i => i.UserId == model.UserId);

                if (existingIdentify != null)
                {
                    await UpdateExistingProfile(existingIdentify, model, coverImagePath, true);
                    await _context.SaveChangesAsync();

                    var updatedAddress = await GetUserAddressAsync(model.UserId);
                    var updatedVoterCard = await GetUserVoterCardAsync(model.UserId);
                    var updatedAffiliation = await GetUserAffiliationInfoAsync(model.UserId);

                    if (existingIdentify != null && IsProfileComplete(existingIdentify, updatedAddress, updatedVoterCard, updatedAffiliation) && existingIdentify.AccountType == "عادي")
                    {
                        existingIdentify.AccountType = "مكتمل";
                        existingIdentify.RequestedPromotion = true;
                        existingIdentify.RequestedPromotionDate = DateTime.Now;
                        _context.Identifies.Update(existingIdentify);
                        await _context.SaveChangesAsync();
                    }

                    TempData["SuccessMessage"] = "✅ تم تحديث الملف الشخصي بنجاح!";
                    return RedirectToAction("ProfileDetails");
                }
                else
                {
                    return RedirectToAction("CompleteProfile");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ في تحديث الملف الشخصي");
                ViewBag.ErrorMessage = "حدث خطأ أثناء التحديث: " + ex.Message;
                return View(model);
            }
        }
        // GET: /Register/CheckEmail
        [HttpGet]
        [AllowAnonymous]
        public IActionResult CheckEmail(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAction("Index");
            }
            ViewBag.Email = email;
            return View();
        }

        #endregion

        #region ========== دوال المساعدة (Helper Methods) ==========

        private async Task LoadListsIntoViewModel(CompleteProfileViewModel model)
        {
            model.Governorates = GetGovernorates();
            model.Genders = GetGenders();
            model.Educations = GetEducations();
            model.Ministries = GetMinistries();
            model.EmploymentStatuses = GetEmploymentStatuses();
            model.JobGradesList = GetJobGrades();
            model.StudyStagesList = GetStudyStages();

            model.AffiliationEntities = await GetDistinctAffiliationEntitiesAsync();
            model.DivisionsList = await GetDistinctDivisionsAsync();
            model.SectionsList = await GetDistinctSectionsAsync();
            model.GroupsList = await GetDistinctGroupsAsync();

            model.UnionsList = await GetDistinctUnionsAsync();
            model.FederationsList = await GetDistinctFederationsAsync();
            model.AssociationsList = await GetDistinctAssociationsAsync();
            model.NgosList = await GetDistinctNgosAsync();
        }

        private async Task<List<string>> GetDistinctAffiliationEntitiesAsync()
        {
            var entities = await _context.AffiliationEntities.ToListAsync();
            return entities.Select(e => e.Name).OrderBy(x => x).ToList();
        }

        private async Task<List<string>> GetDistinctDivisionsAsync()
        {
            var divisions = await _context.Divisions.ToListAsync();
            return divisions.Select(d => d.Name).Distinct().OrderBy(x => x).ToList();
        }

        private async Task<List<string>> GetDistinctSectionsAsync()
        {
            var sections = await _context.Sections.ToListAsync();
            return sections.Select(s => s.Name).Distinct().OrderBy(x => x).ToList();
        }

        private async Task<List<string>> GetDistinctGroupsAsync()
        {
            var groups = await _context.Groups.ToListAsync();
            return groups.Select(g => g.Name).Distinct().OrderBy(x => x).ToList();
        }

        private async Task<List<string>> GetDistinctUnionsAsync()
        {
            var unions = await _context.Unions.ToListAsync();
            return unions.Select(x => x.Name).OrderBy(x => x).ToList();
        }

        private async Task<List<string>> GetDistinctFederationsAsync()
        {
            var federations = await _context.Federations.ToListAsync();
            return federations.Select(x => x.Name).OrderBy(x => x).ToList();
        }

        private async Task<List<string>> GetDistinctAssociationsAsync()
        {
            var associations = await _context.Associations.ToListAsync();
            return associations.Select(x => x.Name).OrderBy(x => x).ToList();
        }

        private async Task<List<string>> GetDistinctNgosAsync()
        {
            var ngos = await _context.Ngos.ToListAsync();
            return ngos.Select(x => x.Name).OrderBy(x => x).ToList();
        }
        private async Task LoadAdditionalInfoLists(AdditionalInfoViewModel model)
        {
            model.AffiliationEntities = await GetDistinctAffiliationEntitiesAsync();
            model.DivisionsList = await GetDistinctDivisionsAsync();
            model.SectionsList = await GetDistinctSectionsAsync();
            model.GroupsList = await GetDistinctGroupsAsync();

            model.UnionsList = await GetDistinctUnionsAsync();
            model.FederationsList = await GetDistinctFederationsAsync();
            model.AssociationsList = await GetDistinctAssociationsAsync();
            model.NgosList = await GetDistinctNgosAsync();
        }

        private async Task<string?> SaveCoverImage(IFormFile coverImageFile)
        {
            if (coverImageFile == null) return null;

            var uploadsFolder = _profileUploadPath;

            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(coverImageFile.FileName);
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await coverImageFile.CopyToAsync(fileStream);
            }

            return "/MyApp_Uploads/Profiles/" + uniqueFileName;
        }

        private async Task UpdateExistingProfile(Identify existingIdentify, CompleteProfileViewModel model, string? coverImagePath, bool isEdit = false)
        {
            existingIdentify.FullName = model.PersonalInfo.FullName ?? "";
            existingIdentify.LastName = model.PersonalInfo.LastName ?? "";
            existingIdentify.MotherName = model.PersonalInfo.MotherName ?? "";
            existingIdentify.Date = model.PersonalInfo.DateOfBirth;
            existingIdentify.Gender = model.PersonalInfo.Gender ?? "";
            existingIdentify.Education = model.PersonalInfo.Education ?? "";
            existingIdentify.Specialization = model.PersonalInfo.Specialization ?? "";
            existingIdentify.PhoneNumber = model.PersonalInfo.PhoneNumber ?? "";
            existingIdentify.MaritalStatus = model.PersonalInfo.MaritalStatus;
            existingIdentify.UniversityType = model.PersonalInfo.UniversityType;
            existingIdentify.InstitutionType = model.PersonalInfo.InstitutionType;
            existingIdentify.InstitutionName = model.PersonalInfo.InstitutionName;
            existingIdentify.FacultyDepartment = model.PersonalInfo.FacultyDepartment;
            existingIdentify.StudyType = model.PersonalInfo.StudyType;
            existingIdentify.StudyStage = model.PersonalInfo.StudyStage;

            existingIdentify.IdentityCardN = model.IdentityCardN;
            existingIdentify.identityDate = model.IdentityDate;

            NormalizeWorkLocation(model.WorkLocation);
            existingIdentify.WorkGovernorate = model.WorkLocation.Governorate;
            existingIdentify.WorkDistrict = null;

            existingIdentify.EmploymentStatus = model.Employment.EmploymentStatus;
            existingIdentify.Work = model.Employment.Work;
            existingIdentify.Ministry = model.Employment.Ministry;
            existingIdentify.Department = model.Employment.Department;
            existingIdentify.Position = model.Employment.Position;
            existingIdentify.JobTitle = model.Employment.JobTitle;
            existingIdentify.JobGrade = model.Employment.JobGrade;

            if (coverImagePath != null)
            {
                if (!string.IsNullOrEmpty(existingIdentify.CoverImage))
                {
                    var oldImagePath = Path.Combine(_webHostEnvironment.WebRootPath,
                        existingIdentify.CoverImage.TrimStart('/'));
                    if (System.IO.File.Exists(oldImagePath))
                    {
                        System.IO.File.Delete(oldImagePath);
                    }
                }
                existingIdentify.CoverImage = coverImagePath;
            }

            _context.Identifies.Update(existingIdentify);
            await _context.SaveChangesAsync();

            await UpdateOrCreateWorkLocation(existingIdentify.Id, model.WorkLocation);
            await UpdateOrCreateAddress(model.UserId, model.Address);
            await UpdateOrCreateVoterCard(model.UserId, model.Documents);
            await UpdateOrCreateAffiliationInfo(model.UserId, model.Affiliation);
            await UpdateOrCreateUnion(model.UserId, model.Memberships);
            await UpdateOrCreateFederation(model.UserId, model.Memberships);
            await UpdateOrCreateAssociation(model.UserId, model.Memberships);
            await UpdateOrCreateNgo(model.UserId, model.Memberships);
        }

        private bool CanUserAccessBasicInfo(Identify profile)
        {
            if (profile == null) return true;

            if (profile.IsBasicInfoApproved)
                return false;

            if (profile.RequestedPromotion == true)
                return false;

            if (profile.IsPromoted)
                return false;

            return true;
        }

        private async Task CreateNewProfile(string userId, CompleteProfileViewModel model, string? coverImagePath)
        {
            await UpdateOrCreateAddress(userId, model.Address);
            await UpdateOrCreateVoterCard(userId, model.Documents);
            await UpdateOrCreateAffiliationInfo(userId, model.Affiliation);
            await UpdateOrCreateUnion(userId, model.Memberships);
            await UpdateOrCreateFederation(userId, model.Memberships);
            await UpdateOrCreateAssociation(userId, model.Memberships);
            await UpdateOrCreateNgo(userId, model.Memberships);

            var identify = new Identify
            {
                FullName = model.PersonalInfo.FullName ?? "",
                LastName = model.PersonalInfo.LastName ?? "",
                MotherName = model.PersonalInfo.MotherName ?? "",
                Date = model.PersonalInfo.DateOfBirth,
                Gender = model.PersonalInfo.Gender ?? "",
                Education = model.PersonalInfo.Education ?? "",
                Specialization = model.PersonalInfo.Specialization ?? "",
                PhoneNumber = model.PersonalInfo.PhoneNumber ?? "",
                MaritalStatus = model.PersonalInfo.MaritalStatus,
                UniversityType = model.PersonalInfo.UniversityType,
                InstitutionType = model.PersonalInfo.InstitutionType,
                InstitutionName = model.PersonalInfo.InstitutionName,
                FacultyDepartment = model.PersonalInfo.FacultyDepartment,
                StudyType = model.PersonalInfo.StudyType,
                StudyStage = model.PersonalInfo.StudyStage,
                IdentityCardN = model.IdentityCardN,
                identityDate = model.IdentityDate,
                WorkGovernorate = model.WorkLocation.Governorate,
                WorkDistrict = null,
                EmploymentStatus = model.Employment.EmploymentStatus,
                Work = model.Employment.Work,
                Ministry = model.Employment.Ministry,
                Department = model.Employment.Department,
                Position = model.Employment.Position,
                JobTitle = model.Employment.JobTitle,
                JobGrade = model.Employment.JobGrade,
                UserId = userId,
                AccountType = "عادي",
                IsPromoted = false,
                CreatedAt = DateTime.UtcNow,
                CoverImage = coverImagePath,
                IsBasicInfoApproved = false
            };

            _context.Identifies.Add(identify);
            await _context.SaveChangesAsync();
            await UpdateOrCreateWorkLocation(identify.Id, model.WorkLocation);
        }

        private async Task UpdateOrCreateWorkLocation(int identifyId, WorkLocationViewModel workLocation)
        {
            NormalizeWorkLocation(workLocation);
            var existingWorkLocation = await _context.WorkLocations
                .FirstOrDefaultAsync(w => w.IdentifyId == identifyId);

            var governorate = workLocation.Governorate ?? string.Empty;
            var district = governorate == "بغداد" ? workLocation.District : null;

            if (string.IsNullOrWhiteSpace(governorate))
            {
                if (existingWorkLocation != null)
                {
                    _context.WorkLocations.Remove(existingWorkLocation);
                    await _context.SaveChangesAsync();
                }

                return;
            }

            if (existingWorkLocation == null)
            {
                existingWorkLocation = new WorkLocation
                {
                    IdentifyId = identifyId
                };

                _context.WorkLocations.Add(existingWorkLocation);
            }

            existingWorkLocation.Governorate = governorate;
            existingWorkLocation.District = district;
            await _context.SaveChangesAsync();
        }

        private static void NormalizeWorkLocation(WorkLocationViewModel workLocation)
        {
            if (string.IsNullOrWhiteSpace(workLocation.Governorate))
                return;

            workLocation.Governorate = workLocation.Governorate.Trim();
            workLocation.District = null;
        }

        private async Task<CompleteProfileViewModel> MapToCompleteProfileViewModelAsync(Identify profile, IdentityUser user, string? userRole)
        {
            var address = await GetUserAddressAsync(user.Id);
            var voterCard = await GetUserVoterCardAsync(user.Id);
            var workLocation = await _context.WorkLocations.FirstOrDefaultAsync(w => w.IdentifyId == profile.Id);
            var union = await GetUserUnionAsync(user.Id);
            var federation = await GetUserFederationAsync(user.Id);
            var association = await GetUserAssociationAsync(user.Id);
            var ngo = await GetUserNgoAsync(user.Id);
            var affiliationInfo = await GetUserAffiliationInfoAsync(user.Id);

            var viewModel = new CompleteProfileViewModel
            {
                UserId = user.Id,
                Email = user.Email,
                UserRole = userRole ?? "User",
                IsEmailConfirmed = user.EmailConfirmed,
                WhatsAppNumber = profile.WhatsAppNumber,
                IsWhatsAppVerified = profile.IsWhatsAppVerified,
                WhatsAppVerifiedAt = profile.WhatsAppVerifiedAt,
                CreatedAt = profile.CreatedAt,
                AccountType = profile.AccountType,
                IsPromoted = profile.IsPromoted,
                PromotionDate = profile.PromotionDate,
                PromotedBy = profile.PromotedBy
            };

            viewModel.PersonalInfo.FullName = profile.FullName;
            viewModel.PersonalInfo.LastName = profile.LastName;
            viewModel.PersonalInfo.MotherName = profile.MotherName;
            viewModel.PersonalInfo.DateOfBirth = profile.Date;
            viewModel.PersonalInfo.Gender = profile.Gender;
            viewModel.PersonalInfo.MaritalStatus = profile.MaritalStatus;
            viewModel.PersonalInfo.Education = profile.Education;
            viewModel.PersonalInfo.Specialization = profile.Specialization;
            viewModel.PersonalInfo.PhoneNumber = profile.PhoneNumber;
            viewModel.PersonalInfo.CoverImage = profile.CoverImage;
            viewModel.PersonalInfo.UniversityType = profile.UniversityType;
            viewModel.PersonalInfo.InstitutionType = profile.InstitutionType;
            viewModel.PersonalInfo.InstitutionName = profile.InstitutionName;
            viewModel.PersonalInfo.FacultyDepartment = profile.FacultyDepartment;
            viewModel.PersonalInfo.StudyType = profile.StudyType;
            viewModel.PersonalInfo.StudyStage = profile.StudyStage;

            if (address != null)
            {
                viewModel.Address.Governorate = address.Governorate;
                viewModel.Address.District = address.District;
                viewModel.Address.Area = address.Area;
                viewModel.Address.Alley = address.Alley;
                viewModel.Address.Street = address.Street;
                viewModel.Address.House = address.House;
                viewModel.Address.NearestPoint = address.NearestPoint;
            }

            viewModel.IdentityCardN = profile.IdentityCardN;
            viewModel.IdentityDate = profile.identityDate;
            viewModel.WorkLocation.Governorate = workLocation?.Governorate ?? profile.WorkGovernorate;
            viewModel.WorkLocation.District = workLocation?.District ?? profile.WorkDistrict;

            if (voterCard != null)
            {
                viewModel.Documents.VoterCardNumber = voterCard.VoterCardNumber;
                viewModel.Documents.PollingCenterNumber = voterCard.PollingCenterNumber;
            }

            viewModel.Employment.EmploymentStatus = profile.EmploymentStatus;
            viewModel.Employment.Work = profile.Work;
            viewModel.Employment.Ministry = profile.Ministry;
            viewModel.Employment.Department = profile.Department;
            viewModel.Employment.Position = profile.Position;
            viewModel.Employment.JobTitle = profile.JobTitle;
            viewModel.Employment.JobGrade = profile.JobGrade;

            if (affiliationInfo != null)
            {
                if (affiliationInfo.AffiliationEntityId.HasValue)
                {
                    var entity = await _context.AffiliationEntities
                        .FirstOrDefaultAsync(e => e.Id == affiliationInfo.AffiliationEntityId.Value);
                    viewModel.Affiliation.AffiliationEntity = entity?.Name;
                }
                if (affiliationInfo.DivisionId.HasValue)
                {
                    var division = await _context.Divisions
                        .FirstOrDefaultAsync(d => d.Id == affiliationInfo.DivisionId.Value);
                    viewModel.Affiliation.Division = division?.Name;
                }
                if (affiliationInfo.SectionId.HasValue)
                {
                    var section = await _context.Sections
                        .FirstOrDefaultAsync(s => s.Id == affiliationInfo.SectionId.Value);
                    viewModel.Affiliation.Section = section?.Name;
                }
                if (affiliationInfo.GroupId.HasValue)
                {
                    var group = await _context.Groups
                        .FirstOrDefaultAsync(g => g.Id == affiliationInfo.GroupId.Value);
                    viewModel.Affiliation.Group = group?.Name;
                }
                viewModel.Affiliation.MozakeName = affiliationInfo.MozakeName;
                viewModel.Affiliation.MozakePhoneNumber = affiliationInfo.MozakePhoneNumber;
                viewModel.Affiliation.BadgeNumber = affiliationInfo.BadgeNumber;
                viewModel.Affiliation.AffiliationDate = affiliationInfo.AffiliationDate;
            }

            if (union != null)
            {
                viewModel.Memberships.UnionName = union.UnionName;
                viewModel.Memberships.UnionPosition = union.Position;
                viewModel.Memberships.UnionIdNumber = union.IdNumber;
                viewModel.Memberships.UnionAffiliationDate = union.AffiliationDate;
            }

            if (federation != null)
            {
                string federationFullName = "";

                if (federation.Federation != null)
                    federationFullName = federation.Federation.Name;

                if (federation.FederationDivision != null)
                    federationFullName += " - " + federation.FederationDivision.Name;

                if (federation.FederationSection != null)
                    federationFullName += " - " + federation.FederationSection.Name;

                if (federation.FederationGroup != null)
                    federationFullName += " - " + federation.FederationGroup.Name;

                viewModel.Memberships.FederationName = federationFullName;
                viewModel.Memberships.FederationPosition = federation.Position;
                viewModel.Memberships.FederationIdNumber = federation.IdNumber;
                viewModel.Memberships.FederationAffiliationDate = federation.AffiliationDate;
            }

            if (association != null)
            {
                viewModel.Memberships.AssociationName = association.AssociationName;
                viewModel.Memberships.AssociationPosition = association.Position;
                viewModel.Memberships.AssociationIdNumber = association.IdNumber;
                viewModel.Memberships.AssociationAffiliationDate = association.AffiliationDate;
            }

            if (ngo != null)
            {
                viewModel.Memberships.NgoName = ngo.NgoName;
                viewModel.Memberships.NgoPosition = ngo.Position;
                viewModel.Memberships.NgoIdNumber = ngo.IdNumber;
                viewModel.Memberships.NgoAffiliationDate = ngo.AffiliationDate;
            }

            return viewModel;
        }

        private bool HasRequiredWorkLocation(Identify profile)
        {
            if (!string.IsNullOrWhiteSpace(profile.WorkGovernorate))
            {
                return true;
            }

            var workLocation = _context.WorkLocations.AsNoTracking().FirstOrDefault(w => w.IdentifyId == profile.Id);
            if (workLocation == null || string.IsNullOrWhiteSpace(workLocation.Governorate))
                return false;

            return workLocation.Governorate != "بغداد" || !string.IsNullOrWhiteSpace(workLocation.District);
        }

        private bool IsBasicInfoComplete(Identify profile)
        {
            if (profile == null) return false;

            if (string.IsNullOrWhiteSpace(profile.FullName)) return false;
            if (string.IsNullOrWhiteSpace(profile.MotherName)) return false;
            if (profile.Date == DateTime.MinValue) return false;
            if (string.IsNullOrWhiteSpace(profile.Gender)) return false;
            if (string.IsNullOrWhiteSpace(profile.PhoneNumber)) return false;
            if (string.IsNullOrWhiteSpace(profile.IdentityCardN)) return false;
            if (profile.IdentityCardN.Length != 12) return false;
            if (!HasRequiredWorkLocation(profile)) return false;

            return true;
        }

        private bool IsProfileComplete(Identify profile, Address? address, VoterCard? voterCard, AffiliationInfo? affiliationInfo)
        {
            if (profile == null) return false;

            if (string.IsNullOrWhiteSpace(profile.FullName)) return false;
            if (string.IsNullOrWhiteSpace(profile.MotherName)) return false;
            if (profile.Date == DateTime.MinValue) return false;
            if (string.IsNullOrWhiteSpace(profile.Gender)) return false;
            if (string.IsNullOrWhiteSpace(profile.PhoneNumber)) return false;
            if (string.IsNullOrWhiteSpace(profile.IdentityCardN)) return false;
            if (profile.IdentityCardN.Length != 12) return false;
            if (!HasRequiredWorkLocation(profile)) return false;

            return true;
        }

        #endregion

        #region ========== قوائم البيانات الثابتة ==========

        private List<string> GetGovernorates()
        {
            return new List<string>
            {
                "بغداد", "الأنبار", "بابل", "البصرة", "ذي قار", "القادسية",
                "ديالى", "دهوك", "أربيل", "كربلاء", "كركوك", "ميسان",
                "المثنى", "النجف", "نينوى", "صلاح الدين", "السليمانية", "واسط"
            };
        }

        private List<string> GetGenders()
        {
            return new List<string> { "ذكر", "أنثى" };
        }

        private List<string> GetEducations()
        {
            return new List<string>
            {
                "آمي",
                "ابتدائي",
                "متوسط",
                "إعدادي",
                "معهد",
                "طالب جامعي",
                "دبلوم",
                "بكالوريوس",
                "ماجستير",
                "دكتوراه"
            };
        }

        private List<string> GetMinistries()
        {
            return IraqiGovernmentEntities.GetMinistries();
        }

        private List<string> GetEmploymentStatuses()
        {
            return new List<string>
            {
                "موظف", "كاسب", "متقاعد", "طالب", "قطاع خاص"
            };
        }

        private List<string> GetJobGrades()
        {
            return new List<string>
    {
        "الدرجة العاشرة",
        "الدرجة التاسعة",
        "الدرجة الثامنة",
        "الدرجة السابعة",
        "الدرجة السادسة",
        "الدرجة الخامسة",
        "الدرجة الرابعة",
        "الدرجة الثالثة",
        "الدرجة الثانية",
        "الدرجة الأولى"
    };
        }

        private List<string> GetStudyStages()
        {
            return new List<string>
            {
               
                "المرحلة الأولى",
                "المرحلة الثانية",
                "المرحلة الثالثة",
                "المرحلة الرابعة",
                "المرحلة الخامسة",
                "المرحلة السادسة",

            };
        }
        private async Task SendConfirmationEmailAsync(IdentityUser user)
        {
            try
            {
                var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);

                // ✅ أضف هذا السطر - تشفير الكود بنفس الطريقة
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

                var callbackUrl = Url.Page(
                    "/Account/ConfirmEmail",
                    pageHandler: null,
                    values: new { area = "Identity", userId = user.Id, code = code },
                    protocol: Request.Scheme);

                var subject = "تأكيد بريدك الإلكتروني";
                var message = $@"
            <div style='direction: rtl; font-family: Arial, sans-serif;'>
                <h2>مرحباً {user.Email}،</h2>
                <p>شكراً لتسجيلك في منصتنا. يرجى تأكيد بريدك الإلكتروني من خلال الضغط على الرابط التالي:</p>
                <p><a href='{callbackUrl}' style='background-color: #4CAF50; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px;'>تأكيد البريد الإلكتروني</a></p>
                <p>أو قم بنسخ الرابط التالي:</p>
                <p>{callbackUrl}</p>
                <hr />
                <p>مع تحيات, فريق الدعم</p>
            </div>";

                await _emailSender.SendEmailAsync(user.Email, subject, message);
                _logger.LogInformation($"✅ تم إرسال بريد التأكيد إلى {user.Email}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ خطأ في إرسال بريد التأكيد إلى {user.Email}");
            }
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


        #endregion
    }
}
