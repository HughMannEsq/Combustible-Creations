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

        // ADMIN ENDPOINTS - Add authentication check in production
        [HttpDelete("admin/delete-user")]
        public async Task<IActionResult> DeleteUser([FromQuery] string email, [FromQuery] string adminKey)
        {
            // Simple security check - change this key!
            if (adminKey != "admin-delete-key-2024")
            {
                return Unauthorized("Invalid admin key");
            }

            try
            {
                var deleted = await _securityService.DeleteUserAsync(email);

                if (deleted)
                {
                    _logger.LogInformation("Admin deleted user: {Email}", email);
                    return Ok(new { message = $"User {email} deleted successfully" });
                }
                else
                {
                    return NotFound(new { message = $"User {email} not found" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user: {Email}", email);
                return StatusCode(500, new { message = "Error deleting user" });
            }
        }

        [HttpPost("admin/reset-password")]
        public async Task<IActionResult> ResetUserPassword([FromBody] ResetPasswordRequest request, [FromQuery] string adminKey)
        {
            // Simple security check - change this key!
            if (adminKey != "admin-delete-key-2024")
            {
                return Unauthorized("Invalid admin key");
            }

            try
            {
                var reset = await _securityService.ResetUserPasswordAsync(request.Email, request.NewPassword);

                if (reset)
                {
                    _logger.LogInformation("Admin reset password for: {Email}", request.Email);
                    return Ok(new { message = $"Password reset for {request.Email}" });
                }
                else
                {
                    return NotFound(new { message = $"User {request.Email} not found" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password for: {Email}", request.Email);
                return StatusCode(500, new { message = "Error resetting password" });
            }
        }

        [HttpGet("admin/user-info")]
        public async Task<IActionResult> GetUserInfo([FromQuery] string email, [FromQuery] string adminKey)
        {
            // Simple security check - change this key!
            if (adminKey != "admin-delete-key-2024")
            {
                return Unauthorized("Invalid admin key");
            }

            try
            {
                var user = await _securityService.GetUserByEmailAsync(email);

                if (user == null)
                {
                    return NotFound(new { message = $"User {email} not found" });
                }

                return Ok(new
                {
                    email = user.Email,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    role = user.Role,
                    isConfirmed = user.IsConfirmed,
                    hasSalt = !string.IsNullOrEmpty(user.Salt),
                    saltValue = string.IsNullOrEmpty(user.Salt)   ? "(no salt)"  : user.Salt.Substring(0, Math.Min(10, user.Salt.Length)) + "...",
                    lastLogin = user.LastLoginAt,
                    sessionActive = !string.IsNullOrEmpty(user.CurrentSessionToken),
                    sessionExpires = user.SessionExpiresAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user info: {Email}", email);
                return StatusCode(500, new { message = "Error getting user info" });
            }
        }

        public class LoginRequest
        {
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        public class ResetPasswordRequest
        {
            public string Email { get; set; } = string.Empty;
            public string NewPassword { get; set; } = string.Empty;
        }
    }
}