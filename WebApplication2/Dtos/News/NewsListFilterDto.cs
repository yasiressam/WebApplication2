// DTOs/News/NewsListFilterDto.cs
namespace WebApplication2.DTOs.News
{
    /// <summary>
    /// فلترة وترتيب قائمة الأخبار
    /// </summary>
    public class NewsListFilterDto
    {
        public string? SearchTerm { get; set; }          // بحث بالعنوان أو المحتوى
        public string? AuthorId { get; set; }            // فلتر بواسطة الكاتب
        public DateTime? FromDate { get; set; }          // من تاريخ
        public DateTime? ToDate { get; set; }            // إلى تاريخ
        public string? SortBy { get; set; } = "CreatedAt"; // الترتيب
        public bool SortDescending { get; set; } = true;   // تنازلي
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    /// <summary>
    /// نتيجة البحث (مع معلومات الصفحات)
    /// </summary>
    public class PagedNewsResult
    {
        public List<NewsDto> News { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;
    }
}