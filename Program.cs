using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using AutumnRidgeUSA.Data;
using AutumnRidgeUSA.Services;
using AutumnRidgeUSA.Middleware; // Add this

var builder = WebApplication.CreateBuilder(args);

// Railway port configuration - Railway sets PORT environment variable
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
builder.Services.AddScoped<ExcelImportService>();

// Add services to the container - ALL services must be added BEFORE builder.Build()

// Database configuration - environment-specific
if (builder.Environment.IsDevelopment())
{
    // Use SQLite for local development
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=app.db"));
}
else
{
    // For Railway production, use SQLite (Railway supports file storage)
    // Use relative path that works in Railway's file system
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite("Data Source=./app.db"));
}

// Add this line with your other service registrations:
builder.Services.AddScoped<ISecurityService, SecurityService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddRazorPages();
builder.Services.AddControllers(); // For MVC controllers

// Register Email Service
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<IEmailService, EmailService>();

// Build the app AFTER all services are registered
var app = builder.Build();

// Database initialization - Railway-friendly approach
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // Always use EnsureCreated for SQLite on Railway
    context.Database.EnsureCreated();
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