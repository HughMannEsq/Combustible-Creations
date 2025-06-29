using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AutumnRidgeUSA.Data;

namespace AutumnRidgeUSA.Pages.Auth
{
    public class ConfirmModel : PageModel
    {
        private readonly AppDbContext _context;

        public ConfirmModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty(SupportsGet = true)]
        public string Token { get; set; } = string.Empty;

        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync()
        {
            if (string.IsNullOrEmpty(Token))
            {
                Success = false;
                Message = "Invalid confirmation token.";
                return Page();
            }

            // Find user with this confirmation token
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.ConfirmationToken == Token && !u.IsConfirmed);

            if (user != null)
            {
                // Confirm the user
                user.IsConfirmed = true;
                user.ConfirmedAt = DateTime.UtcNow;
                user.ConfirmationToken = null; // Clear the token

                await _context.SaveChangesAsync();

                Success = true;
                Message = "Your account has been successfully confirmed! You can now log in.";
            }
            else
            {
                Success = false;
                Message = "Invalid or expired confirmation token.";
            }

            return Page();
        }
    }
}