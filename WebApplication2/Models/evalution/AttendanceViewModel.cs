using WebApplication2.Models;  // ✅ أضف هذا السطر

namespace WebApplication2.Models.ViewModels
{
    public class AttendanceViewModel
    {
        public int Month { get; set; } = DateTime.Now.Month;
        public int Year { get; set; } = DateTime.Now.Year;
        public List<Event> Events { get; set; } = new();  // ✅ الآن لن يظهر خطأ
    }

    public class SaveAttendanceRequest
    {
        public int EventId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public bool Attended { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
    }
}