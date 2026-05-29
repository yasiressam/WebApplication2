using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;

namespace WebApplication2.Controllers
{
    [AllowAnonymous] // متاح للجميع بدون تسجيل دخول
    public class PublicDataController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PublicDataController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ========== دوال جهة الانتساب ==========

        [HttpGet]
        public async Task<IActionResult> GetDivisionsByEntityName(string entityName)
        {
            var allEntities = await _context.AffiliationEntities.ToListAsync();
            var entity = allEntities.FirstOrDefault(e => e.Name == entityName);
            if (entity == null) return Json(new { success = true, data = new List<object>() });

            var divisions = await _context.Divisions
                .Where(d => d.AffiliationEntityId == entity.Id)
                .Select(d => new { id = d.Id, name = d.Name })
                .ToListAsync();
            return Json(new { success = true, data = divisions });
        }

        [HttpGet]
        public async Task<IActionResult> GetSectionsByDivisionName(string entityName, string divisionName)
        {
            if (string.IsNullOrWhiteSpace(entityName) || string.IsNullOrWhiteSpace(divisionName))
                return Json(new { success = true, data = new List<object>() });

            var entity = await _context.AffiliationEntities
                .FirstOrDefaultAsync(e => e.Name == entityName);

            if (entity == null)
                return Json(new { success = true, data = new List<object>() });

            var division = await _context.Divisions
                .FirstOrDefaultAsync(d => d.AffiliationEntityId == entity.Id && d.Name == divisionName);

            if (division == null)
                return Json(new { success = true, data = new List<object>() });

            var sections = await _context.Sections
                .Where(s => s.DivisionId == division.Id)
                .Select(s => new { id = s.Id, name = s.Name })
                .ToListAsync();

            return Json(new { success = true, data = sections });
        }

        [HttpGet]
        public async Task<IActionResult> GetGroupsBySectionName(string entityName, string divisionName, string sectionName)
        {
            if (string.IsNullOrWhiteSpace(entityName) || string.IsNullOrWhiteSpace(divisionName) || string.IsNullOrWhiteSpace(sectionName))
                return Json(new { success = true, data = new List<object>() });

            var entity = await _context.AffiliationEntities
                .FirstOrDefaultAsync(e => e.Name == entityName);

            if (entity == null)
                return Json(new { success = true, data = new List<object>() });

            var division = await _context.Divisions
                .FirstOrDefaultAsync(d => d.AffiliationEntityId == entity.Id && d.Name == divisionName);

            if (division == null)
                return Json(new { success = true, data = new List<object>() });

            var section = await _context.Sections
                .FirstOrDefaultAsync(s => s.DivisionId == division.Id && s.Name == sectionName);

            if (section == null)
                return Json(new { success = true, data = new List<object>() });

            var groups = await _context.Groups
                .Where(g => g.SectionId == section.Id)
                .Select(g => new { id = g.Id, name = g.Name })
                .ToListAsync();

            return Json(new { success = true, data = groups });
        }

        // ========== دوال الاتحاد (Federation) - المستويات الأربعة ==========

        /// <summary>
        /// الحصول على أقسام الاتحاد حسب اسم الاتحاد
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetFederationDivisionsByFederationName(string federationName)
        {
            try
            {
                if (string.IsNullOrEmpty(federationName))
                    return Json(new { success = true, data = new List<object>() });

                // البحث عن الاتحاد في الجدول الرئيسي
                var federation = await _context.Federations
                    .FirstOrDefaultAsync(f => f.Name == federationName);
                if (federation == null)
                    return Json(new { success = true, data = new List<object>() });

                // جلب أقسام هذا الاتحاد
                var divisions = await _context.FederationDivisions
                    .Where(d => d.FederationId == federation.Id)
                    .Select(d => new { id = d.Id, name = d.Name })
                    .ToListAsync();

                return Json(new { success = true, data = divisions });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, data = new List<object>(), message = ex.Message });
            }
        }

        /// <summary>
        /// الحصول على شعب الاتحاد حسب اسم القسم
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetFederationSectionsByDivisionName(string federationName, string divisionName)
        {
            try
            {
                if (string.IsNullOrEmpty(federationName) || string.IsNullOrEmpty(divisionName))
                    return Json(new { success = true, data = new List<object>() });

                // البحث عن الاتحاد
                var federation = await _context.Federations
                    .FirstOrDefaultAsync(f => f.Name == federationName);
                if (federation == null)
                    return Json(new { success = true, data = new List<object>() });

                // البحث عن القسم
                var division = await _context.FederationDivisions
                    .FirstOrDefaultAsync(d => d.FederationId == federation.Id && d.Name == divisionName);
                if (division == null)
                    return Json(new { success = true, data = new List<object>() });

                // جلب شعب هذا القسم
                var sections = await _context.FederationSections
                    .Where(s => s.FederationDivisionId == division.Id)
                    .Select(s => new { id = s.Id, name = s.Name })
                    .ToListAsync();

                return Json(new { success = true, data = sections });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, data = new List<object>(), message = ex.Message });
            }
        }

        /// <summary>
        /// الحصول على تجمعات الاتحاد حسب اسم الشعبة
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetFederationGroupsBySectionName(string federationName, string divisionName, string sectionName)
        {
            try
            {
                if (string.IsNullOrEmpty(federationName) || string.IsNullOrEmpty(divisionName) || string.IsNullOrEmpty(sectionName))
                    return Json(new { success = true, data = new List<object>() });

                // البحث عن الاتحاد
                var federation = await _context.Federations
                    .FirstOrDefaultAsync(f => f.Name == federationName);
                if (federation == null)
                    return Json(new { success = true, data = new List<object>() });

                // البحث عن القسم
                var division = await _context.FederationDivisions
                    .FirstOrDefaultAsync(d => d.FederationId == federation.Id && d.Name == divisionName);
                if (division == null)
                    return Json(new { success = true, data = new List<object>() });

                // البحث عن الشعبة
                var section = await _context.FederationSections
                    .FirstOrDefaultAsync(s => s.FederationDivisionId == division.Id && s.Name == sectionName);
                if (section == null)
                    return Json(new { success = true, data = new List<object>() });

                // جلب تجمعات هذه الشعبة
                var groups = await _context.FederationGroups
                    .Where(g => g.FederationSectionId == section.Id)
                    .Select(g => new { id = g.Id, name = g.Name })
                    .ToListAsync();

                return Json(new { success = true, data = groups });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, data = new List<object>(), message = ex.Message });
            }
        }

        // ========== دوال إضافية للاتحاد (اختيارية) ==========

        /// <summary>
        /// الحصول على جميع الاتحادات (للقوائم المنسدلة)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllFederations()
        {
            try
            {
                var federations = await _context.Federations
                    .Select(f => new { id = f.Id, name = f.Name })
                    .ToListAsync();
                return Json(new { success = true, data = federations });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, data = new List<object>(), message = ex.Message });
            }
        }

        /// <summary>
        /// الحصول على جميع أقسام الاتحاد
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllFederationDivisions(int? federationId)
        {
            try
            {
                IQueryable<FederationDivision> query = _context.FederationDivisions;

                if (federationId.HasValue && federationId.Value > 0)
                {
                    query = query.Where(d => d.FederationId == federationId.Value);
                }

                var data = await query
                    .Select(d => new { id = d.Id, name = d.Name, federationId = d.FederationId })
                    .ToListAsync();

                return Json(new { success = true, data = data });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, data = new List<object>(), message = ex.Message });
            }
        }

        /// <summary>
        /// الحصول على جميع شعب الاتحاد
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllFederationSections(int? divisionId)
        {
            try
            {
                IQueryable<FederationSection> query = _context.FederationSections;

                if (divisionId.HasValue && divisionId.Value > 0)
                {
                    query = query.Where(s => s.FederationDivisionId == divisionId.Value);
                }

                var data = await query
                    .Select(s => new { id = s.Id, name = s.Name, divisionId = s.FederationDivisionId })
                    .ToListAsync();

                return Json(new { success = true, data = data });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, data = new List<object>(), message = ex.Message });
            }
        }

        /// <summary>
        /// الحصول على جميع تجمعات الاتحاد
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllFederationGroups(int? sectionId)
        {
            try
            {
                IQueryable<FederationGroup> query = _context.FederationGroups;

                if (sectionId.HasValue && sectionId.Value > 0)
                {
                    query = query.Where(g => g.FederationSectionId == sectionId.Value);
                }

                var data = await query
                    .Select(g => new { id = g.Id, name = g.Name, sectionId = g.FederationSectionId })
                    .ToListAsync();

                return Json(new { success = true, data = data });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, data = new List<object>(), message = ex.Message });
            }
        }
    }
}