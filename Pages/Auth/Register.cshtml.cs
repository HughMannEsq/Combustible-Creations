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

        public RegisterModel(AppDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
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

            // Check if user already exists
            if (await _context.Users.AnyAsync(u => u.Email == Email))
            {
                ModelState.AddModelError(nameof(Email), "An account with this email already exists.");
                return Page();
            }

            // Check if there's already a temp signup for this email
            var existingTemp = await _context.TempSignups.FirstOrDefaultAsync(t => t.Email == Email);
            if (existingTemp != null)
            {
                _context.TempSignups.Remove(existingTemp); // Remove old temp record
            }

            // Create temp signup record (no password yet)
            var tempSignup = new TempSignup
            {
                FirstName = FirstName,
                LastName = LastName,
                Email = Email,
                VerificationToken = GenerateConfirmationToken(),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            };

            _context.TempSignups.Add(tempSignup);
            await _context.SaveChangesAsync();

            try
            {
                // Send verification email to complete registration
                var completeUrl = $"{Request.Scheme}://{Request.Host}/Auth/CompleteRegistration?token={tempSignup.VerificationToken}";

                await _emailService.SendVerificationEmailAsync(Email, $"{FirstName} {LastName}", completeUrl!);
                StatusMessage = "Please check your email for a link to complete your registration.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error sending email: {ex.Message}";
            }

            return RedirectToPage("/Auth/Register");
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