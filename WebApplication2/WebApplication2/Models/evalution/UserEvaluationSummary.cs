namespace WebApplication2.Models
{
    public class UserEvaluationSummary
    {
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Governorate { get; set; } = string.Empty;

        // درجات العوامل
        public int CommunicationScore { get; set; }      // 1-12
        public int MediaActivityScore { get; set; }      // 1-12
        public int MovementActivityScore { get; set; }   // 1-12
        public int PolarizationScore { get; set; }       // 1-12
        public int SocialMediaScore { get; set; }        // 1-12
        public int SupervisorOpinionScore { get; set; }  // 1-16
        public double PoliticalForumScore { get; set; }  // 0-12
        public double PeriodicMeetingsScore { get; set; } // 0-12

        // المجموع والنسبة
        public double TotalScore =>
            CommunicationScore + MediaActivityScore + MovementActivityScore +
            PolarizationScore + SocialMediaScore + SupervisorOpinionScore +
            PoliticalForumScore + PeriodicMeetingsScore;

        public double Percentage => TotalScore; // لأن المجموع من 100

        public string Grade => Percentage >= 90 ? "ممتاز" :
                               Percentage >= 80 ? "جيد جداً" :
                               Percentage >= 70 ? "جيد" :
                               Percentage >= 60 ? "مقبول" : "ضعيف";
    }
}