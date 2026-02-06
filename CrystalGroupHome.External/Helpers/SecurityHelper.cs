using Microsoft.AspNetCore.Http;

namespace CrystalGroupHome.External.Helpers
{
    /// <summary>
    /// Helper methods for accessing security-related context items
    /// Used to retrieve CSP nonce for inline scripts and styles
    /// </summary>
    public static class SecurityHelper
    {
        /// <summary>
        /// Gets the CSP nonce for the current HTTP request
        /// This nonce must be added to inline script and style tags for CSP compliance
        /// </summary>
        /// <param name="httpContext">The current HTTP context</param>
        /// <returns>The nonce string, or null if not available</returns>
        public static string? GetCspNonce(HttpContext? httpContext)
        {
            return httpContext?.Items["csp-nonce"] as string;
        }
    }
}
