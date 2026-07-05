using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;

namespace WebApplication2.Controllers.Api
{
    [Route("api/public-data")]
    [ApiController]
    [AllowAnonymous]
    public class PublicDataApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public PublicDataApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("divisions/by-entity")]
        public async Task<IActionResult> GetDivisionsByEntityName([FromQuery] string entityName)
        {
            if (string.IsNullOrWhiteSpace(entityName))
            {
                return Ok(new { success = true, data = new List<object>() });
            }

            var entity = await _context.AffiliationEntities
                .FirstOrDefaultAsync(e => e.Name == entityName);

            if (entity == null)
            {
                return Ok(new { success = true, data = new List<object>() });
            }

            var divisions = await _context.Divisions
                .Where(d => d.AffiliationEntityId == entity.Id)
                .Select(d => new { id = d.Id, name = d.Name })
                .ToListAsync();

            return Ok(new { success = true, data = divisions });
        }

        [HttpGet("sections/by-division")]
        public async Task<IActionResult> GetSectionsByDivisionName([FromQuery] string entityName, [FromQuery] string divisionName)
        {
            if (string.IsNullOrWhiteSpace(entityName) || string.IsNullOrWhiteSpace(divisionName))
            {
                return Ok(new { success = true, data = new List<object>() });
            }

            var entity = await _context.AffiliationEntities
                .FirstOrDefaultAsync(e => e.Name == entityName);

            if (entity == null)
            {
                return Ok(new { success = true, data = new List<object>() });
            }

            var division = await _context.Divisions
                .FirstOrDefaultAsync(d => d.AffiliationEntityId == entity.Id && d.Name == divisionName);

            if (division == null)
            {
                return Ok(new { success = true, data = new List<object>() });
            }

            var sections = await _context.Sections
                .Where(s => s.DivisionId == division.Id)
                .Select(s => new { id = s.Id, name = s.Name })
                .ToListAsync();

            return Ok(new { success = true, data = sections });
        }

        [HttpGet("groups/by-section")]
        public async Task<IActionResult> GetGroupsBySectionName(
            [FromQuery] string entityName,
            [FromQuery] string divisionName,
            [FromQuery] string sectionName)
        {
            if (string.IsNullOrWhiteSpace(entityName) ||
                string.IsNullOrWhiteSpace(divisionName) ||
                string.IsNullOrWhiteSpace(sectionName))
            {
                return Ok(new { success = true, data = new List<object>() });
            }

            var entity = await _context.AffiliationEntities
                .FirstOrDefaultAsync(e => e.Name == entityName);

            if (entity == null)
            {
                return Ok(new { success = true, data = new List<object>() });
            }

            var division = await _context.Divisions
                .FirstOrDefaultAsync(d => d.AffiliationEntityId == entity.Id && d.Name == divisionName);

            if (division == null)
            {
                return Ok(new { success = true, data = new List<object>() });
            }

            var section = await _context.Sections
                .FirstOrDefaultAsync(s => s.DivisionId == division.Id && s.Name == sectionName);

            if (section == null)
            {
                return Ok(new { success = true, data = new List<object>() });
            }

            var groups = await _context.Groups
                .Where(g => g.SectionId == section.Id)
                .Select(g => new { id = g.Id, name = g.Name })
                .ToListAsync();

            return Ok(new { success = true, data = groups });
        }

        [HttpGet("federations")]
        public async Task<IActionResult> GetAllFederations()
        {
            var federations = await _context.Federations
                .Select(f => new { id = f.Id, name = f.Name })
                .ToListAsync();

            return Ok(new { success = true, data = federations });
        }

        [HttpGet("federation-divisions/by-federation")]
        public async Task<IActionResult> GetFederationDivisionsByFederationName([FromQuery] string federationName)
        {
            if (string.IsNullOrWhiteSpace(federationName))
            {
                return Ok(new { success = true, data = new List<object>() });
            }

            var federation = await _context.Federations
                .FirstOrDefaultAsync(f => f.Name == federationName);

            if (federation == null)
            {
                return Ok(new { success = true, data = new List<object>() });
            }

            var divisions = await _context.FederationDivisions
                .Where(d => d.FederationId == federation.Id)
                .Select(d => new { id = d.Id, name = d.Name })
                .ToListAsync();

            return Ok(new { success = true, data = divisions });
        }

        [HttpGet("federation-sections/by-division")]
        public async Task<IActionResult> GetFederationSectionsByDivisionName(
            [FromQuery] string federationName,
            [FromQuery] string divisionName)
        {
            if (string.IsNullOrWhiteSpace(federationName) || string.IsNullOrWhiteSpace(divisionName))
            {
                return Ok(new { success = true, data = new List<object>() });
            }

            var federation = await _context.Federations
                .FirstOrDefaultAsync(f => f.Name == federationName);

            if (federation == null)
            {
                return Ok(new { success = true, data = new List<object>() });
            }

            var division = await _context.FederationDivisions
                .FirstOrDefaultAsync(d => d.FederationId == federation.Id && d.Name == divisionName);

            if (division == null)
            {
                return Ok(new { success = true, data = new List<object>() });
            }

            var sections = await _context.FederationSections
                .Where(s => s.FederationDivisionId == division.Id)
                .Select(s => new { id = s.Id, name = s.Name })
                .ToListAsync();

            return Ok(new { success = true, data = sections });
        }

        [HttpGet("federation-groups/by-section")]
        public async Task<IActionResult> GetFederationGroupsBySectionName(
            [FromQuery] string federationName,
            [FromQuery] string divisionName,
            [FromQuery] string sectionName)
        {
            if (string.IsNullOrWhiteSpace(federationName) ||
                string.IsNullOrWhiteSpace(divisionName) ||
                string.IsNullOrWhiteSpace(sectionName))
            {
                return Ok(new { success = true, data = new List<object>() });
            }

            var federation = await _context.Federations
                .FirstOrDefaultAsync(f => f.Name == federationName);

            if (federation == null)
            {
                return Ok(new { success = true, data = new List<object>() });
            }

            var division = await _context.FederationDivisions
                .FirstOrDefaultAsync(d => d.FederationId == federation.Id && d.Name == divisionName);

            if (division == null)
            {
                return Ok(new { success = true, data = new List<object>() });
            }

            var section = await _context.FederationSections
                .FirstOrDefaultAsync(s => s.FederationDivisionId == division.Id && s.Name == sectionName);

            if (section == null)
            {
                return Ok(new { success = true, data = new List<object>() });
            }

            var groups = await _context.FederationGroups
                .Where(g => g.FederationSectionId == section.Id)
                .Select(g => new { id = g.Id, name = g.Name })
                .ToListAsync();

            return Ok(new { success = true, data = groups });
        }

        [HttpGet("federation-divisions")]
        public async Task<IActionResult> GetAllFederationDivisions([FromQuery] int? federationId)
        {
            IQueryable<FederationDivision> query = _context.FederationDivisions;

            if (federationId.HasValue && federationId.Value > 0)
            {
                query = query.Where(d => d.FederationId == federationId.Value);
            }

            var data = await query
                .Select(d => new { id = d.Id, name = d.Name, federationId = d.FederationId })
                .ToListAsync();

            return Ok(new { success = true, data });
        }

        [HttpGet("federation-sections")]
        public async Task<IActionResult> GetAllFederationSections([FromQuery] int? divisionId)
        {
            IQueryable<FederationSection> query = _context.FederationSections;

            if (divisionId.HasValue && divisionId.Value > 0)
            {
                query = query.Where(s => s.FederationDivisionId == divisionId.Value);
            }

            var data = await query
                .Select(s => new { id = s.Id, name = s.Name, divisionId = s.FederationDivisionId })
                .ToListAsync();

            return Ok(new { success = true, data });
        }

        [HttpGet("federation-groups")]
        public async Task<IActionResult> GetAllFederationGroups([FromQuery] int? sectionId)
        {
            IQueryable<FederationGroup> query = _context.FederationGroups;

            if (sectionId.HasValue && sectionId.Value > 0)
            {
                query = query.Where(g => g.FederationSectionId == sectionId.Value);
            }

            var data = await query
                .Select(g => new { id = g.Id, name = g.Name, sectionId = g.FederationSectionId })
                .ToListAsync();

            return Ok(new { success = true, data });
        }
    }
}
