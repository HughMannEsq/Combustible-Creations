using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using AutumnRidgeUSA.Data;
using AutumnRidgeUSA.Models;

namespace AutumnRidgeUSA.Services
{
    public interface ISecurityService
    {
        Task<User?> AuthenticateAsync(string email, string password);
        string HashPassword(string password, string salt);
        string GenerateSalt();
        bool VerifyPassword(string password, string hash, string salt);

        Task<string> CreateSessionAsync(User user);
        Task<User?> ValidateSessionAsync(string sessionToken);
        Task LogoutAsync(string sessionToken);

        // Account management
        Task<bool> DeleteUserAsync(string email);
        Task<User?> GetUserByEmailAsync(string email);
        Task<bool> ResetUserPasswordAsync(string email, string newPassword);
    }

    public class SecurityService : ISecurityService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<SecurityService> _logger;

        public SecurityService(AppDbContext context, ILogger<SecurityService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Replace the AuthenticateAsync method in SecurityService.cs with this version
        // This handles users without proper salts

        public async Task<User?> AuthenticateAsync(string email, string password)
        {
            try
            {
                var normalizedEmail = email.ToLower().Trim();

                // Get user with basic fields
                var user = await _context.Users
                    .Where(u => u.Email.ToLower() == normalizedEmail)
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    _logger.LogWarning("Authentication failed - user not found: {Email}", email);
                    return null;
                }

                // Check if account is confirmed
                if (!user.IsConfirmed)
                {
                    _logger.LogWarning("Authentication failed - account not confirmed: {Email}", email);
                    return null;
                }

                // Handle different password storage scenarios

                // Case 1: User has no salt or temp salt marker (legacy/imported users)
                if (string.IsNullOrEmpty(user.Salt) || user.Salt == "TEMP_SALT_NEEDS_RESET")
                {
                    // For users without salt, check if password matches hash directly
                    // This handles imported users or users created before salt implementation
                    if (user.PasswordHash == password)
                    {
                        _logger.LogWarning("Legacy authentication for user without salt: {Email}", email);

                        // Optionally: Update to secure password here
                        // var newSalt = GenerateSalt();
                        // user.Salt = newSalt;
                        // user.PasswordHash = HashPassword(password, newSalt);
                        // await _context.SaveChangesAsync();

                        return user;
                    }

                    _logger.LogWarning("Authentication failed - legacy password mismatch: {Email}", email);
                    return null;
                }

                // Case 2: User has proper salt (new secure users)
                if (!VerifyPassword(password, user.PasswordHash, user.Salt))
                {
                    _logger.LogWarning("Authentication failed - invalid password: {Email}", email);
                    return null;
                }

                _logger.LogInformation("User authenticated successfully: {Email}", email);
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during authentication for {Email}", email);
                return null;
            }
        }

        public string HashPassword(string password, string salt)
        {
            using var sha256 = SHA256.Create();
            var saltedPassword = password + salt;
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(saltedPassword));
            return Convert.ToBase64String(hashBytes);
        }

        public string GenerateSalt()
        {
            var saltBytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(saltBytes);
            return Convert.ToBase64String(saltBytes);
        }

        public bool VerifyPassword(string password, string hash, string salt)
        {
            var computedHash = HashPassword(password, salt);
            return computedHash == hash;
        }

        // ADD these methods to your existing SecurityService class:



        public async Task<string> CreateSessionAsync(User user)
        {
            var sessionToken = Guid.NewGuid().ToString("N");

            user.CurrentSessionToken = sessionToken;
            user.SessionExpiresAt = DateTime.UtcNow.AddHours(8); // Match cookie expiration
            user.LastLoginAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Session created for user: {Email}", user.Email);
            return sessionToken;
        }

        public async Task<User?> ValidateSessionAsync(string sessionToken)
        {
            if (string.IsNullOrEmpty(sessionToken))
                return null;

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.CurrentSessionToken == sessionToken);

            if (user == null)
                return null;

            // Check if session expired
            if (user.SessionExpiresAt == null || user.SessionExpiresAt <= DateTime.UtcNow)
            {
                // Session expired, clear it
                user.CurrentSessionToken = null;
                user.SessionExpiresAt = null;
                await _context.SaveChangesAsync();
                return null;
            }

            return user;
        }

        public async Task LogoutAsync(string sessionToken)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.CurrentSessionToken == sessionToken);

            if (user != null)
            {
                user.CurrentSessionToken = null;
                user.SessionExpiresAt = null;
                await _context.SaveChangesAsync();

                _logger.LogInformation("User logged out: {Email}", user.Email);
            }
        }

        // Account management methods
        public async Task<bool> DeleteUserAsync(string email)
        {
            try
            {
                var normalizedEmail = email.ToLower().Trim();
                var user = await _context.Users
                    .Include(u => u.UserDivisions)
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);

                if (user == null)
                {
                    _logger.LogWarning("Cannot delete - user not found: {Email}", email);
                    return false;
                }

                // Remove related UserDivisions first
                _context.UserDivisions.RemoveRange(user.UserDivisions);

                // Remove the user
                _context.Users.Remove(user);

                await _context.SaveChangesAsync();

                _logger.LogInformation("User deleted successfully: {Email}", email);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user: {Email}", email);
                return false;
            }
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            var normalizedEmail = email.ToLower().Trim();
            return await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);
        }

        public async Task<bool> ResetUserPasswordAsync(string email, string newPassword)
        {
            try
            {
                var user = await GetUserByEmailAsync(email);
                if (user == null)
                {
                    _logger.LogWarning("Cannot reset password - user not found: {Email}", email);
                    return false;
                }

                // Generate new salt and hash
                var newSalt = GenerateSalt();
                var newHash = HashPassword(newPassword, newSalt);

                user.Salt = newSalt;
                user.PasswordHash = newHash;

                // Clear any existing sessions
                user.CurrentSessionToken = null;
                user.SessionExpiresAt = null;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Password reset successfully for: {Email}", email);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password for: {Email}", email);
                return false;
            }
        }
    }
}