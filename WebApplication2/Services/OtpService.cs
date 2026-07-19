using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using WebApplication2.Models;

namespace WebApplication2.Services
{
    public class OtpService : IOtpService
    {
        private readonly IMemoryCache _cache;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly OtpApiSettings _settings;
        private readonly ILogger<OtpService> _logger;
        private readonly TimeSpan _otpExpiry = TimeSpan.FromMinutes(10);

        public OtpService(
            IMemoryCache cache,
            IHttpClientFactory httpClientFactory,
            IOptions<OtpApiSettings> settings,
            ILogger<OtpService> logger)
        {
            _cache = cache;
            _httpClientFactory = httpClientFactory;
            _settings = settings.Value;
            _logger = logger;
        }

        public Task<(bool Success, string Message, string Code)> GenerateAndSendOtp(string phoneNumber)
        {
            return SendCodeAsync(_settings.ApiUrl, NormalizePhoneNumber(phoneNumber), "otpCode");
        }

        public Task<(bool Success, string Message, string Code)> SendResetPasswordCodeAsync(string phoneNumber)
        {
            return SendCodeAsync(_settings.ResetPasswordUrl, NormalizePhoneNumber(phoneNumber), "resetCode");
        }

        public async Task<(bool Success, string Message)> ValidateOtpAsync(string phoneNumber, string enteredCode)
        {
            var normalizedPhone = NormalizePhoneNumber(phoneNumber);
            if (string.IsNullOrWhiteSpace(normalizedPhone) || string.IsNullOrWhiteSpace(enteredCode))
            {
                return (false, "رقم الهاتف أو الكود غير صالح");
            }

            if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                _logger.LogError("OTP API key is missing.");
                return (false, "إعدادات خدمة OTP غير مكتملة");
            }

            if (!_cache.TryGetValue(BuildMessageIdCacheKey(normalizedPhone), out string? messageId) ||
                string.IsNullOrWhiteSpace(messageId))
            {
                return (false, "لم يتم العثور على طلب تحقق صالح لهذا الرقم");
            }

            var payload = new Dictionary<string, string>
            {
                ["messageId"] = messageId,
                ["code"] = enteredCode
            };

            try
            {
                var responseText = await SendJsonAsync(_settings.VerifyUrl, payload);
                var verifyResponse = Deserialize<VerifyResponse>(responseText);

                if (verifyResponse?.Verified == true || verifyResponse?.Success == true)
                {
                    _cache.Remove(BuildMessageIdCacheKey(normalizedPhone));
                    return (true, verifyResponse.Message ?? "تم التحقق بنجاح");
                }

                return (false, verifyResponse?.Message ?? "كود التحقق غير صحيح أو منتهي الصلاحية");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "OTP verification failed for {PhoneNumber}.", normalizedPhone);
                return (false, ex.Message);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogError(ex, "Error while verifying OTP through configured provider.");
                return (false, "تعذر الاتصال بخدمة التحقق");
            }
        }

        private async Task<(bool Success, string Message, string Code)> SendCodeAsync(
            string endpoint,
            string normalizedPhone,
            string codePropertyName)
        {
            if (string.IsNullOrWhiteSpace(normalizedPhone))
            {
                return (false, "رقم الهاتف غير صالح", string.Empty);
            }

            if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                _logger.LogError("OTP API key is missing.");
                return (false, "إعدادات خدمة OTP غير مكتملة", string.Empty);
            }

            var code = Random.Shared.Next(100000, 999999).ToString();
            var payload = new Dictionary<string, string>
            {
                ["phoneNumber"] = ToE164PhoneNumber(normalizedPhone),
                [codePropertyName] = code
            };

            try
            {
                var responseText = await SendJsonAsync(endpoint, payload);
                var sendResponse = Deserialize<SendResponse>(responseText);

                if (sendResponse == null ||
                    string.IsNullOrWhiteSpace(sendResponse.MessageId) ||
                    (sendResponse.Success.HasValue && !sendResponse.Success.Value))
                {
                    return (false, sendResponse?.Message ?? "فشل إرسال كود التحقق", string.Empty);
                }

                _cache.Set(BuildMessageIdCacheKey(normalizedPhone), sendResponse.MessageId, _otpExpiry);
                _logger.LogInformation("OTP sent successfully to {PhoneNumber}.", normalizedPhone);
                return (true, sendResponse.Message ?? "تم إرسال كود التحقق بنجاح", code);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "OTP sending failed for {PhoneNumber}.", normalizedPhone);
                return (false, ex.Message, string.Empty);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogError(ex, "Error while sending OTP through configured provider.");
                return (false, "تعذر الاتصال بخدمة OTP", string.Empty);
            }
        }

        private async Task<string> SendJsonAsync(string endpoint, object payload)
        {
            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Add("X-API-Key", _settings.ApiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(request);
            var responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(responseText)
                    ? "فشل الاتصال بمزود OTP"
                    : responseText);
            }

            return responseText;
        }

        private static string BuildMessageIdCacheKey(string normalizedPhone)
        {
            return $"OtpMessageId_{normalizedPhone}";
        }

        private static T? Deserialize<T>(string responseText)
        {
            if (string.IsNullOrWhiteSpace(responseText))
                return default;

            return JsonSerializer.Deserialize<T>(responseText, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        private static string NormalizePhoneNumber(string phoneNumber)
        {
            return new string((phoneNumber ?? string.Empty).Where(char.IsDigit).ToArray());
        }

        private static string ToE164PhoneNumber(string normalizedPhone)
        {
            return string.IsNullOrWhiteSpace(normalizedPhone)
                ? string.Empty
                : $"+{normalizedPhone}";
        }

        private sealed class SendResponse
        {
            public bool? Success { get; set; }
            public string? Message { get; set; }
            public string? MessageId { get; set; }
        }

        private sealed class VerifyResponse
        {
            public bool Verified { get; set; }
            public bool? Success { get; set; }
            public string? Message { get; set; }
        }
    }
}
