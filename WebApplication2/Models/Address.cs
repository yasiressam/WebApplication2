namespace WebApplication2.Models
{
    public class Address
    {
        public int Id { get; set; }
        public string Governorate { get; set; }
        public string District { get; set; }
        public string SubDistrict { get; set; }
        public string Alley { get; set; }
        public string Street { get; set; }
        public string House { get; set; }
        public string NearestPoint { get; set; }

        // العلاقة العكسية مع Identify
        public virtual Identify Identify { get; set; }
    }
}