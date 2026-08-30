using System.Net;
using System.Net.Mail;

namespace TM_PE.Data
{
    // Sends the forgot-password verification code by email over SMTP (Gmail
    // by default - see appsettings.json's EmailSettings, and use a Google
    // Account App Password there, never the real account password).
    //
    // SenderEmail/SenderPassword start blank on purpose: with nothing
    // configured, TrySendCodeAsync just returns false instead of throwing, so
    // the forgot-password flow still works end-to-end (see
    // VerifyCodeModel.DevCode) before real credentials are ever set up.
    public class EmailService
    {
        private readonly IConfiguration _config;
        public EmailService(IConfiguration config) => _config = config;

        public async Task<bool> TrySendCodeAsync(string toEmail, string code)
        {
            var host = _config["EmailSettings:SmtpHost"];
            var senderEmail = _config["EmailSettings:SenderEmail"];
            var senderPassword = _config["EmailSettings:SenderPassword"];

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(senderEmail) || string.IsNullOrWhiteSpace(senderPassword))
            {
                return false;
            }

            try
            {
                var port = _config.GetValue<int?>("EmailSettings:SmtpPort") ?? 587;
                var senderName = _config["EmailSettings:SenderName"] ?? "Pakonek Performance";

                using var client = new SmtpClient(host, port)
                {
                    Credentials = new NetworkCredential(senderEmail, senderPassword),
                    EnableSsl = true
                };

                using var message = new MailMessage
                {
                    From = new MailAddress(senderEmail, senderName),
                    Subject = "Your Pakonek Performance verification code",
                    Body = $"Your verification code is: {code}\n\nThis code expires in 10 minutes. If you didn't request this, you can safely ignore this email.",
                    IsBodyHtml = false
                };
                message.To.Add(toEmail);

                await client.SendMailAsync(message);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
