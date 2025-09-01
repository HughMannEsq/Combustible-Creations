using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using AutumnRidgeUSA.Data;
using AutumnRidgeUSA.Models;
using AutumnRidgeUSA.Services;

namespace AutumnRidgeUSA.Pages.Admin
{
    public class TempSignupsModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;

        public TempSignupsModel(AppDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        public List<TempSignupViewModel> TempSignups { get; set; } = new List<TempSignupViewModel>();

        [TempData]
        public string? StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            // Check if user is admin
            var role = Request.Cookies["ImpersonatedRole"] ?? "Guest";
            if (role != "Admin")
            {
                return RedirectToPage("/Home");
            }

            await LoadTempSignupsAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int signupId)
        {
            var role = Request.Cookies["ImpersonatedRole"] ?? "Guest";
            if (role != "Admin")
            {
                return RedirectToPage("/Home");
            }

            var signup = await _context.TempSignups.FindAsync(signupId);
            if (signup != null)
            {
                // Check if this signup has been authorized (completed registration)
                var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == signup.Email);
                if (existingUser == null)
                {
                    _context.TempSignups.Remove(signup);
                    await _context.SaveChangesAsync();
                    StatusMessage = $"Deleted temporary signup for {signup.Email}";
                }
                else
                {
                    StatusMessage = $"Cannot delete - user {signup.Email} has already completed registration";
                }
            }
            else
            {
                StatusMessage = "Signup record not found";
            }

            await LoadTempSignupsAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostCleanupExpiredAsync()
        {
            var role = Request.Cookies["ImpersonatedRole"] ?? "Guest";
            if (role != "Admin")
            {
                return RedirectToPage("/Home");
            }

            var expiredSignups = await _context.TempSignups
                .Where(t => t.ExpiresAt <= DateTime.UtcNow)
                .ToListAsync();

            if (expiredSignups.Any())
            {
                _context.TempSignups.RemoveRange(expiredSignups);
                await _context.SaveChangesAsync();
                StatusMessage = $"Deleted {expiredSignups.Count} expired signup(s)";
            }
            else
            {
                StatusMessage = "No expired signups found";
            }

            await LoadTempSignupsAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostResendEmailAsync(int signupId)
        {
            var role = Request.Cookies["ImpersonatedRole"] ?? "Guest";
            if (role != "Admin")
            {
                return RedirectToPage("/Home");
            }

            var signup = await _context.TempSignups.FindAsync(signupId);
            if (signup != null && signup.ExpiresAt > DateTime.UtcNow)
            {
                try
                {
                    var verificationLink = $"{Request.Scheme}://{Request.Host}/Auth/CompleteRegistration?token={signup.VerificationToken}";
                    await _emailService.SendVerificationEmailAsync(
                        signup.Email,
                        $"{signup.FirstName} {signup.LastName}",
                        verificationLink
                    );
                    StatusMessage = $"Verification email resent to {signup.Email}";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Failed to resend email to {signup.Email}: {ex.Message}";
                }
            }
            else if (signup == null)
            {
                StatusMessage = "Signup record not found";
            }
            else
            {
                StatusMessage = "Cannot resend email - signup has expired";
            }

            await LoadTempSignupsAsync();
            return Page();
        }

        private async Task LoadTempSignupsAsync()
        {
            var tempSignups = await _context.TempSignups
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            // Get list of emails that have completed registration
            var completedEmails = await _context.Users
                .Where(u => tempSignups.Select(t => t.Email).Contains(u.Email))
                .Select(u => u.Email)
                .ToListAsync();

            TempSignups = tempSignups.Select(t => new TempSignupViewModel
            {
                Id = t.Id,
                FirstName = t.FirstName,
                LastName = t.LastName,
                Email = t.Email,
                CreatedAt = t.CreatedAt,
                ExpiresAt = t.ExpiresAt,
                IsAuthorized = completedEmails.Contains(t.Email),
                IsExpired = t.ExpiresAt <= DateTime.UtcNow
            }).ToList();
        }
    }

    public class TempSignupViewModel
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsAuthorized { get; set; }
        public bool IsExpired { get; set; }

        public string TimeRemaining
        {
            get
            {
                if (IsExpired)
                    return "EXPIRED";

                var remaining = ExpiresAt - DateTime.UtcNow;
                if (remaining.TotalMinutes < 1)
                    return "< 1 minute";
                else if (remaining.TotalHours < 1)
                    return $"{(int)remaining.TotalMinutes} minutes";
                else
                    return $"{(int)remaining.TotalHours}h {remaining.Minutes}m";
            }
        }
    }
}