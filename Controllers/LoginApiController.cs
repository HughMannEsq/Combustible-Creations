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

        public LoginApiController(
            ISecurityService securityService,
            ILogger<LoginApiController> logger,
            AppDbContext context)
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

                // Authenticate the user
                var user = await _securityService.AuthenticateAsync(request.Email, request.Password);

                if (user == null)
                {
                    _logger.LogWarning("Login failed for: {Email}", request.Email);
                    return BadRequest(new { message = "Invalid email or password." });
                }

                // User authenticated successfully, now set cookies

                // 1. Set the role cookie (this is what Home.cshtml checks)
                Response.Cookies.Append("ImpersonatedRole", user.Role, new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddHours(8),
                    HttpOnly = false,  // Allow JavaScript to read it
                    SameSite = SameSiteMode.Lax,
                    Secure = false,    // Set to true in production with HTTPS
                    Path = "/"         // Make sure it's available site-wide
                });

                // 2. Create and set session token
                string sessionToken;
                try
                {
                    sessionToken = await _securityService.CreateSessionAsync(user);
                }
                catch (Exception sessionEx)
                {
                    _logger.LogError(sessionEx, "Failed to create session for user {Email}", user.Email);
                    sessionToken = Guid.NewGuid().ToString("N");
                }

                Response.Cookies.Append("SessionToken", sessionToken, new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddHours(8),
                    HttpOnly = true,   // Secure - no JS access
                    SameSite = SameSiteMode.Lax,
                    Secure = false,    // Set to true in production
                    Path = "/"
                });

                // 3. Set user info for client-side display
                Response.Cookies.Append("UserInfo", $"{user.FirstName}|{user.Role}", new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddHours(8),
                    HttpOnly = false,
                    SameSite = SameSiteMode.Lax,
                    Secure = false,
                    Path = "/"
                });

                _logger.LogInformation("Login successful. Set role cookie to: {Role} for user: {Email}",
                    user.Role, user.Email);

                // Return success response
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error for: {Email}", request?.Email ?? "unknown");
                return StatusCode(500, new { message = "An error occurred during login. Please try again." });
            }
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                // Get session token from cookie
                var sessionToken = Request.Cookies["SessionToken"];

                if (!string.IsNullOrEmpty(sessionToken))
                {
                    // Invalidate session in database
                    try
                    {
                        await _securityService.LogoutAsync(sessionToken);
                    }
                    catch (Exception logoutEx)
                    {
                        _logger.LogError(logoutEx, "Error invalidating session");
                    }
                }

                // Clear all auth cookies
                Response.Cookies.Delete("SessionToken");
                Response.Cookies.Delete("ImpersonatedRole");
                Response.Cookies.Delete("UserInfo");

                _logger.LogInformation("User logged out successfully");
                return Ok(new { message = "Logged out successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Logout error");
                // Still clear cookies even if there's an error
                Response.Cookies.Delete("SessionToken");
                Response.Cookies.Delete("ImpersonatedRole");
                Response.Cookies.Delete("UserInfo");
                return Ok(new { message = "Logged out" });
            }
        }

        [HttpGet("check-session")]
        public async Task<IActionResult> CheckSession()
        {
            try
            {
                var sessionToken = Request.Cookies["SessionToken"];

                if (string.IsNullOrEmpty(sessionToken))
                {
                    return Unauthorized(new { message = "No session found" });
                }

                var user = await _securityService.ValidateSessionAsync(sessionToken);

                if (user == null)
                {
                    // Clear invalid cookies
                    Response.Cookies.Delete("SessionToken");
                    Response.Cookies.Delete("ImpersonatedRole");
                    Response.Cookies.Delete("UserInfo");
                    return Unauthorized(new { message = "Session expired or invalid" });
                }

                return Ok(new
                {
                    valid = true,
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Session check error");
                return StatusCode(500, new { message = "Error checking session" });
            }
        }

        public class LoginRequest
        {
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }
    }
}