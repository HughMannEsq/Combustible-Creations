using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AutumnRidgeUSA.Data;
using AutumnRidgeUSA.Models;
using AutumnRidgeUSA.Services;

namespace AutumnRidgeUSA.Controllers
{
    [ApiController]
    [Route("api")]
    public class SignupController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;

        public SignupController(AppDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        [HttpPost("signup")]
        public async Task<IActionResult> Signup([FromBody] SignupRequest request)
        {
            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(request.FirstName) ||
                    string.IsNullOrWhiteSpace(request.LastName) ||
                    string.IsNullOrWhiteSpace(request.Email))
                {
                    return BadRequest(new { message = "All fields are required." });
                }

                // Normalize email for comparison
                var normalizedEmail = request.Email.ToLower().Trim();

                // Check if user already exists
                if (await _context.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail))
                {
                    return BadRequest(new { message = "An account with this email address already exists." });
                }

                // Check if there's already a temp signup for this email and remove it
                var existingTemp = await _context.TempSignups
                    .FirstOrDefaultAsync(t => t.Email.ToLower() == normalizedEmail);
                if (existingTemp != null)
                {
                    _context.TempSignups.Remove(existingTemp);
                }

                // Generate unique UserId
                string userId;
                do
                {
                    userId = GenerateUserId();
                } while (await _context.TempSignups.AnyAsync(t => t.UserId == userId) ||
                         await _context.Users.AnyAsync(u => u.Id.ToString() == userId));

                // Create temporary signup record
                var token = Guid.NewGuid().ToString("N");
                var tempSignup = new TempSignup
                {
                    UserId = userId, // Assign the generated UserId
                    FirstName = request.FirstName.Trim(),
                    LastName = request.LastName.Trim(),
                    Email = normalizedEmail,
                    VerificationToken = token,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(1)
                };

                _context.TempSignups.Add(tempSignup);
                await _context.SaveChangesAsync();

                // Send verification email with UserId included
                var verificationLink = $"{Request.Scheme}://{Request.Host}/Auth/CompleteRegistration?token={token}";
                await _emailService.SendVerificationEmailAsync(
                    normalizedEmail,
                    $"{request.FirstName} {request.LastName}",
                    verificationLink,
                    userId // Pass UserId to include in email
                );

                return Ok(new { message = "Verification email sent. Please check your inbox to complete registration.", userId = userId });
            }
            catch (Exception ex)
            {
                // Log the error (you might want to use a proper logging framework)
                Console.WriteLine($"Signup error: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred. Please try again." });
            }
        }

        private string GenerateUserId()
        {
            var random = new Random();
            var numbers = random.Next(1000, 9999);
            var letters = new string(Enumerable.Range(0, 3)
                .Select(_ => (char)random.Next('A', 'Z' + 1)).ToArray());
            return $"{numbers}-{letters}";
        }
    }

    public class SignupRequest
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}