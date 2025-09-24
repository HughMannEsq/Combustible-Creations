// Services/AdminSecurityService.cs
using Microsoft.Extensions.Options;

namespace AutumnRidgeUSA.Services
{
    public class AdminSecurityService : IAdminSecurityService
    {
        private readonly AdminSecurityOptions _options;

        public AdminSecurityService(IOptions<AdminSecurityOptions> options)
        {
            _options = options.Value;
        }

        public bool ValidateMigrationKey(string key)
        {
            return !string.IsNullOrEmpty(key) && key == _options.MigrationKey;
        }

        public string GetMigrationKey()
        {
            return _options.MigrationKey;
        }
    }
}

 