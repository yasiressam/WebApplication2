// DTOs/Request/UpdateRequestDto.cs
using System.ComponentModel.DataAnnotations;
using WebApplication2.Models.Request;

namespace WebApplication2.DTOs.Request
{
    /// <summary>
    /// بيانات تحديث الطلب (للمسؤول)
    /// </summary>
    public class UpdateRequestDto
    {
        [Display(Name = "رد الإدارة")]
        [Required(ErrorMessage = "الرد مطلوب")]
        public string AdminResponse { get; set; } = string.Empty;

        [Display(Name = "الحالة الجديدة")]
        public RequestStatus NewStatus { get; set; } = RequestStatus.Processed;

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }
    }
}