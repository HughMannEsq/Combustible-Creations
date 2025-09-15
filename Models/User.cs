using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutumnRidgeUSA.Models
{
    public class User
    {

        // For secure password hashing
        [MaxLength(255)]
        public string? Salt { get; set; }

        // For tracking login activity  
        public DateTime? LastLoginAt { get; set; }

        [MaxLength(45)]  // IPv6 max length
        public string? LastLoginIP { get; set; }
        [Key]
        public int Id { get; set; }

        // ADD: This is what your admin dashboard needs for display
        [MaxLength(10)]
        public string? UserId { get; set; }  // Format: "####-ABC" - gets set during registration completion

        [MaxLength(50)]
        public string? FirstName { get; set; }

        [MaxLength(50)]
        public string? LastName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        public ICollection<UserDivision> UserDivisions { get; set; } = new List<UserDivision>();

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        // Keep your existing confirmation system
        public string? ConfirmationToken { get; set; }
        public bool IsConfirmed { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ConfirmedAt { get; set; }

        // Address fields for complete registration
        [MaxLength(15)]
        public string? Phone { get; set; }

        [MaxLength(200)]
        public string? Address { get; set; }

        [MaxLength(50)]
        public string? City { get; set; }

        [MaxLength(2)]
        public string? State { get; set; }

        [MaxLength(10)]
        public string? ZipCode { get; set; }

        [MaxLength(20)]
        public string Role { get; set; } = "Client";

        // Keep your alias for backward compatibility
        [NotMapped]
        public int UserID => Id;

        // Computed properties for display (optional)
        [NotMapped]
        public string FullName => $"{FirstName} {LastName}".Trim();

        [NotMapped]
        public string DivisionsText
        {
            get
            {
                var activeDivisions = UserDivisions?
                    .Where(ud => ud.IsActive && ud.Division.IsActive)
                    .Select(ud => ud.Division.Name)
                    .OrderBy(name => name)
                    .ToList() ?? new List<string>();

                return activeDivisions.Any() ? string.Join(", ", activeDivisions) : "None";
            }
        }
        [NotMapped]
        public string FullAddress
        {
            get
            {
                var addressParts = new[]
                {
                    Address?.Trim(),
                    City?.Trim(),
                    State?.Trim(),
                    ZipCode?.Trim()
                }.Where(part => !string.IsNullOrEmpty(part));
                return string.Join(", ", addressParts);
            }
        }
    }
}