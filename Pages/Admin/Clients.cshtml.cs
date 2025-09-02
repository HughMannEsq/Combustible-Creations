using AutumnRidgeUSA.Models.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace AutumnRidgeUSA.Pages.Admin
{
    public class ClientsModel : PageModel
    {
        public List<Client> Clients { get; set; } = new List<Client>();

        public IActionResult OnGet()
        {
            // ROLE CHECKING FIX: Changed from claims-based to cookie-based role checking
            // OLD (didn't work): var role = User.FindFirst(ClaimTypes.Role)?.Value;
            // NEW (matches your home page system): Read from the cookie set by AuthController
            var role = Request.Cookies["ImpersonatedRole"] ?? "Guest";

            // Check if user has Admin role - if not, redirect to home page
            if (role != "Admin")
            {
                // Redirect instead of Unauthorized() to provide better user experience
                return RedirectToPage("/Home");
            }

            // MOCK DATA: Replace with real database call later
            // This creates sample client data for testing the admin dashboard
            Clients = new List<Client>
            {
                new Client {
                    UserId = "6328-XHN",
                    FirstName = "Alice",
                    LastName = "Smith",
                    Email = "alice@example.com",
                    SignupDate = DateTime.UtcNow.AddDays(-10),
                    PhoneNumber = "555-1234",
                    Balance = 123.45f
                },
                new Client {
                    UserId = "3829-IFV",
                    FirstName = "Bob",
                    LastName = "Sackley",
                    Email = "bob@example.com",
                    SignupDate = DateTime.UtcNow.AddDays(-5),
                    PhoneNumber = "555-5678",
                    Balance = 67.89f
                },
                new Client {
                    UserId = "7832-HIG",
                    FirstName = "Carlos",
                    LastName = "Hogan",
                    Email = "carlos@example.com",
                    SignupDate = DateTime.UtcNow.AddDays(-1),
                    PhoneNumber = "555-8538",
                    Balance = 167.80f
                }
            };

            // Return the page with loaded client data
            return Page();
        }
    }
}