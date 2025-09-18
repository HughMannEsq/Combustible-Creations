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
                _logger.LogInformation("Login attempt for email: {Email}", request.Email);

                var user = await _securityService.AuthenticateAsync(request.Email, request.Password);

                if (user != null)
                {
                    // Simple role cookie (like before)
                    Response.Cookies.Append("ImpersonatedRole", user.Role, new CookieOptions
                    {
                        HttpOnly = false,
                        SameSite = SameSiteMode.Lax,
                        Secure = false, // Simplified for testing
                        Expires = DateTimeOffset.UtcNow.AddHours(8)
                    });

                    _logger.LogInformation("Login successful for: {Email}", request.Email);

                    return Ok(new { message = "Login successful" });
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