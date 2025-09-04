// ========================================
// FILE 1: IEmailService.cs
// Location: Services/IEmailService.cs
// ========================================
using System.Threading.Tasks;

namespace AutumnRidgeUSA.Services
{
    public interface IEmailService
    {
        Task SendVerificationEmailAsync(string toEmail, string firstName, string lastName,
            string userId, string verificationLink);

        Task SendWelcomeEmailAsync(string toEmail, string firstName);

        Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody);
    }
}
