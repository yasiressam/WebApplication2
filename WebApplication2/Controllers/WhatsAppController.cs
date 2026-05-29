using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;

[Authorize(Roles = "SuperAdmin")]
public class WhatsAppController : Controller
{
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
            using (var client = new HttpClient())
            {
                var url = "https://api.verifyway.com/api/v1/"; // endpoint OTP

                var data = new
                {
                    recipient = phone,
                    type = "otp",
                    code = code,
                    channel = "whatsapp"
                };

                var json = JsonConvert.SerializeObject(data);

                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("Authorization", "Bearer 1987$e4VZ9gcP8PW78o3EYdE93uovX298AIRnXk9G"); // ضع مفتاحك هنا
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.SendAsync(request);
                var resultContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    ViewBag.Result = $"تم إرسال OTP بنجاح!\nResponse: {resultContent}";
                }
                else
                {
                    ViewBag.Result = $"فشل الإرسال.\nHTTP Status: {response.StatusCode}\nResponse: {resultContent}";
                }
            }
        }
        catch (Exception ex)
        {
            ViewBag.Result = $"حدث خطأ: {ex.Message}";
        }

        ViewBag.Phone = phone;
        return View();
    }
}
