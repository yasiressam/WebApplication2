namespace WebApplication2.Services
{
    public interface IOtpService
    {
        Task<(bool Success, string Message, string Code)> GenerateAndSendOtp(string phoneNumber);
        bool ValidateOtp(string phoneNumber, string enteredCode);
        void StoreOtp(string phoneNumber, string code);
    }
}