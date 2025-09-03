using AutumnRidgeUSA.Models.Shared;
using AutumnRidgeUSA.Models.ViewModels;
using AutumnRidgeUSA.Data;
using AutumnRidgeUSA.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AutumnRidgeUSA.Pages.Admin
{
    public class ClientsModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;

        public ClientsModel(AppDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        // Properties for both tabs
        public List<Client> Clients { get; set; } = new List<Client>();
        public List<TempSignupViewModel> TempSignups { get; set; } = new List<TempSignupViewModel>();

        [TempData]
        public string? StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            // ROLE CHECKING: Use cookie-based system to match home page
            var role = Request.Cookies["ImpersonatedRole"] ?? "Guest";
            if (role != "Admin")
            {
                return RedirectToPage("/Home");
            }

            // For debugging - always use mock data
            Clients = GetMockClientData();
            TempSignups = new List<TempSignupViewModel>();

            return Page();
        }

        private List<Client> GetMockClientData()
        {
            return new List<Client>
            {
                new Client {
                    UserId = "6328-XHN",
                    FirstName = "Alice",
                    LastName = "Smith",
                    Email = "alice@example.com",
                    SignupDate = DateTime.UtcNow.AddDays(-10),
                    PhoneNumber = "555-1234",
                    Address = "123 Main St",
                    City = "Battle Creek",
                    State = "MI",
                    ZipCode = "49017",
                    Balance = 123.45f,
                    Divisions = "Storage, Real Estate"
                },
                new Client {
                    UserId = "3829-IFV",
                    FirstName = "Bob",
                    LastName = "Sackley",
                    Email = "bob@example.com",
                    SignupDate = DateTime.UtcNow.AddDays(-5),
                    PhoneNumber = "555-5678",
                    Address = "456 Oak Ave",
                    City = "Kalamazoo",
                    State = "MI",
                    ZipCode = "49001",
                    Balance = 67.89f,
                    Divisions = "Contracting"
                },
                new Client {
                    UserId = "7832-HIG",
                    FirstName = "Carlos",
                    LastName = "Hogan",
                    Email = "carlos@example.com",
                    SignupDate = DateTime.UtcNow.AddDays(-1),
                    PhoneNumber = "555-8538",
                    Address = "789 Pine St",
                    City = "Grand Rapids",
                    State = "MI",
                    ZipCode = "49503",
                    Balance = 167.80f,
                    Divisions = ""
                },
                new Client {
                    UserId = "TEST-001",
                    FirstName = "Test",
                    LastName = "User",
                    Email = "test@example.com",
                    SignupDate = DateTime.UtcNow,
                    PhoneNumber = "555-0000",
                    Address = "Test Address",
                    City = "Test City",
                    State = "MI",
                    ZipCode = "00000",
                    Balance = 999.99f,
                    Divisions = "Storage, Contracting, Real Estate"
                }
            };
        }
    }
}