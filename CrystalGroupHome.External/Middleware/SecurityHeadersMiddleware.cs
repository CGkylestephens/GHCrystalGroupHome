using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace CrystalGroupHome.External.Middleware
{
    /// <summary>
    /// Security headers middleware for Content Security Policy (CSP)
    /// Note: Other security headers (X-Content-Type-Options, X-Frame-Options, X-XSS-Protection, 
    /// Referrer-Policy, Permissions-Policy, Strict-Transport-Security) are now configured at the IIS level
    /// to avoid duplication and centralize management across multiple applications on the same server.
    /// </summary>
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IHostEnvironment _environment;

        public SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment environment)
        {
            _next = next;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Generate cryptographically secure nonce for this request
            var nonce = GenerateNonce();
            
            // Store nonce in HttpContext.Items for access in pages/components
            context.Items["csp-nonce"] = nonce;

            // Build connect-src directive - add localhost for Browser Link in development
            var connectSrc = _environment.IsDevelopment() 
                ? "'self' wss: ws: http://localhost:* https://localhost:*" 
                : "'self' wss: ws:";

            // Content Security Policy with nonce support
            // Configured for Blazor Server with external CDN resources
            // This is application-specific and cannot be managed at IIS level due to nonce requirement
            context.Response.Headers.Append("Content-Security-Policy",
                $"default-src 'self'; " +
                $"script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +  // Blazor Server requirements (no nonce)
                $"style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://cdnjs.cloudflare.com; " +  // Blazor Server requirements
                $"img-src 'self' data: https:; " +
                $"font-src 'self' data: https://fonts.gstatic.com https://cdnjs.cloudflare.com; " +
                $"connect-src {connectSrc}; " +
                $"frame-ancestors 'none'; " +
                $"base-uri 'self'; " +
                $"form-action 'self'; " +
                $"upgrade-insecure-requests");  // Upgrade HTTP requests to HTTPS

            // Call next middleware in pipeline
            await _next(context);
        }

        /// <summary>
        /// Generates a cryptographically secure nonce (32 bytes = 256 bits)
        /// Used for CSP script-src and style-src directives
        /// </summary>
        private static string GenerateNonce()
        {
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return Convert.ToBase64String(bytes);
        }
    }

    /// <summary>
    /// Extension method to register security headers middleware
    /// </summary>
    public static class SecurityHeadersMiddlewareExtensions
    {
        public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SecurityHeadersMiddleware>();
        }
    }
}
