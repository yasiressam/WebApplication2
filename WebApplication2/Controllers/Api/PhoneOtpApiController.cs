using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using WebApplication2.Services;

namespace WebApplication2.Controllers.Api
{
    [Route("api/phone-otp")]
    [ApiController]
    [AllowAnonymous]
    public class PhoneOtpApiController : ControllerBase
    {
        private readonly IOtpService _otpService;

        public PhoneOtpApiController(IOtpService otpService)
        {
            _otpService = otpService;
        }

        [HttpPost("send")]
        public async Task<IActionResult> SendOtp([FromBody] SendPhoneOtpRequest request)
        {
            var normalizedPhone = NormalizePhoneNumber(request.CountryCode, request.PhoneNumber);

            if (normalizedPhone == null)
            {
                return BadRequest(new { success = false, message = "رقم الهاتف غير صحيح" });
            }

            var result = await _otpService.GenerateAndSendOtp(normalizedPhone);

            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.Message });
            }

            return Ok(new
            {
                success = true,
                message = result.Message,
                phoneNumber = normalizedPhone,
                otpCode = result.Code
            });
        }

        [HttpPost("verify")]
        public IActionResult VerifyOtp([FromBody] VerifyPhoneOtpRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                return BadRequest(new { success = false, message = "رقم الهاتف مطلوب" });
            }

            if (string.IsNullOrWhiteSpace(request.OtpCode))
            {
                return BadRequest(new { success = false, message = "كود التحقق مطلوب" });
            }

            if (_otpService.ValidateOtp(request.PhoneNumber, request.OtpCode))
            {
                return Ok(new
                {
                    success = true,
                    message = "تم التحقق من رقم الهاتف بنجاح",
                    verifiedPhone = request.PhoneNumber
                });
            }

            return BadRequest(new { success = false, message = "كود غير صحيح" });
        }

        private static string? NormalizePhoneNumber(string? countryCode, string? phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                return null;
            }

            var cleanPhoneNumber = Regex.Replace(phoneNumber, @"[^0-9]", "");

            if (cleanPhoneNumber.StartsWith("0"))
            {
                cleanPhoneNumber = cleanPhoneNumber.Substring(1);
            }

            if (cleanPhoneNumber.Length < 9)
            {
                return null;
            }

            var cleanCountryCode = string.IsNullOrWhiteSpace(countryCode)
                ? string.Empty
                : Regex.Replace(countryCode, @"[^0-9+]", "");

            return cleanCountryCode + cleanPhoneNumber;
        }
    }

    public class SendPhoneOtpRequest
    {
        public string? CountryCode { get; set; }
        public string? PhoneNumber { get; set; }
    }

    public class VerifyPhoneOtpRequest
    {
        public string? PhoneNumber { get; set; }
        public string? OtpCode { get; set; }
    }
}
