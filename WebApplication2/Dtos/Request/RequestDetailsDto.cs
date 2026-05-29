// DTOs/Request/RequestDetailsDto.cs
using System;
using System.Collections.Generic;
using WebApplication2.Models.Request;

namespace WebApplication2.DTOs.Request
{
    public class RecipientInfoDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public bool HasResponded { get; set; }
        public DateTime? RespondedAt { get; set; }
    }

    public class RequestDetailsDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public RequestStatus Status { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }

        public string SenderId { get; set; } = string.Empty;
        public string SenderEmail { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string SenderRole { get; set; } = string.Empty;

        public List<RecipientInfoDto> Recipients { get; set; } = new List<RecipientInfoDto>();
        public List<ReplyInfoDto> Replies { get; set; } = new List<ReplyInfoDto>();

        // للتوافق مع الإصدارات القديمة
        public string? AdminResponse { get; set; }
        public string? Notes { get; set; }
        public string? ProcessorId { get; set; }
        public string? ProcessorName { get; set; }
        public string? ProcessorEmail { get; set; }
        public string? ProcessorRole { get; set; }

        public string CreatedAtFormatted => CreatedAt.ToString("yyyy/MM/dd HH:mm");
        public string? ProcessedAtFormatted => ProcessedAt?.ToString("yyyy/MM/dd HH:mm");
    }
}