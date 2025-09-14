using AutumnRidgeUSA.Models.Shared;
using AutumnRidgeUSA.Models;
using AutumnRidgeUSA.Models.ViewModels;
using AutumnRidgeUSA.Models.Storage;
using AutumnRidgeUSA.Data;
using AutumnRidgeUSA.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AutumnRidgeUSA.Pages.Admin
{
    public class ClientsModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<ClientsModel> _logger;

        public ClientsModel(AppDbContext context, IEmailService emailService, ILogger<ClientsModel> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        // Properties for both tabs
        public List<Client> Clients { get; set; } = new List<Client>();
        public List<TempSignupViewModel> TempSignups { get; set; } = new List<TempSignupViewModel>();

        // Division filtering properties
        public DivisionFilterModel DivisionFilter { get; set; } = new DivisionFilterModel();

        [TempData]
        public string? StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(List<int>? selectedDivisions = null, string? filterOperator = "OR")
        {
            var role = Request.Cookies["ImpersonatedRole"] ?? "Guest";
            if (role != "Admin")
            {
                return RedirectToPage("/Home");
            }

            // Initialize division filter
            DivisionFilter.SelectedDivisionIds = selectedDivisions ?? new List<int>();
            DivisionFilter.FilterOperator = filterOperator ?? "OR";
            await LoadDivisionsAsync();

            // Log the filter parameters for debugging
            _logger.LogInformation("Filter applied - Divisions: [{DivisionIds}], Operator: {Operator}",
                string.Join(", ", DivisionFilter.SelectedDivisionIds), DivisionFilter.FilterOperator);

            // Load data
            await LoadClientsAsync();
            await LoadTempSignupsAsync();

            return Page();
        }

        // NEW: Handler for storage contract modal
        public async Task<IActionResult> OnGetStorageContractAsync(string userId)
        {
            var role = Request.Cookies["ImpersonatedRole"] ?? "Guest";
            if (role != "Admin")
            {
                return new JsonResult(new { success = false, message = "Unauthorized" });
            }

            try
            {
                var user = await _context.Users
                    .Include(u => u.UserDivisions)
                        .ThenInclude(ud => ud.Division)
                    .FirstOrDefaultAsync(u => u.UserId == userId);

                if (user == null)
                {
                    return new JsonResult(new { success = false, message = "User not found" });
                }

                var contracts = await _context.StorageContracts
                    .Where(sc => sc.UserId == user.Id)
                    .OrderByDescending(sc => sc.CreatedAt)
                    .ToListAsync();

                return new JsonResult(new
                {
                    success = true,
                    contracts = contracts.Select(contract => new
                    {
                        id = contract.Id,
                        userId = userId,
                        lockerId = contract.LockerId,
                        securityDeposit = contract.SecurityDeposit,
                        securityDepositReceived = contract.SecurityDepositReceived,
                        monthlyPayment = contract.MonthlyPayment,
                        paymentMethod = contract.PaymentMethod,
                        isActive = contract.IsActive,
                        contractStartDate = contract.ContractStartDate?.ToString("yyyy-MM-dd"),
                        contractEndDate = contract.ContractEndDate?.ToString("yyyy-MM-dd"),
                        createdAt = contract.CreatedAt.ToString("yyyy-MM-dd"),
                        updatedAt = contract.UpdatedAt?.ToString("yyyy-MM-dd")
                    }).ToArray()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching storage contracts for user {UserId}", userId);
                return new JsonResult(new { success = false, message = "Error loading contract details" });
            }
        }

        // Rest of your existing POST methods remain the same...
        public async Task<IActionResult> OnPostDeleteTempSignupAsync(int signupId)
        {
            var role = Request.Cookies["ImpersonatedRole"] ?? "Guest";
            if (role != "Admin")
            {
                return RedirectToPage("/Home");
            }

            try
            {
                var signup = await _context.TempSignups.FindAsync(signupId);
                if (signup != null)
                {
                    var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == signup.Email);
                    if (existingUser == null)
                    {
                        _context.TempSignups.Remove(signup);
                        await _context.SaveChangesAsync();
                        StatusMessage = $"Deleted temporary signup for {signup.Email}";
                        _logger.LogInformation("Admin deleted temp signup for {Email}", signup.Email);
                    }
                    else
                    {
                        StatusMessage = $"Cannot delete - user {signup.Email} has already completed registration";
                        _logger.LogWarning("Attempted to delete completed registration for {Email}", signup.Email);
                    }
                }
                else
                {
                    StatusMessage = "Signup record not found";
                    _logger.LogWarning("Attempted to delete non-existent signup ID: {SignupId}", signupId);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Error deleting signup record";
                _logger.LogError(ex, "Error deleting temp signup ID: {SignupId}", signupId);
            }

            await LoadDivisionsAsync();
            await LoadClientsAsync();
            await LoadTempSignupsAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostCleanupExpiredAsync()
        {
            var role = Request.Cookies["ImpersonatedRole"] ?? "Guest";
            if (role != "Admin")
            {
                return RedirectToPage("/Home");
            }

            try
            {
                var expiredSignups = await _context.TempSignups
                    .Where(t => t.ExpiresAt <= DateTime.UtcNow)
                    .ToListAsync();

                if (expiredSignups.Any())
                {
                    _context.TempSignups.RemoveRange(expiredSignups);
                    await _context.SaveChangesAsync();
                    StatusMessage = $"Deleted {expiredSignups.Count} expired signup(s)";
                    _logger.LogInformation("Cleaned up {Count} expired signups", expiredSignups.Count);
                }
                else
                {
                    StatusMessage = "No expired signups found";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Error cleaning up expired signups";
                _logger.LogError(ex, "Error during cleanup of expired signups");
            }

            await LoadDivisionsAsync();
            await LoadClientsAsync();
            await LoadTempSignupsAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostResendEmailAsync(int signupId)
        {
            var role = Request.Cookies["ImpersonatedRole"] ?? "Guest";
            if (role != "Admin")
            {
                return RedirectToPage("/Home");
            }

            try
            {
                var signup = await _context.TempSignups.FindAsync(signupId);
                if (signup != null && signup.ExpiresAt > DateTime.UtcNow)
                {
                    var verificationLink = $"{Request.Scheme}://{Request.Host}/CompleteRegistration?token={signup.VerificationToken}&userId={signup.UserId}";

                    await _emailService.SendVerificationEmailAsync(
                        signup.Email,
                        signup.FirstName,
                        signup.LastName,
                        signup.UserId,
                        verificationLink
                    );

                    StatusMessage = $"Verification email resent to {signup.Email}";
                    _logger.LogInformation("Admin resent verification email to {Email}", signup.Email);
                }
                else if (signup == null)
                {
                    StatusMessage = "Signup record not found";
                    _logger.LogWarning("Attempted to resend email for non-existent signup ID: {SignupId}", signupId);
                }
                else
                {
                    StatusMessage = "Cannot resend email - signup has expired";
                    _logger.LogWarning("Attempted to resend email for expired signup: {Email}", signup.Email);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to resend verification email";
                _logger.LogError(ex, "Error resending verification email for signup ID: {SignupId}", signupId);
            }

            await LoadDivisionsAsync();
            await LoadClientsAsync();
            await LoadTempSignupsAsync();
            return Page();
        }

     
        private async Task LoadClientsAsync()
        {
            try
            {
                // Start with base query for confirmed clients
                var query = _context.Users
                    .Include(u => u.UserDivisions)
                        .ThenInclude(ud => ud.Division)
                    .Where(u => u.Role == "Client" && u.IsConfirmed);

                // Apply division filtering if any divisions are selected
                if (DivisionFilter.SelectedDivisionIds.Any())
                {
                    if (DivisionFilter.FilterOperator == "AND")
                    {
                        foreach (var divisionId in DivisionFilter.SelectedDivisionIds)
                        {
                            query = query.Where(u => u.UserDivisions.Any(ud => ud.DivisionId == divisionId && ud.IsActive));
                        }
                    }
                    else // OR
                    {
                        query = query.Where(u => u.UserDivisions.Any(ud =>
                            DivisionFilter.SelectedDivisionIds.Contains(ud.DivisionId) && ud.IsActive));
                    }
                }

                var realClients = await query
                    .OrderByDescending(u => u.CreatedAt)
                    .Select(u => new Client
                    {
                        UserId = !string.IsNullOrEmpty(u.UserId) ? u.UserId : $"DB-{u.Id:D4}",
                        FirstName = u.FirstName ?? "Unknown",
                        LastName = u.LastName ?? "User",
                        Email = u.Email,
                        PhoneNumber = u.Phone ?? "N/A",
                        Address = u.Address,
                        City = u.City,
                        State = u.State,
                        ZipCode = u.ZipCode,
                        SignupDate = u.CreatedAt,
                        Balance = 0.0f,
                        Divisions = string.Join(", ", u.UserDivisions
                            .Where(ud => ud.IsActive && ud.Division.IsActive)
                            .Select(ud => ud.Division.Name)
                            .OrderBy(name => name))
                    })
                    .ToListAsync();

                Clients = realClients;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load clients from database");
                Clients = new List<Client>();
            }
        }
        private async Task LoadTempSignupsAsync()
        {
            try
            {
                var tempSignups = await _context.TempSignups
                    .OrderByDescending(t => t.CreatedAt)
                    .ToListAsync();

                var completedEmails = await _context.Users
                    .Where(u => tempSignups.Select(t => t.Email).Contains(u.Email))
                    .Select(u => u.Email)
                    .ToListAsync();

                TempSignups = tempSignups.Select(t => new TempSignupViewModel
                {
                    Id = t.Id,
                    UserId = t.UserId,
                    FirstName = t.FirstName,
                    LastName = t.LastName,
                    Email = t.Email,
                    CreatedAt = t.CreatedAt,
                    ExpiresAt = t.ExpiresAt,
                    IsAuthorized = completedEmails.Contains(t.Email),
                    IsExpired = t.ExpiresAt <= DateTime.UtcNow
                }).ToList();

                _logger.LogInformation("Loaded {Count} temp signups for admin dashboard", TempSignups.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load temp signups from database");
                TempSignups = new List<TempSignupViewModel>();

                if (string.IsNullOrEmpty(StatusMessage))
                {
                    StatusMessage = "Warning: Could not load temporary signups data";
                }
            }
        }

        private async Task LoadDivisionsAsync()
        {
            try
            {
                DivisionFilter.AvailableDivisions = await _context.Divisions
                    .Where(d => d.IsActive)
                    .OrderBy(d => d.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load divisions");
                // Fallback to hardcoded divisions
                DivisionFilter.AvailableDivisions = new List<Division>
                {
                    new Division { Id = 1, Name = "Storage" },
                    new Division { Id = 2, Name = "Contracting" },
                    new Division { Id = 3, Name = "Real Estate" }
                };
            }
        }

        
    }
}