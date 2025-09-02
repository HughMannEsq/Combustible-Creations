using System;

namespace AutumnRidgeUSA.Models.ViewModels
{
    public class TempSignupViewModel
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsAuthorized { get; set; }
        public bool IsExpired { get; set; }

        public string TimeRemaining
        {
            get
            {
                if (IsExpired)
                    return "EXPIRED";

                var remaining = ExpiresAt - DateTime.UtcNow;
                if (remaining.TotalMinutes < 1)
                    return "< 1 minute";
                else if (remaining.TotalHours < 1)
                    return $"{(int)remaining.TotalMinutes} minutes";
                else
                    return $"{(int)remaining.TotalHours}h {remaining.Minutes}m";
            }
        }
    }
}