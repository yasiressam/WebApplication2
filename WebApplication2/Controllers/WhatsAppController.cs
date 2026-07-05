using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication2.Services;

[Authorize(Roles = "SuperAdmin")]
public class WhatsAppController : Controller
{
    private readonly IWhatsAppService _whatsAppService;

    public WhatsAppController(IWhatsAppService whatsAppService)
    {
        _whatsAppService = whatsAppService;
    }

    public IActionResult Send(string phone)
    {
        ViewBag.Phone = phone;
        ViewBag.Result = null;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Send(string phone, string code)
    {
        try
        {
            var message = string.IsNullOrWhiteSpace(code)
                ? "رسالة اختبار من خدمة واتساب."
                : $"رمز التحقق الخاص بك هو: {code}";

            var sent = await _whatsAppService.SendMessageAsync(phone, message);
            ViewBag.Result = sent
                ? "تم إرسال رسالة واتساب بنجاح."
                : "فشل إرسال رسالة واتساب. تحقق من إعدادات الخدمة وسجل التطبيق.";
        }
        catch (Exception ex)
        {
            ViewBag.Result = $"حدث خطأ: {ex.Message}";
        }

        ViewBag.Phone = phone;
        return View();
    }
}
