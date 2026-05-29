// DTOs/News/NewsDto.cs
using System;

namespace WebApplication2.DTOs.News
{
    /// <summary>
    /// عرض أساسي للخبر (للقوائم)
    /// </summary>
    public class NewsDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ShortContent { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public string? AuthorId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedAtFormatted => CreatedAt.ToString("yyyy/MM/dd HH:mm");
    }
}