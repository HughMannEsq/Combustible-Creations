using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AutumnRidgeUSA.Data;
using AutumnRidgeUSA.Models;

namespace AutumnRidgeUSA.Controllers
{
    [ApiController]
    [Route("api")]
    public class SignupController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SignupController(AppDbContext context)
        {
            _context = context;
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

                // Check if email already exists
                var existingUsers = await _context.Users
                    .Where(u => u.Email.ToLower() == normalizedEmail)
                    .ToListAsync();

                if (existingUsers.Any())
                {
                    // Check if last name matches but first name doesn't
                    var sameLastName = existingUsers.Any(u =>
                        !string.IsNullOrEmpty(u.LastName) && !string.IsNullOrEmpty(u.FirstName) &&
                        u.LastName.ToLower() == request.LastName.ToLower().Trim() &&
                        u.FirstName.ToLower() != request.FirstName.ToLower().Trim());

                    if (sameLastName)
                    {
                        // Allow this signup (husband/wife scenario)
                        var newUser = new User
                        {
                            FirstName = request.FirstName.Trim(),
                            LastName = request.LastName.Trim(),
                            Email = normalizedEmail,
                            PasswordHash = "TempPassword123", // You'll want to implement proper password hashing
                            CreatedAt = DateTime.UtcNow
                        };

                        _context.Users.Add(newUser);
                        await _context.SaveChangesAsync();

                        return Ok(new { message = "Account created successfully!" });
                    }
                    else
                    {
                        // Email exists and either same first name or different last name
                        return BadRequest(new { message = "An account with this email address already exists." });
                    }
                }

                // Email doesn't exist, create new user
                var user = new User
                {
                    FirstName = request.FirstName.Trim(),
                    LastName = request.LastName.Trim(),
                    Email = normalizedEmail,
                    PasswordHash = "TempPassword123", // You'll want to implement proper password hashing
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Account created successfully!" });
            }
            catch (Exception ex)
            {
                // Log the error (you might want to use a proper logging framework)
                Console.WriteLine($"Signup error: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred. Please try again." });
            }
        }
    }

    public class SignupRequest
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}