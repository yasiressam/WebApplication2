// DTOs/News/UpdateNewsDto.cs
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.DTOs.News
{
    /// <summary>
    /// بيانات تحديث الخبر
    /// </summary>
    public class UpdateNewsDto
    {
        [StringLength(200, ErrorMessage = "العنوان لا يزيد عن 200 حرف")]
        [Display(Name = "عنوان الخبر")]
        public string? Title { get; set; }

        [Display(Name = "محتوى الخبر")]
        public string? Content { get; set; }

        [Display(Name = "صورة الخبر الحالية")]
        public string? ImageUrl { get; set; }

        [Display(Name = "رفع صورة جديدة")]
        public IFormFile? ImageFile { get; set; }

        [Display(Name = "حذف الصورة الحالية")]
        public bool RemoveImage { get; set; } = false;
    }
}