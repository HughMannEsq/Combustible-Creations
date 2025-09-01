using System.ComponentModel.DataAnnotations;
namespace AutumnRidgeUSA.Models
{
    public class User
    {
        public int Id { get; set; }
        [MaxLength(50)]
        public string? FirstName { get; set; }
        [MaxLength(50)]
        public string? LastName { get; set; }
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        // Keep your existing confirmation system
        public string? ConfirmationToken { get; set; }
        public bool IsConfirmed { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ConfirmedAt { get; set; }

        // ADD: Additional fields for complete registration
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

        // Keep your alias
        public int UserID => Id;
    }
}