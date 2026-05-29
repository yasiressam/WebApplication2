// DTOs/Request/ReplyInfoDto.cs
using System;

namespace WebApplication2.DTOs.Request
{
    /// <summary>
    /// معلومات الرد (للعرض)
    /// </summary>
    public class ReplyInfoDto
    {
        public int Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string UserRole { get; set; } = string.Empty;
        public string Reply { get; set; } = string.Empty;
        public DateTime RepliedAt { get; set; }
        public string? Notes { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public string RepliedAtFormatted => RepliedAt.ToString("yyyy/MM/dd HH:mm");
    }
}