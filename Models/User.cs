using System.ComponentModel.DataAnnotations;

namespace AutumnRidgeUSA.Models
{
    public class User
    {
        public int Id { get; set; }  // Keep your existing primary key

        [MaxLength(50)]
        public string? FirstName { get; set; }  // Made nullable to avoid migration issues

        [MaxLength(50)]
        public string? LastName { get; set; }   // Made nullable to avoid migration issues

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        public string? ConfirmationToken { get; set; }
        public bool IsConfirmed { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ConfirmedAt { get; set; }

        // Optional: Add UserID property if you want both Id and UserID
        // Or you can reference Id as UserID in your business logic
        public int UserID => Id;  // This makes UserID an alias for Id
    }
}