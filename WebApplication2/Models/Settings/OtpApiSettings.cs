namespace WebApplication2.Models
{
    public class OtpApiSettings
    {
        public string ApiUrl { get; set; } = "https://otp.arqam.tech/api/sms/otp";
        public string VerifyUrl { get; set; } = "https://otp.arqam.tech/api/sms/verify";
        public string ResetPasswordUrl { get; set; } = "https://otp.arqam.tech/api/sms/reset-password";
        public string ApiKey { get; set; } = string.Empty;
    }
}
