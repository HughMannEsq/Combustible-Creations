using MailKit.Net.Smtp;
using MimeKit;

namespace AutumnRidgeUSA.Services
{
    public interface IEmailService
    {
        Task SendConfirmationEmailAsync(string toEmail, string confirmationUrl);
        Task SendVerificationEmailAsync(string email, string name, string verificationLink);
        Task SendVerificationEmailAsync(string email, string firstName, string lastName, string userId, string verificationLink);
        Task SendWelcomeEmailAsync(string toEmail, string firstName); // ADD: This method for admin dashboard
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

        // Legacy method for backward compatibility
        public async Task SendVerificationEmailAsync(string email, string name, string verificationLink)
        {
            var subject = "Complete Your Registration - Autumn Ridge LLC";
            var body = $@"
                <html>
                <body>
                    <h2>Hello {name}!</h2>
                    <p>Thank you for starting your registration with Autumn Ridge LLC. To complete your account setup, please click the link below:</p>
                    <p><a href='{verificationLink}' style='background-color: #1e3a5f; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px;'>Complete Registration</a></p>
                    <p>If the button doesn't work, copy and paste this link into your browser:</p>
                    <p>{verificationLink}</p>
                    <p><strong>Important:</strong> This link will expire in 1 hour for security reasons.</p>
                    <p>If you didn't start this registration, please ignore this email.</p>
                    <br>
                    <p>Best regards,<br>Autumn Ridge LLC Team</p>
                </body>
                </html>";

            await SendEmailAsync(email, subject, body);
        }

        // Enhanced method with UserId included - this is what your admin dashboard calls
        public async Task SendVerificationEmailAsync(string email, string firstName, string lastName, string userId, string verificationLink)
        {
            var subject = "Complete Your Registration - Autumn Ridge LLC";
            var body = $@"
                <html>
                <body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
                    <div style=""background-color: #1e3a5f; padding: 20px; text-align: center; border-radius: 8px 8px 0 0;"">
                        <h1 style=""color: #d97706; margin: 0; font-size: 28px; font-weight: bold;"">Autumn Ridge LLC</h1>
                    </div>
                    
                    <div style=""background-color: #f9f9f9; padding: 30px; border-radius: 0 0 8px 8px; border: 1px solid #ddd;"">
                        <h2 style=""color: #1e3a5f; margin-bottom: 20px;"">Hello {firstName},</h2>
                        
                        <p>Thank you for starting your registration with Autumn Ridge LLC. We're excited to welcome you to our community!</p>
                        
                        <div style=""background-color: #e3f2fd; padding: 15px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #1e3a5f;"">
                            <strong>Your Account Details:</strong><br>
                            <strong>Name:</strong> {firstName} {lastName}<br>
                            <strong>User ID:</strong> {userId}<br>
                            <strong>Email:</strong> {email}
                        </div>
                        
                        <p>To complete your account setup and access our services, please click the link below to finish your registration:</p>
                        
                        <div style=""text-align: center; margin: 30px 0;"">
                            <a href=""{verificationLink}"" style=""background-color: #1e3a5f; color: white; padding: 15px 30px; text-decoration: none; border-radius: 5px; font-weight: bold; display: inline-block;"">
                                Complete Registration
                            </a>
                        </div>
                        
                        <div style=""background-color: #fff3cd; padding: 15px; border-radius: 5px; border-left: 4px solid #ffc107; margin: 20px 0;"">
                            <strong>⚠️ Important:</strong> This verification link will expire in <strong>1 hour</strong> for security purposes. Please complete your registration promptly.
                        </div>
                        
                        <p>If the button above doesn't work, copy and paste this link into your browser:</p>
                        <p style=""word-break: break-all; background-color: #f8f9fa; padding: 10px; border-radius: 3px; font-family: monospace; font-size: 12px;"">{verificationLink}</p>
                        
                        <p>Once you complete registration, you'll have full access to:</p>
                        <ul style=""margin: 15px 0; padding-left: 30px;"">
                            <li>Real Estate Services</li>
                            <li>Storage Solutions</li>
                            <li>Contracting Services</li>
                            <li>Account Management Tools</li>
                        </ul>
                        
                        <p style=""margin-top: 30px;"">If you didn't create an account with us, please ignore this email. Your information will be automatically removed from our system within one hour.</p>
                        
                        <hr style=""border: none; height: 1px; background-color: #ddd; margin: 30px 0;"">
                        
                        <p style=""color: #666; font-size: 14px;"">
                            Best regards,<br>
                            <strong>The Autumn Ridge LLC Team</strong><br>
                            <em>Real Estate | Storage | Contracting</em>
                        </p>
                        
                        <p style=""color: #999; font-size: 12px; margin-top: 20px;"">
                            This is an automated message. Please do not reply to this email.
                        </p>
                    </div>
                </body>
                </html>";

            await SendEmailAsync(email, subject, body);
        }

        // ADD: Welcome email method that your admin dashboard expects
        public async Task SendWelcomeEmailAsync(string toEmail, string firstName)
        {
            var subject = "Welcome to Autumn Ridge LLC - Registration Complete!";
            var body = $@"
                <html>
                <body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
                    <div style=""background-color: #1e3a5f; padding: 20px; text-align: center; border-radius: 8px 8px 0 0;"">
                        <h1 style=""color: #d97706; margin: 0; font-size: 28px; font-weight: bold;"">Welcome to Autumn Ridge LLC!</h1>
                    </div>
                    
                    <div style=""background-color: #f9f9f9; padding: 30px; border-radius: 0 0 8px 8px; border: 1px solid #ddd;"">
                        <h2 style=""color: #1e3a5f; margin-bottom: 20px;"">Congratulations, {firstName}!</h2>
                        
                        <div style=""background-color: #d4edda; padding: 15px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #28a745;"">
                            <strong>✅ Registration Complete!</strong><br>
                            Your account has been successfully activated and you now have full access to all our services.
                        </div>
                        
                        <p>Welcome to the Autumn Ridge LLC family! We're thrilled to have you as part of our community.</p>
                        
                        <h3 style=""color: #1e3a5f; margin-top: 25px;"">What's Available to You:</h3>
                        <div style=""background-color: #fff; padding: 20px; border-radius: 5px; border: 1px solid #ddd; margin: 15px 0;"">
                            <ul style=""margin: 0; padding-left: 20px; list-style-type: none;"">
                                <li style=""margin: 10px 0;"">🏠 <strong>Real Estate Services</strong> - Property management and sales</li>
                                <li style=""margin: 10px 0;"">📦 <strong>Storage Solutions</strong> - Secure storage facilities</li>
                                <li style=""margin: 10px 0;"">🔨 <strong>Contracting Services</strong> - Professional construction and renovation</li>
                                <li style=""margin: 10px 0;"">💼 <strong>Account Management</strong> - Easy online account access</li>
                            </ul>
                        </div>
                        
                        <div style=""text-align: center; margin: 30px 0;"">
                            <a href=""https://autumnridgeusa.com/login"" style=""background-color: #1e3a5f; color: white; padding: 15px 30px; text-decoration: none; border-radius: 5px; font-weight: bold; display: inline-block;"">
                                Access Your Account
                            </a>
                        </div>
                        
                        <p style=""margin-top: 25px;"">If you have any questions or need assistance getting started, please don't hesitate to reach out to our support team.</p>
                        
                        <hr style=""border: none; height: 1px; background-color: #ddd; margin: 30px 0;"">
                        
                        <p style=""color: #666; font-size: 14px;"">
                            Thank you for choosing Autumn Ridge LLC!<br>
                            <strong>The Autumn Ridge LLC Team</strong><br>
                            <em>Real Estate | Storage | Contracting</em>
                        </p>
                        
                        <p style=""color: #999; font-size: 12px; margin-top: 20px;"">
                            This is an automated welcome message. You can reply to this email if you need assistance.
                        </p>
                    </div>
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