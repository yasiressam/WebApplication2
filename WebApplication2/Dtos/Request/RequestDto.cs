// DTOs/Request/RequestDto.cs
using System;
using WebApplication2.Models.Request;

namespace WebApplication2.DTOs.Request
{
    /// <summary>
    /// عرض أساسي للطلب (للقوائم)
    /// </summary>
    public class RequestDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ShortContent { get; set; } = string.Empty;
        public RequestStatus Status { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string CreatedAtFormatted => CreatedAt.ToString("yyyy/MM/dd HH:mm");
        public string SenderName { get; set; } = string.Empty;
        public string RecipientName { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public bool IsUnread => !IsRead;
    }
}