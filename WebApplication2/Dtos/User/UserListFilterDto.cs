// DTOs/User/UserListFilterDto.cs
namespace WebApplication2.DTOs.User
{
    /// <summary>
    /// فلترة وترتيب قائمة المستخدمين
    /// </summary>
    public class UserListFilterDto
    {
        public string? SearchTerm { get; set; }           // بحث بالاسم أو الإيميل
        public string? Governorate { get; set; }           // فلتر بالمحافظة
        public string? Role { get; set; }                  // فلتر بالدور
        public string? AccountType { get; set; }           // فلتر بنوع الحساب
        public bool? IsActive { get; set; }                // فلتر بالحالة
        public bool? IsBasicInfoApproved { get; set; }     // فلتر باعتماد البيانات
        public bool? RequestedPromotion { get; set; }      // فلتر بطلب الترقية
        public bool? HasCompleteProfile { get; set; }      // فلتر باكتمال الملف
        public string? SortBy { get; set; } = "CreatedAt"; // الترتيب
        public bool SortDescending { get; set; } = true;   // تنازلي
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    /// <summary>
    /// نتيجة البحث (مع معلومات الصفحات)
    /// </summary>
    public class PagedUserResult
    {
        public List<UserDto> Users { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;
    }
}