using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Izabella.Services
{
    public class EmailSenderService
    {
        private readonly IConfiguration _configuration;

        public EmailSenderService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendReportWithAttachmentAsync(string toEmail, string subject, string body, byte[] attachmentBytes, string fileName)
        {
            // PONTOSÍTVA: A JSON-ben szereplő kulcsneveket olvassuk ki!
            string smtpHost = _configuration["EmailSettings:SmtpServer"] ?? "gastor.hu";
            int smtpPort = int.Parse(_configuration["EmailSettings:Port"] ?? "587");
            string smtpUser = _configuration["EmailSettings:SenderEmail"];
            string smtpPass = _configuration["EmailSettings:SenderPassword"];

            // Biztonsági ellenőrzés, ha üresek lennének a konfigurációs adatok
            if (string.IsNullOrEmpty(smtpUser) || string.IsNullOrEmpty(smtpPass))
            {
                throw new InvalidOperationException("Az e-mail küldéshez szükséges hitelesítési adatok (SenderEmail, SenderPassword) hiányoznak az appsettings.json fájlból!");
            }

            using (var message = new MailMessage())
            {
                // A feladóhoz a beállított SenderEmail címet használjuk
                message.From = new MailAddress(smtpUser, "Izabella Tehenészet Kezelő");
                message.To.Add(new MailAddress(toEmail));
                message.Subject = subject;
                message.Body = body;
                message.IsBodyHtml = true;

                using (var ms = new MemoryStream(attachmentBytes))
                {
                    var attachment = new Attachment(ms, fileName, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                    message.Attachments.Add(attachment);

                    using (var client = new SmtpClient(smtpHost, smtpPort))
                    {
                        client.Credentials = new NetworkCredential(smtpUser, smtpPass);
                        client.EnableSsl = true;
                        await client.SendMailAsync(message);
                    }
                }
            }
        }
    }
}