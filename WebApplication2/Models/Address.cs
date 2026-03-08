namespace WebApplication2.Models
{
    public class Address
    {
        public int Id { get; set; }

        public string Governorate { get; set; } = "بغداد";  // قيمة افتراضية

        public string District { get; set; } = string.Empty;
        public string SubDistrict { get; set; } = string.Empty;
        public string Alley { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public string House { get; set; } = string.Empty;
        public string NearestPoint { get; set; } = string.Empty;

        // العلاقة العكسية مع Identify
        public virtual Identify? Identify { get; set; }
    }
}