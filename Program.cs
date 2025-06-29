using System.Security.Claims;
using AutumnRidgeUSA.Data;
using AutumnRidgeUSA.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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
    // Use Azure SQL Database for production
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("AzureConnection")));
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddRazorPages();
builder.Services.AddControllers(); // For MVC controllers

// Register Email Service
builder.Services.AddScoped<IEmailService, EmailService>();

// Build the app AFTER all services are registered
var app = builder.Build();

// Database initialization - different approach for Azure
if (app.Environment.IsDevelopment())
{
    // For development, ensure database is created
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Database.EnsureCreated();
    }
}
else
{
    // For production, use migrations instead of EnsureCreated
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Only apply pending migrations - don't create/recreate database
        context.Database.Migrate();
    }
}

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

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

// Set up routing
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapGet("/", context =>
{
    context.Response.Redirect("/Home");
    return Task.CompletedTask;
});

app.MapRazorPages();

app.Run();