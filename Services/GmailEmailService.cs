using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace QuanLyThuVien.Services
{
    public class GmailEmailService
    {
        private readonly EmailSettings _settings;
        private readonly ILogger<GmailEmailService> _logger;

        public GmailEmailService(IOptions<EmailSettings> options, ILogger<GmailEmailService> logger)
        {
            _settings = options.Value;
            _logger = logger;
        }

        public async Task<bool> SendEmailAsync(string toEmail, string subject, string body)
        {
            var email = new MimeMessage();
            email.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
            email.To.Add(MailboxAddress.Parse(toEmail));
            email.Subject = subject;

            var builder = new BodyBuilder { HtmlBody = $"<p>{body}</p>" };
            email.Body = builder.ToMessageBody();

            try
            {
                using var smtp = new SmtpClient();
                await smtp.ConnectAsync(_settings.Host, _settings.Port, MailKit.Security.SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(_settings.FromAddress, _settings.Password);
                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Lỗi khi gửi email tới {Email}: {Loi}", toEmail, ex.Message);
                return false;
            }
        }
    }
}
