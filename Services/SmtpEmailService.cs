using System.Net;
using System.Net.Mail;

namespace DACS_Nhom3.Services
{
    public class SmtpEmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public SmtpEmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            var host = _configuration["EmailSettings:SmtpHost"];
            var portText = _configuration["EmailSettings:SmtpPort"];
            var username = _configuration["EmailSettings:Username"];
            var password = _configuration["EmailSettings:Password"];
            var fromEmail = _configuration["EmailSettings:FromEmail"] ?? username;
            var fromName = _configuration["EmailSettings:FromName"] ?? "DACS_Nhóm3";
            var enableSsl = bool.TryParse(_configuration["EmailSettings:EnableSsl"], out var ssl) && ssl;

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(portText) ||
                string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(fromEmail))
            {
                throw new InvalidOperationException("Chưa cấu hình EmailSettings trong appsettings.json.");
            }

            if (!int.TryParse(portText, out var port))
            {
                throw new InvalidOperationException("EmailSettings:SmtpPort không hợp lệ.");
            }

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = enableSsl,
                Credentials = new NetworkCredential(username, password)
            };

            using var message = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            await client.SendMailAsync(message);
        }
    }
}
