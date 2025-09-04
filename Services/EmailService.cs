
// FILE 2: EmailService.cs
// Location: Services/EmailService.cs
// ========================================
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AutumnRidgeUSA.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger)
        {
            _emailSettings = emailSettings.Value;
            _logger = logger;
        }

        public async Task SendVerificationEmailAsync(string toEmail, string firstName,
            string lastName, string userId, string verificationLink)
        {
            var subject = "Welcome to Autumn Ridge LLC - Verify Your Email";

            var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); 
                   color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }}
        .content {{ background: #f9f9f9; padding: 30px; border: 1px solid #e0e0e0; 
                    border-radius: 0 0 10px 10px; }}
        .user-id {{ background: #fff; padding: 15px; border-radius: 8px; 
                    border: 2px solid #667eea; margin: 20px 0; text-align: center; }}
        .user-id-code {{ font-size: 24px; font-weight: bold; color: #667eea; 
                         letter-spacing: 2px; }}
        .btn {{ display: inline-block; padding: 14px 30px; background: #667eea; 
                color: white; text-decoration: none; border-radius: 25px; 
                margin: 20px 0; font-weight: bold; }}
        .btn:hover {{ background: #5a67d8; }}
        .footer {{ text-align: center; margin-top: 30px; color: #666; font-size: 14px; }}
        .divisions {{ margin: 20px 0; padding: 15px; background: #fff; border-radius: 8px; }}
        .division-badge {{ display: inline-block; padding: 5px 12px; border-radius: 15px; 
                          margin: 5px; font-size: 12px; font-weight: bold; }}
        .storage {{ background: #e3f2fd; color: #1976d2; }}
        .contracting {{ background: #fff3e0; color: #f57c00; }}
        .real-estate {{ background: #e8f5e9; color: #388e3c; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Welcome to Autumn Ridge LLC</h1>
            <p style='margin: 0; opacity: 0.9;'>Your Multi-Business Venture Partner</p>
        </div>
        <div class='content'>
            <h2>Hello {firstName} {lastName}!</h2>
            <p>Thank you for signing up with Autumn Ridge LLC. You're just one step away from accessing our comprehensive business services.</p>
            
            <div class='user-id'>
                <p style='margin: 0; color: #666;'>Your Unique User ID:</p>
                <div class='user-id-code'>{userId}</div>
                <p style='margin: 5px 0 0 0; color: #999; font-size: 12px;'>Keep this ID for your records</p>
            </div>

            <p><strong>Complete your registration to access:</strong></p>
            <div class='divisions'>
                <span class='division-badge storage'>Storage Solutions</span>
                <span class='division-badge contracting'>Contracting Services</span>
                <span class='division-badge real-estate'>Real Estate</span>
            </div>

            <p style='text-align: center;'>
                <a href='{verificationLink}' class='btn'>Complete Registration</a>
            </p>

            <p style='color: #666; font-size: 14px;'>
                <strong>Note:</strong> This verification link will expire in 1 hour for security purposes. 
                If you didn't request this email, please ignore it.
            </p>

            <div class='footer'>
                <p>Best regards,<br>The Autumn Ridge LLC Team</p>
                <p style='font-size: 12px; color: #999;'>
                    This is an automated message from no-reply@autumnridgeusa.com<br>
                    Please do not reply to this email.
                </p>
            </div>
        </div>
    </div>
</body>
</html>";

            await SendEmailAsync(toEmail, subject, htmlBody);
        }

        public async Task SendWelcomeEmailAsync(string toEmail, string firstName)
        {
            var subject = "Registration Complete - Welcome to Autumn Ridge LLC!";

            var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); 
                   color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }}
        .content {{ background: #f9f9f9; padding: 30px; border: 1px solid #e0e0e0; 
                    border-radius: 0 0 10px 10px; }}
        .btn {{ display: inline-block; padding: 14px 30px; background: #667eea; 
                color: white; text-decoration: none; border-radius: 25px; 
                margin: 20px 0; font-weight: bold; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Welcome Aboard, {firstName}!</h1>
        </div>
        <div class='content'>
            <h2>Your registration is complete!</h2>
            <p>You now have full access to all Autumn Ridge LLC services and features.</p>
            
            <p style='text-align: center;'>
                <a href='https://autumnridgeusa.com/Account/Login' class='btn'>Sign In to Your Account</a>
            </p>

            <p>If you have any questions, our support team is here to help.</p>
            
            <p>Best regards,<br>The Autumn Ridge LLC Team</p>
        </div>
    </div>
</body>
</html>";

            await SendEmailAsync(toEmail, subject, htmlBody);
        }

        public async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_emailSettings.FromName, _emailSettings.FromEmail));
                message.To.Add(new MailboxAddress(toEmail, toEmail));
                message.Subject = subject;

                var bodyBuilder = new BodyBuilder { HtmlBody = htmlBody };
                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();

                await client.ConnectAsync(_emailSettings.SmtpServer, _emailSettings.SmtpPort,
                    _emailSettings.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None);

                await client.AuthenticateAsync(_emailSettings.Username, _emailSettings.Password);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation($"Email sent successfully to {toEmail}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send email to {toEmail}");
                return false;
            }
        }
    }
}
