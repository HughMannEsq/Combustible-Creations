using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutumnRidgeUSA.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(10)]
        public string? UserId { get; set; }

        [MaxLength(50)]
        public string? FirstName { get; set; }

        [MaxLength(50)]
        public string? LastName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        // Authentication fields (needed for ALL users)
        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? Salt { get; set; }

        [MaxLength(20)]
        public string Role { get; set; } = "Client";

        // Session management (needed for ALL users)
        [MaxLength(255)]
        public string? CurrentSessionToken { get; set; }
        public DateTime? SessionExpiresAt { get; set; }
        public DateTime? LastLoginAt { get; set; }

        [MaxLength(45)]
        public string? LastLoginIP { get; set; }

        // Core user info
        public string? ConfirmationToken { get; set; }
        public bool IsConfirmed { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ConfirmedAt { get; set; }

        // Phone fields
        [MaxLength(15)]
        public string? Phone { get; set; }

        [MaxLength(10)]
        public string? PhoneType { get; set; }

        [MaxLength(15)]
        public string? Phone2 { get; set; }

        [MaxLength(10)]
        public string? Phone2Type { get; set; }

        // Address fields
        [MaxLength(200)]
        public string? Address { get; set; }

        [MaxLength(50)]
        public string? City { get; set; }

        [MaxLength(2)]
        public string? State { get; set; }

        [MaxLength(10)]
        public string? ZipCode { get; set; }

        // Navigation properties
        public ICollection<UserDivision> UserDivisions { get; set; } = new List<UserDivision>();

        // Computed properties
        [NotMapped]
        public int UserID => Id;

        [NotMapped]
        public string FullName => $"{FirstName} {LastName}".Trim();

        [NotMapped]
        public string PhoneDisplay
        {
            get
            {
                var phones = new List<string>();

                if (!string.IsNullOrEmpty(Phone))
                {
                    var typeCode = PhoneType switch
                    {
                        "Cell" => "(C)",
                        "Home" => "(H)",
                        "Work" => "(W)",
                        _ => ""
                    };
                    phones.Add(string.IsNullOrEmpty(typeCode) ? Phone : $"{Phone}  {typeCode}");
                }

                if (!string.IsNullOrEmpty(Phone2))
                {
                    var typeCode2 = Phone2Type switch
                    {
                        "Cell" => "(C)",
                        "Home" => "(H)",
                        "Work" => "(W)",
                        _ => ""
                    };
                    phones.Add(string.IsNullOrEmpty(typeCode2) ? Phone2 : $"{Phone2}  {typeCode2}");
                }

                return phones.Any() ? string.Join("\n", phones) : "N/A";
            }
        }

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