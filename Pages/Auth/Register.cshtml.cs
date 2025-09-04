using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using AutumnRidgeUSA.Data;
using AutumnRidgeUSA.Models;
using AutumnRidgeUSA.Services;

namespace AutumnRidgeUSA.Pages.Auth
{
    public class RegisterModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<RegisterModel> _logger;

        public RegisterModel(AppDbContext context, IEmailService emailService, ILogger<RegisterModel> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        [BindProperty]
        [Required]
        public string FirstName { get; set; } = string.Empty;

        [BindProperty]
        [Required]
        public string LastName { get; set; } = string.Empty;

        [BindProperty]
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        [Required]
        [DataType(DataType.Password)]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters long")]
        public string Password { get; set; } = string.Empty;

        [TempData]
        public string? StatusMessage { get; set; }

        public void OnGet()
        {
            // Handle GET request
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var normalizedEmail = Email.ToLower().Trim();

            // Check if user already exists
            if (await _context.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail))
            {
                ModelState.AddModelError(nameof(Email), "An account with this email already exists.");
                return Page();
            }

            // Check if there's already a temp signup for this email
            var existingTemp = await _context.TempSignups
                .FirstOrDefaultAsync(t => t.Email.ToLower() == normalizedEmail);
            if (existingTemp != null)
            {
                _context.TempSignups.Remove(existingTemp); // Remove old temp record
            }

            // Generate unique UserId
            string userId;
            do
            {
                userId = GenerateUserId();
            } while (await _context.TempSignups.AnyAsync(t => t.UserId == userId) ||
                     await _context.Users.AnyAsync(u => u.UserId == userId));

            // Create temp signup record (no password yet)
            var verificationToken = GenerateConfirmationToken();
            var tempSignup = new TempSignup
            {
                UserId = userId,  // Now including the UserId
                FirstName = FirstName.Trim(),
                LastName = LastName.Trim(),
                Email = normalizedEmail,
                VerificationToken = verificationToken,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            };

            _context.TempSignups.Add(tempSignup);
            await _context.SaveChangesAsync();

            try
            {
                // Build the complete verification URL with both token and userId
                var completeUrl = $"{Request.Scheme}://{Request.Host}/Auth/CompleteRegistration?token={verificationToken}&userId={userId}";

                // Send verification email with all 5 required parameters
                await _emailService.SendVerificationEmailAsync(
                    normalizedEmail,        // toEmail
                    FirstName.Trim(),       // firstName
                    LastName.Trim(),        // lastName
                    userId,                 // userId
                    completeUrl            // verificationLink
                );

                StatusMessage = "Please check your email for a link to complete your registration. Your User ID is: " + userId;
                _logger.LogInformation("Registration initiated for {Email} with UserId {UserId}", normalizedEmail, userId);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error sending email: {ex.Message}";
                _logger.LogError(ex, "Failed to send verification email to {Email}", normalizedEmail);
            }

            return RedirectToPage("/Auth/Register");
        }

        private string GenerateUserId()
        {
            var random = new Random();
            var numbers = random.Next(1000, 9999);
            var letters = new string(Enumerable.Range(0, 3)
                .Select(_ => (char)random.Next('A', 'Z' + 1)).ToArray());
            return $"{numbers}-{letters}";
        }

        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }

        private static string GenerateConfirmationToken()
        {
            return Guid.NewGuid().ToString("N");
        }
    }
}