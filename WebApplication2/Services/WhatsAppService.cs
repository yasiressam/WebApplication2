using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;
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
                _logger.LogError("WhatsApp API settings are missing.");
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
                using var content = new MultipartFormDataContent
                {
                    { new StringContent(_settings.AppKey), "appkey" },
                    { new StringContent(_settings.AuthKey), "authkey" },
                    { new StringContent(normalizedPhone), "to" },
                    { new StringContent(message), "message" }
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, _settings.ApiUrl)
                {
                    Content = content
                };

                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                using var response = await client.SendAsync(request, cancellationToken);
                var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode && LooksSuccessful(responseText))
                {
                    _logger.LogInformation("WhatsApp message sent to {PhoneNumber}.", normalizedPhone);
                    return true;
                }

                _logger.LogError(
                    "WhatsApp API failed with status {StatusCode}: {Response}",
                    response.StatusCode,
                    responseText);
                return false;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogError(ex, "Error while sending WhatsApp message.");
                return false;
            }
        }

        private static string NormalizeIraqPhoneNumber(string phoneNumber)
        {
            var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());
            if (string.IsNullOrWhiteSpace(digits))
                return string.Empty;

            if (digits.StartsWith("00"))
                digits = digits[2..];

            if (digits.StartsWith("964"))
                return digits;

            if (digits.StartsWith("0"))
                return "964" + digits[1..];

            if (digits.StartsWith("7"))
                return "964" + digits;

            return digits;
        }

        private static bool LooksSuccessful(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
                return true;

            var body = responseBody.Trim();

            if (TryReadSuccessFlag(body, out var success))
                return success;

            return !body.Contains("\"status\":false", StringComparison.OrdinalIgnoreCase)
                && !body.Contains("\"success\":false", StringComparison.OrdinalIgnoreCase)
                && !body.Contains("\"error\":true", StringComparison.OrdinalIgnoreCase)
                && !body.Contains("\"error\":\"", StringComparison.OrdinalIgnoreCase)
                && !body.Contains("failed", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryReadSuccessFlag(string responseBody, out bool success)
        {
            success = false;

            try
            {
                using var document = JsonDocument.Parse(responseBody);
                var root = document.RootElement;

                if (TryGetBoolean(root, "success", out success))
                    return true;

                if (TryGetBoolean(root, "status", out success))
                    return true;

                if (TryGetString(root, "status", out var statusText))
                {
                    success = statusText.Equals("success", StringComparison.OrdinalIgnoreCase)
                        || statusText.Equals("sent", StringComparison.OrdinalIgnoreCase)
                        || statusText.Equals("queued", StringComparison.OrdinalIgnoreCase);
                    return success
                        || statusText.Equals("failed", StringComparison.OrdinalIgnoreCase)
                        || statusText.Equals("error", StringComparison.OrdinalIgnoreCase);
                }

                if (TryGetString(root, "message_status", out var messageStatus))
                {
                    success = messageStatus.Equals("success", StringComparison.OrdinalIgnoreCase)
                        || messageStatus.Equals("sent", StringComparison.OrdinalIgnoreCase)
                        || messageStatus.Equals("queued", StringComparison.OrdinalIgnoreCase);
                    return true;
                }
            }
            catch (JsonException)
            {
                return false;
            }

            return false;
        }

        private static bool TryGetBoolean(JsonElement root, string propertyName, out bool value)
        {
            value = false;

            if (!root.TryGetProperty(propertyName, out var property))
                return false;

            if (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False)
            {
                value = property.GetBoolean();
                return true;
            }

            if (property.ValueKind == JsonValueKind.String &&
                bool.TryParse(property.GetString(), out value))
            {
                return true;
            }

            return false;
        }

        private static bool TryGetString(JsonElement root, string propertyName, out string value)
        {
            value = string.Empty;

            if (!root.TryGetProperty(propertyName, out var property) ||
                property.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            value = property.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }
    }
}
