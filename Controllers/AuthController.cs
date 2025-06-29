using Microsoft.AspNetCore.Mvc;

namespace AutumnRidgeUSA.Controllers
{
    public class AuthController : Controller
    {
        [HttpPost]
        public IActionResult SwitchRole(string role)
        {
            if (!new[] { "Guest", "Client", "Admin" }.Contains(role))
                return BadRequest();

            // Set the impersonation cookie
            Response.Cookies.Append("ImpersonatedRole", role, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddHours(1),
                HttpOnly = false,
                SameSite = SameSiteMode.Lax,
                Secure = false // Set to true in production with HTTPS
            });

            // Redirect back to the referring page
            string refererUrl = Request.Headers["Referer"].ToString();
            return Redirect(string.IsNullOrEmpty(refererUrl) ? "/" : refererUrl);
        }
    }
}