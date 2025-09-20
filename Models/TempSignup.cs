using System.ComponentModel.DataAnnotations.Schema;

namespace AutumnRidgeUSA.Models
{
    public class TempSignup
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string VerificationToken { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        // Add these computed properties to your TempSignup model
        [NotMapped]
        public bool IsExpired => CreatedAt.AddHours(24) < DateTime.UtcNow;

        [NotMapped]
        public bool IsAuthorized => !string.IsNullOrEmpty(VerificationToken); // Adjust logic as needed

        [NotMapped]
        public string TimeRemaining
        {
            get
            {
                var timeLeft = CreatedAt.AddHours(24) - DateTime.UtcNow;
                if (timeLeft.TotalMinutes < 0) return "EXPIRED";
                return $"{timeLeft.Hours}h {timeLeft.Minutes}m remaining";
            }
        }
    }
}