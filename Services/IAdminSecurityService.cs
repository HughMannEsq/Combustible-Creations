// Services/IAdminSecurityService.cs
namespace AutumnRidgeUSA.Services
{
    public interface IAdminSecurityService
    {
        bool ValidateMigrationKey(string key);
        string GetMigrationKey();
    }
}