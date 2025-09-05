// Create this file: Controllers/SubdomainController.cs
using Microsoft.AspNetCore.Mvc;

namespace AutumnRidgeUSA.Controllers
{
    public class SubdomainController : Controller
    {
        [Route("Subdomains/{subdomain}")]
        public IActionResult Index(string subdomain)
        {
            // Validate subdomain
            var validSubdomains = new[] { "contracting", "realestate", "residential", "storage" };
            if (!validSubdomains.Contains(subdomain.ToLower()))
            {
                return NotFound();
            }

            // Store subdomain info in ViewData for the view
            ViewData["Subdomain"] = subdomain.ToLower();
            ViewData["SubdomainDisplay"] = char.ToUpper(subdomain[0]) + subdomain.Substring(1).ToLower();

            // Return the subdomain-specific page
            return View($"~/Pages/Subdomains/{char.ToUpper(subdomain[0]) + subdomain.Substring(1)}/Index.cshtml");
        }
    }
}