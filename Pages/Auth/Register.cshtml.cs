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

            // Create new user
            var user = new User
            {
                Email = Email,
                PasswordHash = HashPassword(Password),
                ConfirmationToken = GenerateConfirmationToken(),
                IsConfirmed = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            try
            {
                // Generate confirmation URL
                var confirmationUrl = Url.Page("/Auth/Confirm", pageHandler: null,
                    values: new { token = user.ConfirmationToken }, protocol: Request.Scheme);

                // Send confirmation email
                await _emailService.SendConfirmationEmailAsync(user.Email, confirmationUrl!);

                StatusMessage = "Registration successful! Please check your email for a confirmation link.";
            }
            catch (Exception ex)
            {
                // Log the error but don't fail the registration
                // In production, you might want to handle this differently
                StatusMessage = $"Registration successful! However, there was an issue sending the confirmation email. " +
                              $"Please contact support. Error: {ex.Message}";
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