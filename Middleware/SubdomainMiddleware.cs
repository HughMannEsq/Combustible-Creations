// Create this file: Middleware/SubdomainRoutingMiddleware.cs
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace AutumnRidgeUSA.Middleware
{
    public class SubdomainRoutingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string[] _validSubdomains = { "contracting", "realestate", "residential", "storage" };

        public SubdomainRoutingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var request = context.Request;
            var host = request.Host.Host.ToLower();
            var path = request.Path.Value?.ToLower() ?? "/";
            var isLocalhost = host.Contains("localhost");

            string? subdomain = null;

            // Detect subdomain using the same logic as your banner
            if (!isLocalhost)
            {
                // Production: extract from host
                var parts = host.Split('.');
                if (parts.Length > 2 && !host.StartsWith("www."))
                {
                    subdomain = parts[0];
                }
            }
            else
            {
                // Localhost: extract from path
                var pathSegments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (pathSegments.Length > 0 && _validSubdomains.Contains(pathSegments[0]))
                {
                    subdomain = pathSegments[0];
                }
            }

            // If we have a valid subdomain and this is a root request, redirect to subdomain page
            if (!string.IsNullOrEmpty(subdomain) &&
                _validSubdomains.Contains(subdomain) &&
                (path == "/" || path == "/home"))
            {
                // For localhost, redirect to /Subdomains/[Division]
                // For production, serve the subdomain page directly
                if (isLocalhost)
                {
                    context.Response.Redirect($"/Subdomains/{char.ToUpper(subdomain[0])}{subdomain.Substring(1)}");
                    return;
                }
                else
                {
                    // For production subdomains, rewrite path to serve subdomain content
                    context.Request.Path = $"/Subdomains/{char.ToUpper(subdomain[0])}{subdomain.Substring(1)}";
                }
            }

            await _next(context);
        }
    }
}