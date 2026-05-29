// DTOs/Request/CreateRequestDto.cs
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.DTOs.Request
{
    /// <summary>
    /// بيانات إنشاء طلب جديد
    /// </summary>
    public class CreateRequestDto
    {
        [Required(ErrorMessage = "العنوان مطلوب")]
        [Display(Name = "عنوان الطلب")]
        [StringLength(200, ErrorMessage = "العنوان لا يزيد عن 200 حرف")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "المحتوى مطلوب")]
        [Display(Name = "محتوى الطلب")]
        public string Content { get; set; } = string.Empty;

        [Required(ErrorMessage = "الرجاء اختيار مستلم واحد على الأقل")]
        [Display(Name = "المستلمون")]
        public List<string> RecipientIds { get; set; } = new List<string>();
    }
}