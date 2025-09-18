using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AutumnRidgeUSA.Services;
using AutumnRidgeUSA.Data;

namespace AutumnRidgeUSA.Controllers
{
    [ApiController]
    [Route("api")]
    public class LoginApiController : ControllerBase
    {
        private readonly ISecurityService _securityService;
        private readonly ILogger<LoginApiController> _logger;
        private readonly AppDbContext _context;

        public LoginApiController(ISecurityService securityService, ILogger<LoginApiController> logger, AppDbContext context)
        {
            _securityService = securityService;
            _logger = logger;
            _context = context;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                {
                    return BadRequest(new { message = "Email and password are required." });
                }

                _logger.LogInformation("Login attempt for email: {Email}", request.Email);

                var user = await _securityService.AuthenticateAsync(request.Email, request.Password);

                if (user != null)
                {
                    // Create secure session
                    var sessionToken = await _securityService.CreateSessionAsync(user);

                    // Set secure session cookie
                    Response.Cookies.Append("SessionToken", sessionToken, new CookieOptions
                    {
                        HttpOnly = true,        // Prevents JavaScript access
                        Secure = true,          // HTTPS only
                        SameSite = SameSiteMode.Strict,
                        Expires = DateTimeOffset.UtcNow.AddHours(2)
                    });

                    // Also set role cookie for backward compatibility with your home page
                    Response.Cookies.Append("ImpersonatedRole", user.Role, new CookieOptions
                    {
                        HttpOnly = false,
                        SameSite = SameSiteMode.Lax,
                        Secure = true,
                        Expires = DateTimeOffset.UtcNow.AddHours(2)
                    });

                    _logger.LogInformation("Login successful for: {Email}", request.Email);

                    return Ok(new
                    {
                        message = "Login successful",
                        user = new
                        {
                            email = user.Email,
                            firstName = user.FirstName,
                            lastName = user.LastName,
                            role = user.Role,
                            userId = user.UserId
                        }
                    });
                }
                else
                {
                    _logger.LogWarning("Login failed for: {Email}", request.Email);
                    return BadRequest(new { message = "Invalid email or password." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error for: {Email}", request.Email);
                return StatusCode(500, new { message = "An error occurred during login." });
            }
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var sessionToken = Request.Cookies["SessionToken"];

                if (!string.IsNullOrEmpty(sessionToken))
                {
                    await _securityService.LogoutAsync(sessionToken);
                }

                // Clear both cookies
                Response.Cookies.Delete("SessionToken");
                Response.Cookies.Delete("ImpersonatedRole");

                return Ok(new { message = "Logged out successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Logout error");
                return StatusCode(500, new { message = "An error occurred during logout." });
            }
        }

        public class LoginRequest
        {
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }
    }
}