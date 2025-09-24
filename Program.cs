// Program.cs - Updated with PostgreSQL support

using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using AutumnRidgeUSA.Data;
using AutumnRidgeUSA.Services;
using AutumnRidgeUSA.Services.Helpers;
using AutumnRidgeUSA.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Railway port configuration - Railway sets PORT environment variable
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Add services to the container - ALL services must be added BEFORE builder.Build()

// Database configuration - environment-specific
if (builder.Environment.IsDevelopment())
{
    // Keep SQLite for local development
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=app.db"));
}
else
{
    // Use PostgreSQL for Railway production
    var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL");

    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException("DATABASE_URL environment variable not found");
    }

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString));
}

// Core services
builder.Services.AddScoped<ISecurityService, SecurityService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddRazorPages();
builder.Services.AddControllers(); // For MVC controllers

// Migration and admin services
builder.Services.Configure<AdminSecurityOptions>(
    builder.Configuration.GetSection("AdminSecurity"));
builder.Services.AddScoped<IDatabaseMigrationService, DatabaseMigrationService>();
builder.Services.AddScoped<IUserImportService, UserImportService>();
builder.Services.AddScoped<IAdminSecurityService, AdminSecurityService>();
builder.Services.AddScoped<ICsvParsingHelper, CsvParsingHelper>();
builder.Services.AddScoped<IExcelParsingHelper, ExcelParsingHelper>();

// Legacy services (keeping for compatibility)
builder.Services.AddScoped<ExcelImportService>();

// Email Service
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("Email")); // Updated to match your appsettings.json structure
builder.Services.AddScoped<IEmailService, EmailService>();

// Build the app AFTER all services are registered
var app = builder.Build();

// Database initialization
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (app.Environment.IsDevelopment())
    {
        // Development: Use EnsureCreated for SQLite
        context.Database.EnsureCreated();
    }
    else
    {
        // Production: Use proper migrations for PostgreSQL
        try
        {
            context.Database.Migrate();
        }
        catch (Exception ex)
        {
            // Log error but don't crash the app
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Database migration failed");
        }
    }
}

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // Railway handles HTTPS at the load balancer level, so don't force HTTPS redirect
    // app.UseHttpsRedirection(); // Commented out for Railway
    // The default HSTS value is 30 days. You may want to change this for production scenarios.
    app.UseHsts();
}
else
{
    // Only use HTTPS redirect in development
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();

// Add subdomain routing middleware (optional - your banner handles detection)
// app.UseMiddleware<SubdomainRoutingMiddleware>();

// Add the impersonation middleware BEFORE authorization
// NOTE: Remove this in production - it's a security risk!
if (app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        var impersonatedRole = context.Request.Cookies["ImpersonatedRole"];
        var role = !string.IsNullOrEmpty(impersonatedRole) ? impersonatedRole : "Guest";

        if (!string.IsNullOrEmpty(impersonatedRole))
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, "TestUser"),
                new Claim(ClaimTypes.Role, impersonatedRole)
            };

            var identity = new ClaimsIdentity(claims, "Fake");
            var principal = new ClaimsPrincipal(identity);
            context.User = principal;
        }

        await next();
    });
}

app.UseAuthorization();

// Set up routing with subdomain support
app.MapControllerRoute(
    name: "subdomain",
    pattern: "Subdomains/{subdomain}/{action=Index}",
    defaults: new { controller = "Subdomain" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapGet("/", context =>
{
    var subdomain = context.Items["Subdomain"]?.ToString();
    if (!string.IsNullOrEmpty(subdomain))
    {
        // Redirect to subdomain-specific home
        context.Response.Redirect($"/Subdomains/{char.ToUpper(subdomain[0]) + subdomain.Substring(1)}");
    }
    else
    {
        context.Response.Redirect("/Home");
    }
    return Task.CompletedTask;
});

app.MapRazorPages();
app.MapControllers();

app.Run();