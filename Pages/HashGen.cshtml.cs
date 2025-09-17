using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AutumnRidgeUSA.Services;

namespace AutumnRidgeUSA.Pages
{
    public class HashGenModel : PageModel
    {
        private readonly ISecurityService _securityService;

        public HashGenModel(ISecurityService securityService)
        {
            _securityService = securityService;
        }

        public string? Salt { get; set; }
        public string? Hash { get; set; }

        public void OnGet()
        {
        }

        public IActionResult OnPost(string password)
        {
            Salt = _securityService.GenerateSalt();
            Hash = _securityService.HashPassword(password, Salt);
            return Page();
        }
    }
}