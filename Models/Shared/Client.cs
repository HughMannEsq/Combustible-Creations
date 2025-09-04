using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutumnRidgeUSA.Models.Shared
{
    public class Client
    {
        [Key]
        public string UserId { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? ZipCode { get; set; }
        public string Email { get; set; } = string.Empty;
        public float Balance { get; set; }
        public DateTime SignupDate { get; set; }

        public string Divisions { get; set; } = string.Empty;

        // Computed property that combines all address fields for dashboard display
        [NotMapped] // This tells Entity Framework not to create a database column for this property
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

        // Updated: Full name property with lastName, firstName format
        [NotMapped]
        public string FullName
        {
            get
            {
                var lastName = LastName?.Trim() ?? "";
                var firstName = FirstName?.Trim() ?? "";

                if (string.IsNullOrEmpty(lastName) && string.IsNullOrEmpty(firstName))
                    return "";

                if (string.IsNullOrEmpty(lastName))
                    return firstName;

                if (string.IsNullOrEmpty(firstName))
                    return lastName;

                return $"{lastName}, {firstName}";
            }
        }
    }
}