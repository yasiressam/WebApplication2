using Microsoft.AspNetCore.Mvc;
using WebApplication2.Services;
using System.Text.RegularExpressions;

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
                // التحقق من المدخلات
                if (string.IsNullOrEmpty(phoneNumber))
                {
                    TempData["ErrorMessage"] = "رقم الهاتف مطلوب";
                    return RedirectToAction("Index");
                }

                // تنظيف الرقم
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

                // استخدام Task.Run لمنع التجميد
                var result = await Task.Run(async () =>
                    await _otpService.GenerateAndSendOtp(fullPhoneNumber)
                ).ConfigureAwait(false);

                if (result.Success)
                {
                    TempData["PhoneNumber"] = fullPhoneNumber;
                    TempData["OtpCode"] = result.Code;
                    return RedirectToAction("Verify");
                }
                else
                {
                    TempData["ErrorMessage"] = result.Message;
                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
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
        public IActionResult VerifyOtp(string otpCode)
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

            if (_otpService.ValidateOtp(phoneNumber, otpCode))
            {
                TempData["VerifiedPhone"] = phoneNumber;
                return RedirectToAction("Index", "Register");
            }

            TempData["ErrorMessage"] = "كود غير صحيح";
            return RedirectToAction("Verify");
        }
    }
}