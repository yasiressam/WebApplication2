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
                if (normalizedGovernorate != "بغداد")
                    return normalizedGovernorate;

                var normalizedDistrict = district?.Trim();
                return normalizedDistrict switch
                {
                    "الكرخ" => "بغداد - الكرخ",
                    "الرصافة" => "بغداد - الرصافة",
                    _ => "بغداد مركزي"
                };
            }

            static bool IsBaghdadWorkGovernorate(string? governorate)
            {
                return governorate?.Trim() == "بغداد" ||
                       governorate?.Trim().StartsWith("بغداد -", StringComparison.OrdinalIgnoreCase) == true ||
                       governorate?.Trim() == "بغداد مركزي";
            }

            var identifies = await context.Identifies
                .Where(i =>
                    i.WorkGovernorate == "بغداد" ||
                    i.ManagedGovernorate == "بغداد" ||
                    (i.WorkGovernorate != null && i.WorkGovernorate.StartsWith("بغداد -") && i.WorkDistrict != null) ||
                    (i.ManagedGovernorate != null && i.ManagedGovernorate.StartsWith("بغداد -") && i.ManagedDistrict != null) ||
                    (i.WorkGovernorate == "بغداد مركزي" && i.WorkDistrict != null) ||
                    (i.ManagedGovernorate == "بغداد مركزي" && i.ManagedDistrict != null))
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
                    w.Governorate == "بغداد" ||
                    (w.Governorate.StartsWith("بغداد -") && w.District != null) ||
                    (w.Governorate == "بغداد مركزي" && w.District != null))
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
                .Where(a => a.Governorate == "بغداد" || (a.Governorate.StartsWith("بغداد -") && a.BaghdadScope != null))
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
                .Where(r => r.Governorate == "بغداد")
                .ToListAsync();

            foreach (var request in assignmentRequests)
            {
                request.Governorate = "بغداد مركزي";
                updatedCount++;
            }

            if (updatedCount > 0)
                await context.SaveChangesAsync();

            return updatedCount;
        }
    }
}
