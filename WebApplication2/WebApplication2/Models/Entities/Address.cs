using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class Address
    {
        [Key]
        public int Id { get; set; }

        // ===== معلومات الموقع الأساسية =====
        public string Governorate { get; set; } = string.Empty;  // المحافظة
        public string District { get; set; } = string.Empty;     // القضاء
        public string Area { get; set; } = string.Empty;         // المنطقة (بدلاً من SubDistrict)

        // ===== معلومات التفاصيل =====
        public string Alley { get; set; } = string.Empty;        // المحلة
        public string Street { get; set; } = string.Empty;       // الزقاق
        public string House { get; set; } = string.Empty;        // الدار
        public string NearestPoint { get; set; } = string.Empty; // أقرب نقطة دالة

        // رابط المستخدم
        public string UserId { get; set; } = string.Empty;
    }
}