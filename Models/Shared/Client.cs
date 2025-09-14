// Models/Shared/Client.cs
namespace AutumnRidgeUSA.Models.Shared
{
    public class Client
    {
        // Basic client properties
        public string UserId { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? ZipCode { get; set; }
        public DateTime SignupDate { get; set; }
        public float Balance { get; set; }
        public string Divisions { get; set; } = string.Empty;

        // Computed properties
        public string FullName => $"{FirstName} {LastName}";

        public string FullAddress
        {
            get
            {
                if (string.IsNullOrEmpty(Address))
                    return string.Empty;

                return $"{Address}, {City}, {State} {ZipCode}";
            }
        }
    }
}