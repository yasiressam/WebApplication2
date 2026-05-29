namespace WebApplication2.Models
{
    public class RequestHistoryViewModel
    {
        public string RequestType { get; set; } = "promotion";
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string BackAction { get; set; } = string.Empty;
        public string BackController { get; set; } = string.Empty;
        public string DetailsAction { get; set; } = "Details";
        public string DetailsController { get; set; } = string.Empty;
        public List<RequestHistoryItemViewModel> Items { get; set; } = new();
        public string? SearchName { get; set; }
        public string? SearchGovernorate { get; set; }
        public string? SearchPhone { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalItems { get; set; }
        public int TotalPages { get; set; } = 1;
        public int[] PageSizeOptions { get; set; } = [10, 25, 50, 100];
        public List<string> GovernorateOptions { get; set; } = new();
    }

    public class RequestHistoryItemViewModel
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Governorate { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusClass { get; set; } = "secondary";
        public DateTime RequestDate { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public string? ProcessedBy { get; set; }
        public string? Reason { get; set; }
        public string? CoverImage { get; set; }
    }
}
