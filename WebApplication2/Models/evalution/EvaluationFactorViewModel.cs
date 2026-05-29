namespace WebApplication2.Models.ViewModels
{
    public class EvaluationFactorViewModel
    {
        public string FactorName { get; set; } = string.Empty;
        public string FactorDisplayName { get; set; } = string.Empty;
        public int MaxScore { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int TotalUsers { get; set; }
        public int PageSize { get; set; } = 10;
        public List<UserFactorScoreVM> Users { get; set; } = new();
    }

    public class UserFactorScoreVM
    {
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Governorate { get; set; } = string.Empty;
        public int CurrentScore { get; set; }
        public int MaxScore { get; set; }
    }
}
