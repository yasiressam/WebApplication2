using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class SiteSettings
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = "البريد الإلكتروني")]
        [EmailAddress(ErrorMessage = "الرجاء إدخال بريد إلكتروني صحيح")]
        public string ContactEmail { get; set; } = "info@iraqinews.com";

        [Display(Name = "رقم الهاتف")]
        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        public string ContactPhone { get; set; } = "+964 770 000 0000";

        [Display(Name = "العنوان")]
        public string? SiteAddress { get; set; } = "العراق - بغداد";

        [Display(Name = "وصف الموقع")]
        [Required(ErrorMessage = "وصف الموقع مطلوب")]
        public string SiteDescription { get; set; } = "منصة إلكترونية متكاملة تهدف إلى توفير خدمات إلكترونية للمواطنين العراقيين بأسلوب عصري وسهل.";

        // وسائل التواصل الاجتماعي (الثلاثة فقط)
        [Display(Name = "رابط فيسبوك")]
        [Url(ErrorMessage = "الرجاء إدخال رابط صحيح")]
        public string? FacebookUrl { get; set; } = "";

        [Display(Name = "رابط إنستغرام")]
        [Url(ErrorMessage = "الرجاء إدخال رابط صحيح")]
        public string? InstagramUrl { get; set; } = "";

        [Display(Name = "رقم واتساب")]
        [RegularExpression(@"^\+?[0-9\s\-\(\)]+$", ErrorMessage = "الرجاء إدخال رقم واتساب صحيح")]
        public string? WhatsAppNumber { get; set; } = "";

        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }
}