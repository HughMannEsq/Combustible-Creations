// Services/AdminSecurityOptions.cs - Add this new configuration class
namespace AutumnRidgeUSA.Services
{
    public class AdminSecurityOptions
    {
        public const string SectionName = "AdminSecurity";

        public string MigrationKey { get; set; } = "your-secret-migration-key-2024";
        public bool EnableMigrationEndpoints { get; set; } = true;
        public List<string> AllowedIPs { get; set; } = new();
    }
}

// Your existing User.cs model is perfect! It already has all the required properties:
// - All the session management fields are already there
// - Contact information (Phone, Address, City, State, ZipCode) is already there
// - The MaxLength attributes will work great with the migrations

// Your existing EmailSettings.cs is perfect too! The only difference is the property names:
// - Your EmailSettings uses "FromEmail" 
// - I'll update the Program.cs configuration mapping to match your structure