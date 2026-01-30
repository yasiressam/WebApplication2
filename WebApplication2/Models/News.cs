using System;
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class News
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "العنوان مطلوب")]
        [Display(Name = "عنوان الخبر")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "المحتوى مطلوب")]
        [Display(Name = "محتوى الخبر")]
        public string Content { get; set; } = string.Empty;

        [Display(Name = "صورة الخبر")]
        public string? ImageUrl { get; set; }

        [Display(Name = "معرف الكاتب")]
        public string? AuthorId { get; set; }

        [Display(Name = "اسم الكاتب")]
        public string? AuthorName { get; set; }

        [Display(Name = "تاريخ الإنشاء")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "تاريخ التعديل")]
        public DateTime? UpdatedAt { get; set; }
    }
}