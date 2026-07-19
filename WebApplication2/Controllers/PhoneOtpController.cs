using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using WebApplication2.Services;

namespace WebApplication2.Controllers
{
    public class PhoneOtpController : Controller
    {
        private readonly IOtpService _otpService;

        public PhoneOtpController(IOtpService otpService)
        {
            _otpService = otpService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendOtp(string countryCode, string phoneNumber)
        {
            try
            {
                if (string.IsNullOrEmpty(phoneNumber))
                {
                    TempData["ErrorMessage"] = "رقم الهاتف مطلوب";
                    return RedirectToAction("Index");
                }

                phoneNumber = Regex.Replace(phoneNumber, @"[^0-9]", "");

                if (phoneNumber.StartsWith("0"))
                {
                    phoneNumber = phoneNumber.Substring(1);
                }

                if (string.IsNullOrEmpty(phoneNumber) || phoneNumber.Length < 9)
                {
                    TempData["ErrorMessage"] = "رقم الهاتف غير صحيح";
                    return RedirectToAction("Index");
                }

                var fullPhoneNumber = countryCode + phoneNumber;
                var result = await _otpService.GenerateAndSendOtp(fullPhoneNumber);

                if (result.Success)
                {
                    TempData["PhoneNumber"] = fullPhoneNumber;
                    TempData["OtpCode"] = result.Code;
                    return RedirectToAction("Verify");
                }

                TempData["ErrorMessage"] = result.Message;
                return RedirectToAction("Index");
            }
            catch
            {
                TempData["ErrorMessage"] = "حدث خطأ. يرجى المحاولة مرة أخرى.";
                return RedirectToAction("Index");
            }
        }

        [HttpGet]
        public IActionResult Verify()
        {
            var phoneNumber = TempData["PhoneNumber"] as string;

            if (string.IsNullOrEmpty(phoneNumber))
            {
                return RedirectToAction("Index");
            }

            ViewBag.PhoneNumber = phoneNumber;
            ViewBag.OtpCode = TempData["OtpCode"];
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyOtp(string otpCode)
        {
            var phoneNumber = TempData["PhoneNumber"] as string;

            if (string.IsNullOrEmpty(phoneNumber))
            {
                TempData["ErrorMessage"] = "انتهت الجلسة";
                return RedirectToAction("Index");
            }

            if (string.IsNullOrEmpty(otpCode))
            {
                TempData["ErrorMessage"] = "كود التحقق مطلوب";
                return RedirectToAction("Verify");
            }

            var result = await _otpService.ValidateOtpAsync(phoneNumber, otpCode);
            if (result.Success)
            {
                TempData["VerifiedPhone"] = phoneNumber;
                return RedirectToAction("Index", "Register");
            }

            TempData["ErrorMessage"] = result.Message;
            return RedirectToAction("Verify");
        }
    }
}
