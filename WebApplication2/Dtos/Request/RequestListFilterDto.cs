// DTOs/Request/RequestListFilterDto.cs
using System;
using System.Collections.Generic;
using WebApplication2.Models.Request;

namespace WebApplication2.DTOs.Request
{
    /// <summary>
    /// فلترة وترتيب قائمة الطلبات
    /// </summary>
    public class RequestListFilterDto
    {
        public string? SearchTerm { get; set; }
        public RequestStatus? Status { get; set; }
        public string? SenderId { get; set; }
        public string? RecipientId { get; set; }
        public bool? IsRead { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? SortBy { get; set; } = "CreatedAt";
        public bool SortDescending { get; set; } = true;
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    /// <summary>
    /// نتيجة البحث (مع معلومات الصفحات)
    /// </summary>
    public class PagedRequestResult
    {
        public List<RequestDto> Requests { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;

        public int PendingCount { get; set; }
        public int UnderReviewCount { get; set; }
        public int ApprovedCount { get; set; }
        public int RejectedCount { get; set; }
        public int ProcessedCount { get; set; }
        public int UnreadCount { get; set; }
    }
}