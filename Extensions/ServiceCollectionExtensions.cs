// Extensions/ServiceCollectionExtensions.cs
using AutumnRidgeUSA.Services;
using AutumnRidgeUSA.Services.Helpers;

namespace AutumnRidgeUSA.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDatabaseMigrationServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Configure admin security options
            services.Configure<AdminSecurityOptions>(
                configuration.GetSection(AdminSecurityOptions.SectionName));

            // Register core services
            services.AddScoped<IDatabaseMigrationService, DatabaseMigrationService>();
            services.AddScoped<IUserImportService, UserImportService>();
            services.AddScoped<IAdminSecurityService, AdminSecurityService>();

            // Register helper services
            services.AddScoped<ICsvParsingHelper, CsvParsingHelper>();
            services.AddScoped<IExcelParsingHelper, ExcelParsingHelper>();

            return services;
        }
    }
}