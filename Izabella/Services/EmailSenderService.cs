using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration; // <-- Ez a névtér szükséges az IConfiguration-höz

namespace Izabella.Services
{
    public class EmailSenderService
    {
        private readonly IConfiguration _configuration;

        // Kontruktor injektálás: a keretrendszer automatikusan átadja a konfigurációt
        public EmailSenderService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendReportWithAttachmentAsync(string toEmail, string subject, string body, byte[] attachmentBytes, string fileName)
        {
            // Adatok biztonságos kiolvasása az appsettings.json fájlból
            string smtpHost = _configuration["EmailSettings:SmtpHost"] ?? "gastor.hu";
            int smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
            string smtpUser = _configuration["EmailSettings:SmtpUser"];
            string smtpPass = _configuration["EmailSettings:SmtpPass"];

            using (var message = new MailMessage())
            {
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
                        client.EnableSsl = true; // Az 587-es porton ez elindítja a TLS kézfogást
                        await client.SendMailAsync(message);
                    }
                }
            }
        }
    }
}