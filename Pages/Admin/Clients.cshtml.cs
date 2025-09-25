using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AutumnRidgeUSA.Data;
using AutumnRidgeUSA.Models;
using AutumnRidgeUSA.Models.Shared;

namespace AutumnRidgeUSA.Pages.Admin
{
    public class ClientsModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ClientsModel> _logger;

        public ClientsModel(AppDbContext context, ILogger<ClientsModel> logger)
        {
            _context = context;
            _logger = logger;
        }

        public List<Client> Clients { get; set; } = new();
        public List<TempSignup> TempSignups { get; set; } = new();
        public string StatusMessage { get; set; } = string.Empty;
        public DivisionFilter DivisionFilter { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(string[] selectedDivisions, string filterOperator = "OR")
        {
            try
            {
                // Load available divisions for the filter
                DivisionFilter.AvailableDivisions = await _context.Divisions
                    .Where(d => d.IsActive)
                    .OrderBy(d => d.Name)
                    .ToListAsync();

                DivisionFilter.SelectedDivisionIds = selectedDivisions?.ToList() ?? new List<string>();
                DivisionFilter.FilterOperator = filterOperator;

                // Get all users with "Client" role from the database
                var userQuery = _context.Users
                    .Include(u => u.UserDivisions)
                        .ThenInclude(ud => ud.Division)
                    .Where(u => u.Role == "Client" && u.IsConfirmed);

                // Apply division filtering if selected
                if (DivisionFilter.SelectedDivisionIds.Any())
                {
                    var selectedDivisionIds = DivisionFilter.SelectedDivisionIds
                        .Select(id => int.TryParse(id, out int parsedId) ? parsedId : 0)
                        .Where(id => id > 0)
                        .ToList();

                    if (selectedDivisionIds.Any())
                    {
                        if (filterOperator == "AND")
                        {
                            // User must have ALL selected divisions
                            userQuery = userQuery.Where(u =>
                                selectedDivisionIds.All(divId =>
                                    u.UserDivisions.Any(ud => ud.DivisionId == divId && ud.IsActive)));
                        }
                        else
                        {
                            // User must have ANY of the selected divisions (OR)
                            userQuery = userQuery.Where(u =>
                                u.UserDivisions.Any(ud =>
                                    selectedDivisionIds.Contains(ud.DivisionId) && ud.IsActive));
                        }
                    }
                }

                var users = await userQuery.OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ToListAsync();

                // Convert User entities to Client objects for the view
                Clients = users.Select(user => MapUserToClient(user)).ToList();

                // Load temp signups
                TempSignups = await _context.TempSignups
                    .OrderByDescending(t => t.CreatedAt)
                    .ToListAsync();

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading clients page");
                StatusMessage = "Error loading client data. Please try again.";
                return Page();
            }
        }


        // Convert User entity to Client view model
        private Client MapUserToClient(User user)
        {
            // Calculate balance (you may want to implement this based on your business logic)
            float balance = CalculateUserBalance(user);

            return new Client
            {
                UserId = user.UserId ?? $"USR-{user.Id}",
                FirstName = user.FirstName ?? "Unknown",
                LastName = user.LastName ?? "User",
                Email = user.Email,
                PhoneNumber = user.Phone,
                Address = user.Address,
                City = user.City,
                State = user.State,
                ZipCode = user.ZipCode,
                SignupDate = user.ConfirmedAt ?? user.CreatedAt,
                Balance = balance,
                Divisions = user.DivisionsText // This uses the computed property from your User model
            };
        }

        // Calculate user balance - implement based on your business logic
        private float CalculateUserBalance(User user)
        {
            // TODO: Implement actual balance calculation
            // This might involve:
            // - Storage contract payments
            // - Outstanding invoices
            // - Account credits/debits
            // - etc.

            // For now, return 0 or implement a simple calculation
            return 0.0f;

            // Example implementation if you have a separate Transactions table:
            /*
            var transactions = _context.Transactions
                .Where(t => t.UserId == user.UserId)
                .Sum(t => t.Amount);
            return (float)transactions;
            */
        }

        // Storage contract handler for the modal
        public async Task<IActionResult> OnGetStorageContractAsync(string userId)
        // Storage contract handler for the modal
        {
            try
            {
                // First find the user to get their database ID
                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
                if (user == null)
                {
                    return new JsonResult(new { success = false, message = "User not found." });
                }

                var contractData = await _context.StorageContractUsers
                    .Where(scu => scu.UserId == user.Id && scu.IsActive)
                    .Include(scu => scu.StorageContract)
                        .ThenInclude(sc => sc.StorageUnit)
                    .OrderByDescending(scu => scu.StorageContract.CreatedAt)
                    .Select(scu => new
                    {
                        id = scu.StorageContract.Id,
                        contractNumber = scu.StorageContract.ContractNumber,
                        unitId = scu.StorageContract.StorageUnit.UnitId,
                        unitSize = scu.StorageContract.StorageUnit.UnitSize,
                        moveInDate = scu.StorageContract.MoveInDate,
                        grossRent = scu.StorageContract.GrossRent,
                        paymentCycle = scu.StorageContract.PaymentCycle,
                        securityDeposit = scu.StorageContract.SecurityDeposit,
                        securityDepositBalance = scu.StorageContract.SecurityDepositBalance,
                        isOnline = scu.StorageContract.IsOnline,
                        hasAutopay = scu.StorageContract.HasAutopay,
                        contractStartDate = scu.StorageContract.ContractStartDate,
                        contractEndDate = scu.StorageContract.ContractEndDate,
                        isActive = scu.StorageContract.IsActive,
                        isPrimaryHolder = scu.IsPrimaryContractHolder,
                        accessLevel = scu.AccessLevel,
                        createdAt = scu.StorageContract.CreatedAt,
                        updatedAt = scu.StorageContract.UpdatedAt
                    })
                    .ToListAsync();

                if (contractData.Any())
                {
                    return new JsonResult(new { success = true, contracts = contractData });
                }
                else
                {
                    return new JsonResult(new { success = false, message = "No storage contracts found for this user." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching storage contracts for user {UserId}", userId);
                return new JsonResult(new { success = false, message = "Error loading storage contracts." });
            }
        } 

        // Cleanup expired temp signups
        public async Task<IActionResult> OnPostCleanupExpiredAsync()
        {
            try
            {
                var expiredSignups = await _context.TempSignups
                    .Where(ts => ts.CreatedAt.AddHours(24) < DateTime.UtcNow)
                    .ToListAsync();

                _context.TempSignups.RemoveRange(expiredSignups);
                await _context.SaveChangesAsync();

                StatusMessage = $"Cleaned up {expiredSignups.Count} expired signups.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired signups");
                StatusMessage = "Error cleaning up expired signups.";
            }

            return RedirectToPage();
        }

        // Delete temp signup
        public async Task<IActionResult> OnPostDeleteTempSignupAsync(int signupId)
        {
            try
            {
                var signup = await _context.TempSignups.FindAsync(signupId);
                if (signup != null)
                {
                    _context.TempSignups.Remove(signup);
                    await _context.SaveChangesAsync();
                    StatusMessage = $"Deleted signup for {signup.Email}.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting temp signup {SignupId}", signupId);
                StatusMessage = "Error deleting signup.";
            }

            return RedirectToPage();
        }

        // Resend email for temp signup
        public async Task<IActionResult> OnPostResendEmailAsync(int signupId)
        {
            try
            {
                var signup = await _context.TempSignups.FindAsync(signupId);
                if (signup != null)
                {
                    // TODO: Implement email resending logic
                    // var emailService = HttpContext.RequestServices.GetRequiredService<IEmailService>();
                    // await emailService.ResendSignupEmail(signup);

                    StatusMessage = $"Resent email to {signup.Email}.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resending email for signup {SignupId}", signupId);
                StatusMessage = "Error resending email.";
            }

            return RedirectToPage();
        }
    }

    // Division filter helper class
    public class DivisionFilter
    {
        public List<Division> AvailableDivisions { get; set; } = new();
        public List<string> SelectedDivisionIds { get; set; } = new();
        public string FilterOperator { get; set; } = "OR";
    }
}