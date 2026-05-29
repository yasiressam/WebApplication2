// DTOs/News/CreateNewsDto.cs
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.DTOs.News
{
    /// <summary>
    /// بيانات إنشاء خبر جديد
    /// </summary>
    public class CreateNewsDto
    {
        [Required(ErrorMessage = "العنوان مطلوب")]
        [Display(Name = "عنوان الخبر")]
        [StringLength(200, ErrorMessage = "العنوان لا يزيد عن 200 حرف")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "المحتوى مطلوب")]
        [Display(Name = "محتوى الخبر")]
        public string Content { get; set; } = string.Empty;

        [Display(Name = "صورة الخبر")]
        public string? ImageUrl { get; set; }

        // صورة يتم رفعها (للاستخدام مع IFormFile)
        public Microsoft.AspNetCore.Http.IFormFile? ImageFile { get; set; }
    }
}