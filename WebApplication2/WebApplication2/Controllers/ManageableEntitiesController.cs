using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;

namespace WebApplication2.Controllers
{
    [Authorize(Roles = clsRoles.SuperAdmin)]
    public class ManageableEntitiesController : Controller
    {
        private readonly ILogger<ManageableEntitiesController> _logger;
        private readonly ApplicationDbContext _context;

        public ManageableEntitiesController(
            ApplicationDbContext context,
            ILogger<ManageableEntitiesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ===== دوال مساعدة للتحقق من استخدام الكيانات (جهة الانتساب) =====
        private async Task<bool> IsAffiliationEntityInUse(int id)
        {
            return await _context.AffiliationInfos.AnyAsync(x => x.AffiliationEntityId == id && x.UserId != null);
        }

        private async Task<bool> IsDivisionInUse(int id)
        {
            return await _context.AffiliationInfos.AnyAsync(x => x.DivisionId == id && x.UserId != null);
        }

        private async Task<bool> IsSectionInUse(int id)
        {
            return await _context.AffiliationInfos.AnyAsync(x => x.SectionId == id && x.UserId != null);
        }

        private async Task<bool> IsGroupInUse(int id)
        {
            return await _context.AffiliationInfos.AnyAsync(x => x.GroupId == id && x.UserId != null);
        }

        // ===== دوال مساعدة للتحقق من استخدام الكيانات (الاتحاد) =====
        private async Task<bool> IsFederationDivisionInUse(int id)
        {
            return await _context.FederationMemberships.AnyAsync(f => f.FederationDivisionId == id && f.UserId != null);
        }

        private async Task<bool> IsFederationSectionInUse(int id)
        {
            return await _context.FederationMemberships.AnyAsync(f => f.FederationSectionId == id && f.UserId != null);
        }

        private async Task<bool> IsFederationGroupInUse(int id)
        {
            return await _context.FederationMemberships.AnyAsync(f => f.FederationGroupId == id && f.UserId != null);
        }

        private async Task<bool> IsFederationInUse(string federationName)
        {
            return await _context.FederationMemberships
                .Include(f => f.Federation)
                .AnyAsync(f => f.Federation != null && f.Federation.Name == federationName);
        }

        private async Task<bool> IsUnionInUse(string unionName)
        {
            return await _context.UnionMemberships.AnyAsync(u => u.UnionName == unionName);
        }

        private async Task<bool> IsAssociationInUse(string associationName)
        {
            return await _context.AssociationMemberships.AnyAsync(a => a.AssociationName == associationName);
        }

        private async Task<bool> IsNgoInUse(string name)
        {
            return await _context.NgoMemberships.AnyAsync(n => n.NgoName == name);
        }

        #region ========== Dashboard ==========

        // GET: ManageableEntities/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var allAffiliationEntities = await _context.AffiliationEntities.ToListAsync();
            var allUnions = await _context.Unions.ToListAsync();
            var allFederations = await _context.Federations.ToListAsync();
            var allAssociations = await _context.Associations.ToListAsync();
            var allNgos = await _context.Ngos.ToListAsync();

            ViewBag.AffiliationEntitiesCount = allAffiliationEntities.Count();
            ViewBag.UnionsCount = allUnions.Count();
            ViewBag.FederationsCount = allFederations.Count();
            ViewBag.AssociationsCount = allAssociations.Count();
            ViewBag.NgosCount = allNgos.Count();

            return View();
        }

        #endregion

        #region ========== جهات الانتساب (AffiliationEntity) ==========

        // GET: ManageableEntities/AffiliationEntities
        public async Task<IActionResult> AffiliationEntities()
        {
            try
            {
                var entities = await _context.AffiliationEntities
                    .Include(x => x.Divisions)
                    .ThenInclude(d => d.Sections)
                    .ThenInclude(s => s.Groups)
                    .OrderBy(x => x.Name)
                    .ToListAsync();
                return View(entities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في جلب جهات الانتساب");
                TempData["ErrorMessage"] = "حدث خطأ في تحميل البيانات";
                return View(new List<AffiliationEntity>());
            }
        }

        // GET: ManageableEntities/CreateAffiliationEntity
        public IActionResult CreateAffiliationEntity()
        {
            return View();
        }

        // POST: ManageableEntities/CreateAffiliationEntity
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAffiliationEntity(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                ModelState.AddModelError("", "جهة الانتساب مطلوبة");
                return View();
            }

            try
            {
                var allEntities = await _context.AffiliationEntities.ToListAsync();
                var exists = allEntities.Any(x => x.Name == name);

                if (!exists)
                {
                    var entity = new AffiliationEntity
                    {
                        Name = name,
                        CreatedAt = DateTime.Now
                    };

                    _context.AffiliationEntities.Add(entity);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"تم إضافة جهة انتساب جديدة: {name}");
                }
                else
                {
                    TempData["ErrorMessage"] = "هذه الجهة موجودة مسبقاً";
                    return RedirectToAction(nameof(CreateAffiliationEntity));
                }

                TempData["SuccessMessage"] = "✅ تم إضافة جهة الانتساب بنجاح";
                return RedirectToAction(nameof(AffiliationEntities));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في إضافة جهة انتساب");
                ModelState.AddModelError("", "حدث خطأ أثناء الحفظ: " + ex.Message);
                return View();
            }
        }

        // POST: ManageableEntities/DeleteAffiliationEntity
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAffiliationEntity(int id)
        {
            try
            {
                var entity = await _context.AffiliationEntities.FindAsync(id);
                if (entity == null)
                {
                    return Json(new { success = false, message = "العنصر غير موجود" });
                }

                var isUsedByUsers = await IsAffiliationEntityInUse(id);
                if (isUsedByUsers)
                {
                    return Json(new { success = false, message = "لا يمكن حذف جهة الانتساب لأنها مستخدمة من قبل أعضاء" });
                }

                _context.AffiliationEntities.Remove(entity);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "✅ تم الحذف بنجاح" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"خطأ في حذف جهة انتساب");
                return Json(new { success = false, message = "❌ حدث خطأ: " + ex.Message });
            }
        }

        // GET: ManageableEntities/AffiliationEntityDetails/{id}
        public async Task<IActionResult> AffiliationEntityDetails(int id)
        {
            try
            {
                var entity = await _context.AffiliationEntities
                    .Include(x => x.Divisions)
                    .ThenInclude(d => d.Sections)
                    .ThenInclude(s => s.Groups)
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (entity == null)
                {
                    TempData["ErrorMessage"] = "جهة الانتساب غير موجودة";
                    return RedirectToAction(nameof(AffiliationEntities));
                }

                var divisions = await _context.Divisions
                    .Where(d => d.AffiliationEntityId == id)
                    .OrderBy(d => d.Name)
                    .ToListAsync();

                ViewBag.Divisions = divisions;

                return View(entity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في عرض تفاصيل جهة الانتساب");
                TempData["ErrorMessage"] = "حدث خطأ في تحميل البيانات";
                return RedirectToAction(nameof(AffiliationEntities));
            }
        }

        // GET: ManageableEntities/CreateDivision (لجهة الانتساب)
        public async Task<IActionResult> CreateDivision(int? entityId)
        {
            var entities = await _context.AffiliationEntities.ToListAsync();
            ViewBag.AffiliationEntities = entities;
            ViewBag.EntityId = entityId;
            return View();
        }

        // POST: ManageableEntities/CreateDivision
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDivision(int affiliationEntityId, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                ModelState.AddModelError("", "اسم القسم مطلوب");
                var entities = await _context.AffiliationEntities.ToListAsync();
                ViewBag.AffiliationEntities = entities;
                return View();
            }

            try
            {
                var entity = await _context.AffiliationEntities.FindAsync(affiliationEntityId);
                if (entity == null)
                {
                    TempData["ErrorMessage"] = "جهة الانتساب غير موجودة";
                    return RedirectToAction(nameof(AffiliationEntities));
                }

                var existingDivisions = await _context.Divisions
                    .Where(d => d.AffiliationEntityId == affiliationEntityId)
                    .ToListAsync();
                var exists = existingDivisions.Any(x => x.Name == name);

                if (exists)
                {
                    TempData["ErrorMessage"] = "هذا القسم موجود مسبقاً";
                    return RedirectToAction(nameof(AffiliationEntityDetails), new { id = affiliationEntityId });
                }

                var division = new Division
                {
                    Name = name,
                    AffiliationEntityId = affiliationEntityId,
                    CreatedAt = DateTime.Now
                };

                _context.Divisions.Add(division);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"✅ تم إضافة القسم '{name}' بنجاح";
                return RedirectToAction(nameof(AffiliationEntityDetails), new { id = affiliationEntityId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في إضافة قسم");
                ModelState.AddModelError("", "حدث خطأ أثناء الحفظ: " + ex.Message);
                var entities = await _context.AffiliationEntities.ToListAsync();
                ViewBag.AffiliationEntities = entities;
                return View();
            }
        }

        // POST: ManageableEntities/DeleteDivision
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDivision(int id)
        {
            try
            {
                var division = await _context.Divisions.FindAsync(id);
                if (division == null)
                {
                    return Json(new { success = false, message = "العنصر غير موجود" });
                }

                var isUsedByUsers = await IsDivisionInUse(id);
                if (isUsedByUsers)
                {
                    return Json(new { success = false, message = "لا يمكن حذف القسم لأنه مستخدم من قبل أعضاء" });
                }

                var entityId = division.AffiliationEntityId;
                _context.Divisions.Remove(division);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "✅ تم الحذف بنجاح", redirectUrl = Url.Action(nameof(AffiliationEntityDetails), new { id = entityId }) });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "❌ حدث خطأ: " + ex.Message });
            }
        }

        // GET: ManageableEntities/DivisionDetails
        public async Task<IActionResult> DivisionDetails(int id)
        {
            try
            {
                var division = await _context.Divisions
                    .Include(x => x.Sections)
                    .ThenInclude(s => s.Groups)
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (division == null)
                {
                    TempData["ErrorMessage"] = "القسم غير موجود";
                    return RedirectToAction(nameof(AffiliationEntities));
                }

                var sections = await _context.Sections
                    .Where(s => s.DivisionId == id)
                    .OrderBy(s => s.Name)
                    .ToListAsync();

                ViewBag.Sections = sections;

                return View(division);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في عرض تفاصيل القسم");
                TempData["ErrorMessage"] = "حدث خطأ في تحميل البيانات";
                return RedirectToAction(nameof(AffiliationEntities));
            }
        }

        // GET: ManageableEntities/CreateSection (لجهة الانتساب)
        public async Task<IActionResult> CreateSection(int? divisionId)
        {
            if (divisionId.HasValue)
            {
                var division = await _context.Divisions.FindAsync(divisionId.Value);
                ViewBag.CurrentDivision = division;
            }
            ViewBag.DivisionId = divisionId;
            return View();
        }

        // POST: ManageableEntities/CreateSection
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSection(int divisionId, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                ModelState.AddModelError("", "اسم الشعبة مطلوب");
                return View();
            }

            try
            {
                var division = await _context.Divisions.FindAsync(divisionId);
                if (division == null)
                {
                    TempData["ErrorMessage"] = "القسم غير موجود";
                    return RedirectToAction(nameof(AffiliationEntities));
                }

                var existingSections = await _context.Sections
                    .Where(s => s.DivisionId == divisionId)
                    .ToListAsync();
                var exists = existingSections.Any(x => x.Name == name);

                if (exists)
                {
                    TempData["ErrorMessage"] = "هذه الشعبة موجودة مسبقاً";
                    return RedirectToAction(nameof(DivisionDetails), new { id = divisionId });
                }

                var section = new Section
                {
                    Name = name,
                    DivisionId = divisionId,
                    CreatedAt = DateTime.Now
                };

                _context.Sections.Add(section);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"✅ تم إضافة الشعبة '{name}' بنجاح";
                return RedirectToAction(nameof(DivisionDetails), new { id = divisionId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في إضافة شعبة");
                ModelState.AddModelError("", "حدث خطأ أثناء الحفظ: " + ex.Message);
                return View();
            }
        }

        // POST: ManageableEntities/DeleteSection
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSection(int id)
        {
            try
            {
                var section = await _context.Sections.FindAsync(id);
                if (section == null)
                {
                    return Json(new { success = false, message = "العنصر غير موجود" });
                }

                var isUsedByUsers = await IsSectionInUse(id);
                if (isUsedByUsers)
                {
                    return Json(new { success = false, message = "لا يمكن حذف الشعبة لأنها مستخدمة من قبل أعضاء" });
                }

                var divisionId = section.DivisionId;
                _context.Sections.Remove(section);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "✅ تم الحذف بنجاح", redirectUrl = Url.Action(nameof(DivisionDetails), new { id = divisionId }) });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "❌ حدث خطأ: " + ex.Message });
            }
        }

        // GET: ManageableEntities/SectionDetails
        public async Task<IActionResult> SectionDetails(int id)
        {
            try
            {
                var section = await _context.Sections
                    .Include(x => x.Groups)
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (section == null)
                {
                    TempData["ErrorMessage"] = "الشعبة غير موجودة";
                    return RedirectToAction(nameof(AffiliationEntities));
                }

                var groups = await _context.Groups
                    .Where(g => g.SectionId == id)
                    .OrderBy(g => g.Name)
                    .ToListAsync();

                ViewBag.Groups = groups;

                return View(section);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في عرض تفاصيل الشعبة");
                TempData["ErrorMessage"] = "حدث خطأ في تحميل البيانات";
                return RedirectToAction(nameof(AffiliationEntities));
            }
        }

        // GET: ManageableEntities/CreateGroup (لجهة الانتساب)
        public async Task<IActionResult> CreateGroup(int? sectionId)
        {
            if (sectionId.HasValue)
            {
                var section = await _context.Sections.FindAsync(sectionId.Value);
                ViewBag.CurrentSection = section;
            }
            ViewBag.SectionId = sectionId;
            return View();
        }

        // POST: ManageableEntities/CreateGroup
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateGroup(int sectionId, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                ModelState.AddModelError("", "اسم الوحدة مطلوب");
                return View();
            }

            try
            {
                var section = await _context.Sections.FindAsync(sectionId);
                if (section == null)
                {
                    TempData["ErrorMessage"] = "الشعبة غير موجودة";
                    return RedirectToAction(nameof(AffiliationEntities));
                }

                var existingGroups = await _context.Groups
                    .Where(g => g.SectionId == sectionId)
                    .ToListAsync();
                var exists = existingGroups.Any(x => x.Name == name);

                if (exists)
                {
                    TempData["ErrorMessage"] = "هذه الوحدة موجودة مسبقاً";
                    return RedirectToAction(nameof(SectionDetails), new { id = sectionId });
                }

                var group = new Group
                {
                    Name = name,
                    SectionId = sectionId,
                    CreatedAt = DateTime.Now
                };

                _context.Groups.Add(group);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"✅ تم إضافة الوحدة '{name}' بنجاح";
                return RedirectToAction(nameof(SectionDetails), new { id = sectionId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في إضافة تجمع");
                ModelState.AddModelError("", "حدث خطأ أثناء الحفظ: " + ex.Message);
                return View();
            }
        }

        // POST: ManageableEntities/DeleteGroup
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteGroup(int id)
        {
            try
            {
                var group = await _context.Groups.FindAsync(id);
                if (group == null)
                {
                    return Json(new { success = false, message = "العنصر غير موجود" });
                }

                var isUsedByUsers = await IsGroupInUse(id);
                if (isUsedByUsers)
                {
                    return Json(new { success = false, message = "لا يمكن حذف الوحدة لأنها مستخدمة من قبل أعضاء" });
                }

                var sectionId = group.SectionId;
                _context.Groups.Remove(group);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "✅ تم الحذف بنجاح", redirectUrl = Url.Action(nameof(SectionDetails), new { id = sectionId }) });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "❌ حدث خطأ: " + ex.Message });
            }
        }

        #endregion

        #region ========== الاتحادات (Federation) ==========

        // GET: ManageableEntities/Federations
        public async Task<IActionResult> Federations()
        {
            try
            {
                var federations = await _context.Federations
                    .Include(x => x.Divisions)
                    .ThenInclude(d => d.Sections)
                    .ThenInclude(s => s.Groups)
                    .OrderBy(x => x.Name)
                    .ToListAsync();
                return View(federations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في جلب الاتحادات");
                TempData["ErrorMessage"] = "حدث خطأ في تحميل البيانات";
                return View(new List<Federation>());
            }
        }

        // GET: ManageableEntities/CreateFederation
        public IActionResult CreateFederation()
        {
            return View();
        }

        // POST: ManageableEntities/CreateFederation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFederation(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                ModelState.AddModelError("", "اسم الاتحاد مطلوب");
                return View();
            }

            try
            {
                var exists = await _context.Federations.AnyAsync(f => f.Name == name);

                if (!exists)
                {
                    var federation = new Federation
                    {
                        Name = name,
                        CreatedAt = DateTime.Now,
                        CreatedBy = User.Identity?.Name
                    };
                    _context.Federations.Add(federation);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"تم إضافة اتحاد جديد: {name}");
                }
                else
                {
                    TempData["ErrorMessage"] = "هذا الاتحاد موجود مسبقاً";
                    return RedirectToAction(nameof(CreateFederation));
                }

                TempData["SuccessMessage"] = "✅ تم إضافة الاتحاد بنجاح";
                return RedirectToAction(nameof(Federations));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في إضافة اتحاد");
                ModelState.AddModelError("", "حدث خطأ أثناء الحفظ: " + ex.Message);
                return View();
            }
        }

        // POST: ManageableEntities/DeleteFederation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFederation(int id)
        {
            try
            {
                var federation = await _context.Federations.FindAsync(id);
                if (federation == null)
                {
                    return Json(new { success = false, message = "العنصر غير موجود" });
                }

                var isUsedByUsers = await IsFederationInUse(federation.Name);
                if (isUsedByUsers)
                {
                    return Json(new { success = false, message = "لا يمكن حذف الاتحاد لأنه مستخدم من قبل أعضاء" });
                }

                _context.Federations.Remove(federation);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "✅ تم الحذف بنجاح" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في حذف اتحاد");
                return Json(new { success = false, message = "❌ حدث خطأ: " + ex.Message });
            }
        }

        // GET: ManageableEntities/FederationDetails/{id}
        public async Task<IActionResult> FederationDetails(int id)
        {
            try
            {
                var federation = await _context.Federations
                    .Include(x => x.Divisions)
                    .ThenInclude(d => d.Sections)
                    .ThenInclude(s => s.Groups)
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (federation == null)
                {
                    TempData["ErrorMessage"] = "الاتحاد غير موجود";
                    return RedirectToAction(nameof(Federations));
                }

                var divisions = await _context.FederationDivisions
                    .Where(d => d.FederationId == id)
                    .OrderBy(d => d.Name)
                    .ToListAsync();

                ViewBag.Divisions = divisions;

                return View(federation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في عرض تفاصيل الاتحاد");
                TempData["ErrorMessage"] = "حدث خطأ في تحميل البيانات";
                return RedirectToAction(nameof(Federations));
            }
        }

        // GET: ManageableEntities/CreateFederationDivision
        public async Task<IActionResult> CreateFederationDivision(int? federationId)
        {
            var federations = await _context.Federations.ToListAsync();
            ViewBag.Federations = federations;
            ViewBag.FederationId = federationId;
            return View();
        }

        // POST: ManageableEntities/CreateFederationDivision
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFederationDivision(int federationId, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                ModelState.AddModelError("", "اسم القسم مطلوب");
                var federations = await _context.Federations.ToListAsync();
                ViewBag.Federations = federations;
                return View();
            }

            try
            {
                var federation = await _context.Federations.FindAsync(federationId);
                if (federation == null)
                {
                    TempData["ErrorMessage"] = "الاتحاد غير موجود";
                    return RedirectToAction(nameof(Federations));
                }

                var existingDivisions = await _context.FederationDivisions
                    .Where(d => d.FederationId == federationId)
                    .ToListAsync();
                var exists = existingDivisions.Any(x => x.Name == name);

                if (exists)
                {
                    TempData["ErrorMessage"] = "هذا القسم موجود مسبقاً";
                    return RedirectToAction(nameof(FederationDetails), new { id = federationId });
                }

                var division = new FederationDivision
                {
                    Name = name,
                    FederationId = federationId,
                    CreatedAt = DateTime.Now
                };

                _context.FederationDivisions.Add(division);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"✅ تم إضافة القسم '{name}' بنجاح";
                return RedirectToAction(nameof(FederationDetails), new { id = federationId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في إضافة قسم اتحاد");
                ModelState.AddModelError("", "حدث خطأ أثناء الحفظ: " + ex.Message);
                var federations = await _context.Federations.ToListAsync();
                ViewBag.Federations = federations;
                return View();
            }
        }

        // POST: ManageableEntities/DeleteFederationDivision
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFederationDivision(int id)
        {
            try
            {
                var division = await _context.FederationDivisions.FindAsync(id);
                if (division == null)
                {
                    return Json(new { success = false, message = "العنصر غير موجود" });
                }

                var isUsedByUsers = await IsFederationDivisionInUse(id);
                if (isUsedByUsers)
                {
                    return Json(new { success = false, message = "لا يمكن حذف القسم لأنه مستخدم من قبل أعضاء" });
                }

                var federationId = division.FederationId;
                _context.FederationDivisions.Remove(division);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "✅ تم الحذف بنجاح", redirectUrl = Url.Action(nameof(FederationDetails), new { id = federationId }) });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "❌ حدث خطأ: " + ex.Message });
            }
        }

        // GET: ManageableEntities/FederationDivisionDetails
        public async Task<IActionResult> FederationDivisionDetails(int id)
        {
            try
            {
                var division = await _context.FederationDivisions
                    .Include(x => x.Sections)
                    .ThenInclude(s => s.Groups)
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (division == null)
                {
                    TempData["ErrorMessage"] = "القسم غير موجود";
                    return RedirectToAction(nameof(Federations));
                }

                var sections = await _context.FederationSections
                    .Where(s => s.FederationDivisionId == id)
                    .OrderBy(s => s.Name)
                    .ToListAsync();

                ViewBag.Sections = sections;

                return View(division);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في عرض تفاصيل القسم");
                TempData["ErrorMessage"] = "حدث خطأ في تحميل البيانات";
                return RedirectToAction(nameof(Federations));
            }
        }

        // GET: ManageableEntities/CreateFederationSection
        public async Task<IActionResult> CreateFederationSection(int? divisionId)
        {
            if (divisionId.HasValue)
            {
                var division = await _context.FederationDivisions.FindAsync(divisionId.Value);
                ViewBag.CurrentDivision = division;
            }
            ViewBag.DivisionId = divisionId;
            return View();
        }

        // POST: ManageableEntities/CreateFederationSection
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFederationSection(int divisionId, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                ModelState.AddModelError("", "اسم الشعبة مطلوب");
                return View();
            }

            try
            {
                var division = await _context.FederationDivisions.FindAsync(divisionId);
                if (division == null)
                {
                    TempData["ErrorMessage"] = "القسم غير موجود";
                    return RedirectToAction(nameof(Federations));
                }

                var existingSections = await _context.FederationSections
                    .Where(s => s.FederationDivisionId == divisionId)
                    .ToListAsync();
                var exists = existingSections.Any(x => x.Name == name);

                if (exists)
                {
                    TempData["ErrorMessage"] = "هذه الشعبة موجودة مسبقاً";
                    return RedirectToAction(nameof(FederationDivisionDetails), new { id = divisionId });
                }

                var section = new FederationSection
                {
                    Name = name,
                    FederationDivisionId = divisionId,
                    CreatedAt = DateTime.Now
                };

                _context.FederationSections.Add(section);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"✅ تم إضافة الشعبة '{name}' بنجاح";
                return RedirectToAction(nameof(FederationDivisionDetails), new { id = divisionId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في إضافة شعبة اتحاد");
                ModelState.AddModelError("", "حدث خطأ أثناء الحفظ: " + ex.Message);
                return View();
            }
        }

        // POST: ManageableEntities/DeleteFederationSection
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFederationSection(int id)
        {
            try
            {
                var section = await _context.FederationSections.FindAsync(id);
                if (section == null)
                {
                    return Json(new { success = false, message = "العنصر غير موجود" });
                }

                var isUsedByUsers = await IsFederationSectionInUse(id);
                if (isUsedByUsers)
                {
                    return Json(new { success = false, message = "لا يمكن حذف الشعبة لأنها مستخدمة من قبل أعضاء" });
                }

                var divisionId = section.FederationDivisionId;
                _context.FederationSections.Remove(section);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "✅ تم الحذف بنجاح", redirectUrl = Url.Action(nameof(FederationDivisionDetails), new { id = divisionId }) });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "❌ حدث خطأ: " + ex.Message });
            }
        }

        // GET: ManageableEntities/FederationSectionDetails
        public async Task<IActionResult> FederationSectionDetails(int id)
        {
            try
            {
                var section = await _context.FederationSections
                    .Include(x => x.Groups)
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (section == null)
                {
                    TempData["ErrorMessage"] = "الشعبة غير موجودة";
                    return RedirectToAction(nameof(Federations));
                }

                var groups = await _context.FederationGroups
                    .Where(g => g.FederationSectionId == id)
                    .OrderBy(g => g.Name)
                    .ToListAsync();

                ViewBag.Groups = groups;

                return View(section);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في عرض تفاصيل الشعبة");
                TempData["ErrorMessage"] = "حدث خطأ في تحميل البيانات";
                return RedirectToAction(nameof(Federations));
            }
        }

        // GET: ManageableEntities/CreateFederationGroup
        public async Task<IActionResult> CreateFederationGroup(int? sectionId)
        {
            if (sectionId.HasValue)
            {
                var section = await _context.FederationSections.FindAsync(sectionId.Value);
                ViewBag.CurrentSection = section;
            }
            ViewBag.SectionId = sectionId;
            return View();
        }

        // POST: ManageableEntities/CreateFederationGroup
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFederationGroup(int sectionId, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                ModelState.AddModelError("", "اسم الوحدة مطلوب");
                return View();
            }

            try
            {
                var section = await _context.FederationSections.FindAsync(sectionId);
                if (section == null)
                {
                    TempData["ErrorMessage"] = "الشعبة غير موجودة";
                    return RedirectToAction(nameof(Federations));
                }

                var existingGroups = await _context.FederationGroups
                    .Where(g => g.FederationSectionId == sectionId)
                    .ToListAsync();
                var exists = existingGroups.Any(x => x.Name == name);

                if (exists)
                {
                    TempData["ErrorMessage"] = "هذه الوحدة موجودة مسبقاً";
                    return RedirectToAction(nameof(FederationSectionDetails), new { id = sectionId });
                }

                var group = new FederationGroup
                {
                    Name = name,
                    FederationSectionId = sectionId,
                    CreatedAt = DateTime.Now
                };

                _context.FederationGroups.Add(group);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"✅ تم إضافة الوحدة '{name}' بنجاح";
                return RedirectToAction(nameof(FederationSectionDetails), new { id = sectionId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في إضافة تجمع اتحاد");
                ModelState.AddModelError("", "حدث خطأ أثناء الحفظ: " + ex.Message);
                return View();
            }
        }

        // POST: ManageableEntities/DeleteFederationGroup
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFederationGroup(int id)
        {
            try
            {
                var group = await _context.FederationGroups.FindAsync(id);
                if (group == null)
                {
                    return Json(new { success = false, message = "العنصر غير موجود" });
                }

                var isUsedByUsers = await IsFederationGroupInUse(id);
                if (isUsedByUsers)
                {
                    return Json(new { success = false, message = "لا يمكن حذف الوحدة لأنها مستخدمة من قبل أعضاء" });
                }

                var sectionId = group.FederationSectionId;
                _context.FederationGroups.Remove(group);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "✅ تم الحذف بنجاح", redirectUrl = Url.Action(nameof(FederationSectionDetails), new { id = sectionId }) });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "❌ حدث خطأ: " + ex.Message });
            }
        }

        #endregion

        #region ========== النقابات (Unions) ==========

        // GET: ManageableEntities/Unions
        public async Task<IActionResult> Unions()
        {
            try
            {
                var unions = await _context.Unions
                    .OrderBy(u => u.Name)
                    .ToListAsync();
                return View(unions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في جلب النقابات");
                TempData["ErrorMessage"] = "حدث خطأ في تحميل البيانات";
                return View(new List<Union>());
            }
        }

        // GET: ManageableEntities/CreateUnion
        public IActionResult CreateUnion()
        {
            return View();
        }

        // POST: ManageableEntities/CreateUnion
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUnion(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                ModelState.AddModelError("", "اسم النقابة مطلوب");
                return View();
            }

            try
            {
                var exists = await _context.Unions.AnyAsync(u => u.Name == name);

                if (!exists)
                {
                    var union = new Union
                    {
                        Name = name,
                        CreatedAt = DateTime.Now,
                        CreatedBy = User.Identity?.Name
                    };
                    _context.Unions.Add(union);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"تم إضافة نقابة جديدة: {name}");
                }
                else
                {
                    TempData["ErrorMessage"] = "هذه النقابة موجودة مسبقاً";
                    return RedirectToAction(nameof(CreateUnion));
                }

                TempData["SuccessMessage"] = "✅ تم إضافة النقابة بنجاح";
                return RedirectToAction(nameof(Unions));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في إضافة نقابة");
                ModelState.AddModelError("", "حدث خطأ أثناء الحفظ: " + ex.Message);
                return View();
            }
        }

        // POST: ManageableEntities/DeleteUnion
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUnion(int id)
        {
            try
            {
                var union = await _context.Unions.FindAsync(id);
                if (union == null)
                {
                    return Json(new { success = false, message = "العنصر غير موجود" });
                }

                var isUsedByUsers = await IsUnionInUse(union.Name);
                if (isUsedByUsers)
                {
                    return Json(new { success = false, message = "لا يمكن الحذف لأنه مستخدم من قبل أعضاء" });
                }

                _context.Unions.Remove(union);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "✅ تم الحذف بنجاح" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في حذف نقابة");
                return Json(new { success = false, message = "❌ حدث خطأ: " + ex.Message });
            }
        }

        #endregion

        #region ========== الجمعيات (Associations) ==========

        // GET: ManageableEntities/Associations
        public async Task<IActionResult> Associations()
        {
            try
            {
                var associations = await _context.Associations
                    .OrderBy(a => a.Name)
                    .ToListAsync();
                return View(associations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في جلب الجمعيات");
                TempData["ErrorMessage"] = "حدث خطأ في تحميل البيانات";
                return View(new List<Association>());
            }
        }

        // GET: ManageableEntities/CreateAssociation
        public IActionResult CreateAssociation()
        {
            return View();
        }

        // POST: ManageableEntities/CreateAssociation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAssociation(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                ModelState.AddModelError("", "اسم الجمعية مطلوب");
                return View();
            }

            try
            {
                var exists = await _context.Associations.AnyAsync(a => a.Name == name);

                if (!exists)
                {
                    var association = new Association
                    {
                        Name = name,
                        CreatedAt = DateTime.Now,
                        CreatedBy = User.Identity?.Name
                    };
                    _context.Associations.Add(association);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"تم إضافة جمعية جديدة: {name}");
                }
                else
                {
                    TempData["ErrorMessage"] = "هذه الجمعية موجودة مسبقاً";
                    return RedirectToAction(nameof(CreateAssociation));
                }

                TempData["SuccessMessage"] = "✅ تم إضافة الجمعية بنجاح";
                return RedirectToAction(nameof(Associations));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في إضافة جمعية");
                ModelState.AddModelError("", "حدث خطأ أثناء الحفظ: " + ex.Message);
                return View();
            }
        }

        // POST: ManageableEntities/DeleteAssociation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAssociation(int id)
        {
            try
            {
                var association = await _context.Associations.FindAsync(id);
                if (association == null)
                {
                    return Json(new { success = false, message = "العنصر غير موجود" });
                }

                var isUsedByUsers = await IsAssociationInUse(association.Name);
                if (isUsedByUsers)
                {
                    return Json(new { success = false, message = "لا يمكن الحذف لأنه مستخدم من قبل أعضاء" });
                }

                _context.Associations.Remove(association);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "✅ تم الحذف بنجاح" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في حذف جمعية");
                return Json(new { success = false, message = "❌ حدث خطأ: " + ex.Message });
            }
        }

        #endregion

        #region ========== المنظمات غير الحكومية (Ngos) ==========

        // GET: ManageableEntities/Ngos
        public async Task<IActionResult> Ngos()
        {
            try
            {
                var ngos = await _context.Ngos
                    .OrderBy(n => n.Name)
                    .ToListAsync();
                return View(ngos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في جلب المنظمات غير الحكومية");
                TempData["ErrorMessage"] = "حدث خطأ في تحميل البيانات";
                return View(new List<Ngo>());
            }
        }

        // GET: ManageableEntities/CreateNgo
        public IActionResult CreateNgo()
        {
            return View();
        }

        // POST: ManageableEntities/CreateNgo
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateNgo(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                ModelState.AddModelError("", "الاسم مطلوب");
                return View();
            }

            try
            {
                var exists = await _context.Ngos.AnyAsync(n => n.Name == name);

                if (!exists)
                {
                    var ngo = new Ngo
                    {
                        Name = name,
                        CreatedAt = DateTime.Now,
                        CreatedBy = User.Identity?.Name
                    };
                    _context.Ngos.Add(ngo);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"تم إضافة منظمة غير حكومية: {name}");
                }
                else
                {
                    TempData["ErrorMessage"] = "هذه المنظمة موجودة مسبقاً";
                    return RedirectToAction(nameof(CreateNgo));
                }

                TempData["SuccessMessage"] = "✅ تم إضافة المنظمة غير الحكومية بنجاح";
                return RedirectToAction(nameof(Ngos));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في إضافة منظمة غير حكومية");
                ModelState.AddModelError("", "حدث خطأ أثناء الحفظ: " + ex.Message);
                return View();
            }
        }

        // POST: ManageableEntities/DeleteNgo
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteNgo(int id)
        {
            try
            {
                var ngo = await _context.Ngos.FindAsync(id);
                if (ngo == null)
                {
                    return Json(new { success = false, message = "العنصر غير موجود" });
                }

                var isUsedByUsers = await IsNgoInUse(ngo.Name);
                if (isUsedByUsers)
                {
                    return Json(new { success = false, message = "لا يمكن الحذف لأنه مستخدم من قبل أعضاء" });
                }

                _context.Ngos.Remove(ngo);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "✅ تم الحذف بنجاح" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في حذف منظمة غير حكومية");
                return Json(new { success = false, message = "❌ حدث خطأ: " + ex.Message });
            }
        }

        #endregion

        #region ========== API للصفحة المتكاملة ==========

        [HttpGet]
        public async Task<IActionResult> FullHierarchyManager()
        {
            var entities = await _context.AffiliationEntities.ToListAsync();
            ViewBag.AffiliationEntities = entities.ToList();
            return View();
        }

        // API لجهة الانتساب
        [HttpGet]
        public async Task<IActionResult> GetDivisionsByEntity(int entityId)
        {
            var divisions = await _context.Divisions
                .Where(d => d.AffiliationEntityId == entityId)
                .Select(d => new { id = d.Id, name = d.Name })
                .ToListAsync();
            return Json(new { success = true, data = divisions });
        }

        [HttpGet]
        public async Task<IActionResult> GetSectionsByDivision(int divisionId)
        {
            var sections = await _context.Sections
                .Where(s => s.DivisionId == divisionId)
                .Select(s => new { id = s.Id, name = s.Name })
                .ToListAsync();
            return Json(new { success = true, data = sections });
        }

        [HttpGet]
        public async Task<IActionResult> GetGroupsBySection(int sectionId)
        {
            var groups = await _context.Groups
                .Where(g => g.SectionId == sectionId)
                .Select(g => new { id = g.Id, name = g.Name })
                .ToListAsync();
            return Json(new { success = true, data = groups });
        }

        // API للاتحاد
        [HttpGet]
        public async Task<IActionResult> GetFederationDivisionsByFederation(int federationId)
        {
            var divisions = await _context.FederationDivisions
                .Where(d => d.FederationId == federationId)
                .Select(d => new { id = d.Id, name = d.Name })
                .ToListAsync();
            return Json(new { success = true, data = divisions });
        }

        [HttpGet]
        public async Task<IActionResult> GetFederationSectionsByDivision(int divisionId)
        {
            var sections = await _context.FederationSections
                .Where(s => s.FederationDivisionId == divisionId)
                .Select(s => new { id = s.Id, name = s.Name })
                .ToListAsync();
            return Json(new { success = true, data = sections });
        }

        [HttpGet]
        public async Task<IActionResult> GetFederationGroupsBySection(int sectionId)
        {
            var groups = await _context.FederationGroups
                .Where(g => g.FederationSectionId == sectionId)
                .Select(g => new { id = g.Id, name = g.Name })
                .ToListAsync();
            return Json(new { success = true, data = groups });
        }

        // دوال البحث بالاسم (للـ Views)
        [HttpGet]
        public async Task<IActionResult> GetDivisionsByEntityName(string entityName)
        {
            var entity = await _context.AffiliationEntities.FirstOrDefaultAsync(e => e.Name == entityName);
            if (entity == null) return Json(new { success = true, data = new List<object>() });

            var divisions = await _context.Divisions
                .Where(d => d.AffiliationEntityId == entity.Id)
                .Select(d => new { id = d.Id, name = d.Name })
                .ToListAsync();
            return Json(new { success = true, data = divisions });
        }

        [HttpGet]
        public async Task<IActionResult> GetSectionsByDivisionName(string divisionName)
        {
            var division = await _context.Divisions.FirstOrDefaultAsync(d => d.Name == divisionName);
            if (division == null) return Json(new { success = true, data = new List<object>() });

            var sections = await _context.Sections
                .Where(s => s.DivisionId == division.Id)
                .Select(s => new { id = s.Id, name = s.Name })
                .ToListAsync();
            return Json(new { success = true, data = sections });
        }

        [HttpGet]
        public async Task<IActionResult> GetGroupsBySectionName(string sectionName)
        {
            var section = await _context.Sections.FirstOrDefaultAsync(s => s.Name == sectionName);
            if (section == null) return Json(new { success = true, data = new List<object>() });

            var groups = await _context.Groups
                .Where(g => g.SectionId == section.Id)
                .Select(g => new { id = g.Id, name = g.Name })
                .ToListAsync();
            return Json(new { success = true, data = groups });
        }

        // دوال البحث بالاسم للاتحاد
        [HttpGet]
        public async Task<IActionResult> GetFederationDivisionsByFederationName(string federationName)
        {
            var federation = await _context.Federations.FirstOrDefaultAsync(f => f.Name == federationName);
            if (federation == null) return Json(new { success = true, data = new List<object>() });

            var divisions = await _context.FederationDivisions
                .Where(d => d.FederationId == federation.Id)
                .Select(d => new { id = d.Id, name = d.Name })
                .ToListAsync();
            return Json(new { success = true, data = divisions });
        }

        [HttpGet]
        public async Task<IActionResult> GetFederationSectionsByDivisionName(string divisionName)
        {
            var division = await _context.FederationDivisions.FirstOrDefaultAsync(d => d.Name == divisionName);
            if (division == null) return Json(new { success = true, data = new List<object>() });

            var sections = await _context.FederationSections
                .Where(s => s.FederationDivisionId == division.Id)
                .Select(s => new { id = s.Id, name = s.Name })
                .ToListAsync();
            return Json(new { success = true, data = sections });
        }

        [HttpGet]
        public async Task<IActionResult> GetFederationGroupsBySectionName(string sectionName)
        {
            var section = await _context.FederationSections.FirstOrDefaultAsync(s => s.Name == sectionName);
            if (section == null) return Json(new { success = true, data = new List<object>() });

            var groups = await _context.FederationGroups
                .Where(g => g.FederationSectionId == section.Id)
                .Select(g => new { id = g.Id, name = g.Name })
                .ToListAsync();
            return Json(new { success = true, data = groups });
        }

        // دوال الإضافة عبر AJAX لجهة الانتساب
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAffiliationEntityAjax(string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                    return Json(new { success = false, message = "الاسم مطلوب" });

                var allEntities = await _context.AffiliationEntities.ToListAsync();
                if (allEntities.Any(x => x.Name == name))
                    return Json(new { success = false, message = "هذه الجهة موجودة مسبقاً" });

                var entity = new AffiliationEntity { Name = name, CreatedAt = DateTime.Now };
                _context.AffiliationEntities.Add(entity);
                await _context.SaveChangesAsync();

                return Json(new { success = true, id = entity.Id, message = "تمت الإضافة بنجاح" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddDivisionAjax(string name, int entityId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                    return Json(new { success = false, message = "الاسم مطلوب" });

                var existingDivisions = await _context.Divisions
                    .Where(d => d.AffiliationEntityId == entityId)
                    .ToListAsync();
                if (existingDivisions.Any(x => x.Name == name))
                    return Json(new { success = false, message = "هذا القسم موجود مسبقاً" });

                var division = new Division { Name = name, AffiliationEntityId = entityId, CreatedAt = DateTime.Now };
                _context.Divisions.Add(division);
                await _context.SaveChangesAsync();

                return Json(new { success = true, id = division.Id, message = "تمت الإضافة بنجاح" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddSectionAjax(string name, int divisionId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                    return Json(new { success = false, message = "الاسم مطلوب" });

                var existingSections = await _context.Sections
                    .Where(s => s.DivisionId == divisionId)
                    .ToListAsync();
                if (existingSections.Any(x => x.Name == name))
                    return Json(new { success = false, message = "هذه الشعبة موجودة مسبقاً" });

                var section = new Section { Name = name, DivisionId = divisionId, CreatedAt = DateTime.Now };
                _context.Sections.Add(section);
                await _context.SaveChangesAsync();

                return Json(new { success = true, id = section.Id, message = "تمت الإضافة بنجاح" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddGroupAjax(string name, int sectionId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                    return Json(new { success = false, message = "الاسم مطلوب" });

                var existingGroups = await _context.Groups
                    .Where(g => g.SectionId == sectionId)
                    .ToListAsync();
                if (existingGroups.Any(x => x.Name == name))
                    return Json(new { success = false, message = "هذه الوحدة موجودة مسبقاً" });

                var group = new Group { Name = name, SectionId = sectionId, CreatedAt = DateTime.Now };
                _context.Groups.Add(group);
                await _context.SaveChangesAsync();

                return Json(new { success = true, id = group.Id, message = "تمت الإضافة بنجاح" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // دوال الإضافة للاتحاد عبر AJAX
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddFederationAjax(string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                    return Json(new { success = false, message = "الاسم مطلوب" });

                var exists = await _context.Federations.AnyAsync(f => f.Name == name);
                if (exists)
                    return Json(new { success = false, message = "هذا الاتحاد موجود مسبقاً" });

                var federation = new Federation
                {
                    Name = name,
                    CreatedAt = DateTime.Now,
                    CreatedBy = User.Identity?.Name
                };
                _context.Federations.Add(federation);
                await _context.SaveChangesAsync();

                return Json(new { success = true, id = federation.Id, message = "تمت الإضافة بنجاح" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddFederationDivisionAjax(string name, int federationId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                    return Json(new { success = false, message = "الاسم مطلوب" });

                var existingDivisions = await _context.FederationDivisions
                    .Where(d => d.FederationId == federationId)
                    .ToListAsync();
                if (existingDivisions.Any(x => x.Name == name))
                    return Json(new { success = false, message = "هذا القسم موجود مسبقاً" });

                var division = new FederationDivision { Name = name, FederationId = federationId, CreatedAt = DateTime.Now };
                _context.FederationDivisions.Add(division);
                await _context.SaveChangesAsync();

                return Json(new { success = true, id = division.Id, message = "تمت الإضافة بنجاح" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddFederationSectionAjax(string name, int divisionId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                    return Json(new { success = false, message = "الاسم مطلوب" });

                var existingSections = await _context.FederationSections
                    .Where(s => s.FederationDivisionId == divisionId)
                    .ToListAsync();
                if (existingSections.Any(x => x.Name == name))
                    return Json(new { success = false, message = "هذه الشعبة موجودة مسبقاً" });

                var section = new FederationSection { Name = name, FederationDivisionId = divisionId, CreatedAt = DateTime.Now };
                _context.FederationSections.Add(section);
                await _context.SaveChangesAsync();

                return Json(new { success = true, id = section.Id, message = "تمت الإضافة بنجاح" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ===== دالة AJAX للاتحاد لإضافة تجمع (المفقودة) =====
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddFederationGroupAjax(string name, int sectionId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                    return Json(new { success = false, message = "الاسم مطلوب" });

                var existingGroups = await _context.FederationGroups
                    .Where(g => g.FederationSectionId == sectionId)
                    .ToListAsync();
                if (existingGroups.Any(x => x.Name == name))
                    return Json(new { success = false, message = "هذه الوحدة موجودة مسبقاً" });

                var group = new FederationGroup
                {
                    Name = name,
                    FederationSectionId = sectionId,
                    CreatedAt = DateTime.Now
                };
                _context.FederationGroups.Add(group);
                await _context.SaveChangesAsync();

                return Json(new { success = true, id = group.Id, message = "تمت الإضافة بنجاح" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion
    }
}
