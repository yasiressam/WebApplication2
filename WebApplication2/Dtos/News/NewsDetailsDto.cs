// DTOs/News/NewsDetailsDto.cs
using System;

namespace WebApplication2.DTOs.News
{
    /// <summary>
    /// تفاصيل الخبر الكاملة (لصفحة القراءة)
    /// </summary>
    public class NewsDetailsDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public string? AuthorId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string CreatedAtFormatted => CreatedAt.ToString("yyyy/MM/dd HH:mm");
        public string? UpdatedAtFormatted => UpdatedAt?.ToString("yyyy/MM/dd HH:mm");
    }
}