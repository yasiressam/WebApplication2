using Microsoft.Extensions.Caching.Memory;

namespace WebApplication2.Services
{
    public class OtpService : IOtpService
    {
        private readonly IMemoryCache _cache;
        private readonly TimeSpan _otpExpiry = TimeSpan.FromMinutes(5);

        public OtpService(IMemoryCache cache)
        {
            _cache = cache;
        }

        public async Task<(bool Success, string Message, string Code)> GenerateAndSendOtp(string phoneNumber)
        {
            // محاكاة عملية قصيرة (بدون أي اتصال خارجي)
            await Task.Delay(500); // نصف ثانية فقط

            // تنظيف الرقم
            var normalizedPhone = phoneNumber.Replace(" ", "").Replace("-", "").Replace("+", "");

            // توليد كود عشوائي
            var random = new Random();
            var otpCode = random.Next(100000, 999999).ToString();

            // تخزين الكود
            StoreOtp(normalizedPhone, otpCode);

            // عرض الكود في الكونسول
            System.Diagnostics.Debug.WriteLine($"🔐 كود التحقق: {otpCode}");
            Console.WriteLine($"🔐 كود التحقق: {otpCode}");

            return (true, "تم إرسال كود التحقق بنجاح", otpCode);
        }

        public bool ValidateOtp(string phoneNumber, string enteredCode)
        {
            var normalizedPhone = phoneNumber.Replace(" ", "").Replace("-", "").Replace("+", "");
            var cacheKey = $"Otp_{normalizedPhone}";

            if (_cache.TryGetValue(cacheKey, out string storedCode))
            {
                return storedCode == enteredCode;
            }

            return false;
        }

        public void StoreOtp(string phoneNumber, string code)
        {
            var normalizedPhone = phoneNumber.Replace(" ", "").Replace("-", "").Replace("+", "");
            var cacheKey = $"Otp_{normalizedPhone}";
            _cache.Set(cacheKey, code, _otpExpiry);
        }
    }
}