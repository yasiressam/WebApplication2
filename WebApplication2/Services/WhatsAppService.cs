using Microsoft.Extensions.Options;
using WebApplication2.Models;

namespace WebApplication2.Services
{
    public class WhatsAppService : IWhatsAppService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly WhatsAppApiSettings _settings;
        private readonly ILogger<WhatsAppService> _logger;

        public WhatsAppService(
            IHttpClientFactory httpClientFactory,
            IOptions<WhatsAppApiSettings> settings,
            ILogger<WhatsAppService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<bool> SendMessageAsync(
            string phoneNumber,
            string message,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_settings.ApiUrl) ||
                string.IsNullOrWhiteSpace(_settings.AuthKey) ||
                string.IsNullOrWhiteSpace(_settings.AppKey))
            {
                _logger.LogError("WaSender settings are missing.");
                return false;
            }

            var normalizedPhone = NormalizeIraqPhoneNumber(phoneNumber);
            if (string.IsNullOrWhiteSpace(normalizedPhone))
            {
                _logger.LogWarning("WhatsApp message skipped because phone number is empty.");
                return false;
            }

            try
            {
                var client = _httpClientFactory.CreateClient();
                using var formData = new MultipartFormDataContent
                {
                    { new StringContent(_settings.AppKey), "appkey" },
                    { new StringContent(_settings.AuthKey), "authkey" },
                    { new StringContent(normalizedPhone), "to" },
                    { new StringContent(message), "message" }
                };

                using var response = await client.PostAsync(_settings.ApiUrl, formData, cancellationToken);
                var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode && LooksSuccessful(responseText))
                {
                    _logger.LogInformation("WaSender message sent to {PhoneNumber}.", normalizedPhone);
                    return true;
                }

                _logger.LogError(
                    "WaSender failed with status {StatusCode}: {Response}",
                    response.StatusCode,
                    responseText);
                return false;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogError(ex, "Error while sending WhatsApp message through WaSender.");
                return false;
            }
        }

        private static string NormalizeIraqPhoneNumber(string phoneNumber)
        {
            var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());
            if (string.IsNullOrWhiteSpace(digits))
                return string.Empty;

            if (digits.StartsWith("00", StringComparison.Ordinal))
                digits = digits[2..];

            if (digits.StartsWith("964", StringComparison.Ordinal))
                return digits;

            if (digits.StartsWith("0", StringComparison.Ordinal))
                return "964" + digits[1..];

            if (digits.StartsWith("7", StringComparison.Ordinal))
                return "964" + digits;

            return digits;
        }

        private static bool LooksSuccessful(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
                return true;

            var body = responseBody.Trim();

            return !body.Contains("\"status\":false", StringComparison.OrdinalIgnoreCase)
                && !body.Contains("\"success\":false", StringComparison.OrdinalIgnoreCase)
                && !body.Contains("\"error\":true", StringComparison.OrdinalIgnoreCase)
                && !body.Contains("\"errors\":", StringComparison.OrdinalIgnoreCase)
                && !body.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)
                && !body.Contains("invalid", StringComparison.OrdinalIgnoreCase)
                && !body.Contains("failed", StringComparison.OrdinalIgnoreCase);
        }
    }
}
