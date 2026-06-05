using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;

namespace WebApplication2.Services
{
    public static class BaghdadWorkLocationNormalizer
    {
        public static async Task<int> NormalizeAsync(ApplicationDbContext context)
        {
            var updatedCount = 0;

            static string? NormalizeBaghdadGovernorate(string? governorate, string? district)
            {
                if (string.IsNullOrWhiteSpace(governorate))
                    return governorate;

                var normalizedGovernorate = governorate.Trim();
                if (normalizedGovernorate != "Ø¨ØºØ¯Ø§Ø¯")
                    return normalizedGovernorate;

                var normalizedDistrict = district?.Trim();
                return normalizedDistrict switch
                {
                    "Ø§Ù„ÙƒØ±Ø®" => "Ø¨ØºØ¯Ø§Ø¯ - Ø§Ù„ÙƒØ±Ø®",
                    "Ø§Ù„Ø±ØµØ§ÙØ©" => "Ø¨ØºØ¯Ø§Ø¯ - Ø§Ù„Ø±ØµØ§ÙØ©",
                    _ => "Ø¨ØºØ¯Ø§Ø¯ Ù…Ø±ÙƒØ²ÙŠ"
                };
            }

            static bool IsBaghdadWorkGovernorate(string? governorate)
            {
                return governorate?.Trim() == "Ø¨ØºØ¯Ø§Ø¯" ||
                       governorate?.Trim().StartsWith("Ø¨ØºØ¯Ø§Ø¯ -", StringComparison.OrdinalIgnoreCase) == true ||
                       governorate?.Trim() == "Ø¨ØºØ¯Ø§Ø¯ Ù…Ø±ÙƒØ²ÙŠ";
            }

            var identifies = await context.Identifies
                .Where(i =>
                    i.WorkGovernorate == "Ø¨ØºØ¯Ø§Ø¯" ||
                    i.ManagedGovernorate == "Ø¨ØºØ¯Ø§Ø¯" ||
                    (i.WorkGovernorate != null && i.WorkGovernorate.StartsWith("Ø¨ØºØ¯Ø§Ø¯ -") && i.WorkDistrict != null) ||
                    (i.ManagedGovernorate != null && i.ManagedGovernorate.StartsWith("Ø¨ØºØ¯Ø§Ø¯ -") && i.ManagedDistrict != null) ||
                    (i.WorkGovernorate == "Ø¨ØºØ¯Ø§Ø¯ Ù…Ø±ÙƒØ²ÙŠ" && i.WorkDistrict != null) ||
                    (i.ManagedGovernorate == "Ø¨ØºØ¯Ø§Ø¯ Ù…Ø±ÙƒØ²ÙŠ" && i.ManagedDistrict != null))
                .ToListAsync();

            foreach (var identify in identifies)
            {
                var changed = false;

                var normalizedWorkGovernorate = NormalizeBaghdadGovernorate(identify.WorkGovernorate, identify.WorkDistrict);
                if (normalizedWorkGovernorate != identify.WorkGovernorate)
                {
                    identify.WorkGovernorate = normalizedWorkGovernorate;
                    changed = true;
                }

                if (IsBaghdadWorkGovernorate(identify.WorkGovernorate) && !string.IsNullOrWhiteSpace(identify.WorkDistrict))
                {
                    identify.WorkDistrict = null;
                    changed = true;
                }

                var normalizedManagedGovernorate = NormalizeBaghdadGovernorate(identify.ManagedGovernorate, identify.ManagedDistrict);
                if (normalizedManagedGovernorate != identify.ManagedGovernorate)
                {
                    identify.ManagedGovernorate = normalizedManagedGovernorate;
                    changed = true;
                }

                if (IsBaghdadWorkGovernorate(identify.ManagedGovernorate) && !string.IsNullOrWhiteSpace(identify.ManagedDistrict))
                {
                    identify.ManagedDistrict = null;
                    changed = true;
                }

                if (changed)
                    updatedCount++;
            }

            var workLocations = await context.WorkLocations
                .Where(w =>
                    w.Governorate == "Ø¨ØºØ¯Ø§Ø¯" ||
                    (w.Governorate.StartsWith("Ø¨ØºØ¯Ø§Ø¯ -") && w.District != null) ||
                    (w.Governorate == "Ø¨ØºØ¯Ø§Ø¯ Ù…Ø±ÙƒØ²ÙŠ" && w.District != null))
                .ToListAsync();

            foreach (var workLocation in workLocations)
            {
                var changed = false;
                var normalizedGovernorate = NormalizeBaghdadGovernorate(workLocation.Governorate, workLocation.District);

                if (normalizedGovernorate != workLocation.Governorate)
                {
                    workLocation.Governorate = normalizedGovernorate ?? workLocation.Governorate;
                    changed = true;
                }

                if (IsBaghdadWorkGovernorate(workLocation.Governorate) && !string.IsNullOrWhiteSpace(workLocation.District))
                {
                    workLocation.District = null;
                    changed = true;
                }

                if (changed)
                    updatedCount++;
            }

            var assignments = await context.ManagementAssignments
                .Where(a => a.Governorate == "Ø¨ØºØ¯Ø§Ø¯" || (a.Governorate.StartsWith("Ø¨ØºØ¯Ø§Ø¯ -") && a.BaghdadScope != null))
                .ToListAsync();

            foreach (var assignment in assignments)
            {
                var changed = false;
                var normalizedGovernorate = NormalizeBaghdadGovernorate(assignment.Governorate, assignment.BaghdadScope);

                if (normalizedGovernorate != assignment.Governorate)
                {
                    assignment.Governorate = normalizedGovernorate ?? assignment.Governorate;
                    changed = true;
                }

                if (IsBaghdadWorkGovernorate(assignment.Governorate) && !string.IsNullOrWhiteSpace(assignment.BaghdadScope))
                {
                    assignment.BaghdadScope = null;
                    changed = true;
                }

                if (changed)
                    updatedCount++;
            }

            var assignmentRequests = await context.ManagementAssignmentRequests
                .Where(r => r.Governorate == "Ø¨ØºØ¯Ø§Ø¯")
                .ToListAsync();

            foreach (var request in assignmentRequests)
            {
                request.Governorate = "Ø¨ØºØ¯Ø§Ø¯ Ù…Ø±ÙƒØ²ÙŠ";
                updatedCount++;
            }

            if (updatedCount > 0)
                await context.SaveChangesAsync();

            return updatedCount;
        }
    }
}
