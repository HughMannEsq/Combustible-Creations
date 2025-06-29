using MailKit.Net.Smtp;
using MimeKit;

namespace AutumnRidgeUSA.Services
{
    public interface IEmailService
    {
        Task SendConfirmationEmailAsync(string toEmail, string confirmationUrl);
        Task SendEmailAsync(string toEmail, string subject, string body);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendConfirmationEmailAsync(string toEmail, string confirmationUrl)
        {
            var subject = "Confirm Your Account - Autumn Ridge USA";
            var body = $@"
                <html>
                <body>
                    <h2>Welcome to Autumn Ridge USA!</h2>
                    <p>Thank you for registering with us. Please confirm your account by clicking the link below:</p>
                    <p><a href='{confirmationUrl}' style='background-color: #4CAF50; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px;'>Confirm Account</a></p>
                    <p>If the button doesn't work, copy and paste this link into your browser:</p>
                    <p>{confirmationUrl}</p>
                    <p>If you didn't create an account with us, please ignore this email.</p>
                    <br>
                    <p>Best regards,<br>Autumn Ridge USA Team</p>
                </body>
                </html>";

            await SendEmailAsync(toEmail, subject, body);
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(
                    _configuration["Email:FromName"] ?? "Autumn Ridge USA",
                    _configuration["Email:FromAddress"]));
                message.To.Add(new MailboxAddress("", toEmail));
                message.Subject = subject;

                var builder = new BodyBuilder
                {
                    HtmlBody = body
                };
                message.Body = builder.ToMessageBody();

                using var client = new SmtpClient();

                // Connect to SMTP server
                await client.ConnectAsync(
                    _configuration["Email:SmtpServer"],
                    int.Parse(_configuration["Email:SmtpPort"] ?? "587"),
                    bool.Parse(_configuration["Email:UseSsl"] ?? "true"));

                // Authenticate
                await client.AuthenticateAsync(
                    _configuration["Email:Username"],
                    _configuration["Email:Password"]);

                // Send email
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation("Email sent successfully to {Email}", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
                throw;
            }
        }
    }
}