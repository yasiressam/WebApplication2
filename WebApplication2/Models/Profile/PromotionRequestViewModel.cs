using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class PromotionRequestViewModel
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;

        [Display(Name = "البريد الإلكتروني")]
        public string UserEmail { get; set; } = string.Empty;

        [Display(Name = "الاسم الرباعي")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "رقم الهاتف")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Display(Name = "محافظة العمل التنظيمي")]
        public string Governorate { get; set; } = string.Empty;

        [Display(Name = "قضاء العمل التنظيمي")]
        public string District { get; set; } = string.Empty;

        // ✅ تغيير من int إلى string (لـ 12 رقم)
        [Display(Name = "رقم البطاقة الموحدة")]
        public string IdentityCardN { get; set; } = string.Empty;

        public string? AffiliationEntity { get; set; }
        public string? Division { get; set; }
        public string? Section { get; set; }
        public string? Group { get; set; }

        public string AffiliationDisplay
        {
            get
            {
                var parts = new List<string>();

                if (!string.IsNullOrWhiteSpace(AffiliationEntity)) parts.Add($"الجهة: {AffiliationEntity}");
                if (!string.IsNullOrWhiteSpace(Division)) parts.Add($"القسم: {Division}");
                if (!string.IsNullOrWhiteSpace(Section)) parts.Add($"الشعبة: {Section}");
                if (!string.IsNullOrWhiteSpace(Group)) parts.Add($"الوحدة: {Group}");

                return parts.Count > 0 ? string.Join(" - ", parts) : "---";
            }
        }

        [Display(Name = "تاريخ التقديم")]
        public DateTime RequestDate { get; set; }

        [Display(Name = "نوع الحساب")]
        public string AccountType { get; set; } = string.Empty;

        [Display(Name = "الصورة")]
        public string? CoverImage { get; set; }

        // نسبة اكتمال الملف
        public int CompletionPercentage { get; set; }
        public bool HasCompleteProfile { get; set; }

        // سبب الرفض
        public string? RejectionReason { get; set; }
    }
}
