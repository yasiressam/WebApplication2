using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;

namespace WebApplication2.Models
{
    public class clsEmailConfirm : IEmailSender
    {
        private readonly IConfiguration _configuration;

        public clsEmailConfirm(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // قراءة بيانات الإيميل من appsettings.json بشكل آمن
            var fMail = _configuration["EmailSettings:Email"];
            var fPassword = _configuration["EmailSettings:Password"];

            if (string.IsNullOrEmpty(fMail) || string.IsNullOrEmpty(fPassword))
            {
                throw new InvalidOperationException("Email settings are not configured properly.");
            }

            var theMsg = new MailMessage();
            theMsg.From = new MailAddress(fMail);
            theMsg.Subject = subject;
            theMsg.To.Add(email);
            theMsg.Body = $"<html><body>{htmlMessage}</body></html>";
            theMsg.IsBodyHtml = true;

            var smtpClient = new SmtpClient("smtp.gmail.com")
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(fMail, fPassword),
                Port = 587
            };

            await smtpClient.SendMailAsync(theMsg);
        }
    }
}