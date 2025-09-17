using AutumnRidgeUSA.Services;
using Microsoft.AspNetCore.Mvc;

namespace AutumnRidgeUSA.Controllers
{
    public class LoginApiController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
using Microsoft.AspNetCore.Mvc;
using AutumnRidgeUSA.Services;

namespace AutumnRidgeUSA.Controllers
{
    [ApiController]
    [Route("api")]
    public class LoginApiController : ControllerBase
    {
        private readonly ISecurityService _securityService;
        private readonly ILogger<LoginApiController> _logger;

        public LoginApiController(ISecurityService securityService, ILogger<LoginApiController> logger)
        {
            _securityService = securityService;
            _logger = logger;
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
                    // For Phase 2, we'll just use the role switcher cookie mechanism
                    // In Phase 3, we'll add proper session management
                    Response.Cookies.Append("ImpersonatedRole", user.Role, new CookieOptions
                    {
                        Expires = DateTimeOffset.UtcNow.AddHours(8),
                        HttpOnly = false,
                        SameSite = SameSiteMode.Lax,
                        Secure = false // Set to true in production with HTTPS
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
                            role = user.Role
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
    }

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}