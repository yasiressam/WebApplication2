namespace WebApplication2.Services
{
    public interface IOtpService
    {
        Task<(bool Success, string Message, string Code)> GenerateAndSendOtp(string phoneNumber);
        Task<(bool Success, string Message, string Code)> SendResetPasswordCodeAsync(string phoneNumber);
        Task<(bool Success, string Message)> ValidateOtpAsync(string phoneNumber, string enteredCode);
    }
}
