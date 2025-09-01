using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using AutumnRidgeUSA.Data;
using AutumnRidgeUSA.Models;
using System.Security.Cryptography;
using System.Text;

namespace AutumnRidgeUSA.Pages.Auth
{
    public class CompleteRegistrationModel : PageModel
    {
        private readonly AppDbContext _context;

        public CompleteRegistrationModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public string Token { get; set; } = string.Empty;

        [BindProperty]
        public string FirstName { get; set; } = string.Empty;

        [BindProperty]
        public string LastName { get; set; } = string.Empty;

        [BindProperty]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        [Required]
        [DataType(DataType.Password)]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;

        [BindProperty]
        public string? Phone { get; set; }

        [BindProperty]
        public string? Address { get; set; }

        [BindProperty]
        public string? City { get; set; }

        [BindProperty]
        public string? State { get; set; }

        [BindProperty]
        public string? ZipCode { get; set; }

        public async Task<IActionResult> OnGetAsync(string token)
        {
            var tempSignup = await _context.TempSignups
                .FirstOrDefaultAsync(t => t.VerificationToken == token && t.ExpiresAt > DateTime.UtcNow);

            if (tempSignup == null)
                return RedirectToPage("/Error");

            Token = token;
            FirstName = tempSignup.FirstName!;
            LastName = tempSignup.LastName!;
            Email = tempSignup.Email;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var tempSignup = await _context.TempSignups
                .FirstOrDefaultAsync(t => t.VerificationToken == Token && t.ExpiresAt > DateTime.UtcNow);

            if (tempSignup == null)
                return RedirectToPage("/Error");

            // Create permanent user
            var user = new User
            {
                FirstName = tempSignup.FirstName,
                LastName = tempSignup.LastName,
                Email = tempSignup.Email,
                PasswordHash = HashPassword(Password),
                Phone = Phone,
                Address = Address,
                City = City,
                State = State,
                ZipCode = ZipCode,
                Role = "Client",
                IsConfirmed = true,
                ConfirmedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            _context.TempSignups.Remove(tempSignup);
            await _context.SaveChangesAsync();

            return RedirectToPage("/Auth/Login");
        }

        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }
    }
}