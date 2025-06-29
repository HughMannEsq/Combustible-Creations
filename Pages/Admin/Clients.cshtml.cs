using AutumnRidgeUSA.Models.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;



namespace AutumnRidgeUSA.Pages.Admin
{
    public class ClientsModel : PageModel
    {
        public List<Client> Clients { get; set; }

        public IActionResult OnGet()
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            if (role != "Admin")
            {
                return Unauthorized(); // Or RedirectToPage("/AccessDenied");
            }

            // Replace with real database call later
            Clients = new List<Client>
        {
            new Client { Raaid = "6328-XHN", FirstName = "Alice", LastName= "Smith", Email = "alice@example.com",SignupDate = DateTime.UtcNow.AddDays(-10),   PhoneNumber = "555-1234",
                 Balance = 123.45f, },
            new Client { Raaid = "3829-IFV", FirstName = "Bob", LastName= "Sackley", Email = "bob@example.com", SignupDate = DateTime.UtcNow.AddDays(-5), PhoneNumber = "555-5678",
                 Balance = 67.89f },
            new Client { Raaid = "7832-HIG", FirstName = "Carlos", LastName= "Hogan", Email = "carlos@example.com", SignupDate = DateTime.UtcNow.AddDays(-1), PhoneNumber = "555-8538",
                 Balance = 167.80f } 
        };

            return Page();
        }
    }
}


