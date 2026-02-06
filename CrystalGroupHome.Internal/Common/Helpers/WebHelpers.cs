using System.Security.Claims;

namespace CrystalGroupHome.Internal.Common.Helpers
{
    /// <summary>
    /// Helper methods for web application functionality
    /// </summary>
    public static class WebHelpers
    {
        /// <summary>
        /// Extracts the current username from the HTTP context
        /// </summary>
        /// <param name="httpContextAccessor">The HTTP context accessor</param>
        /// <returns>The current username or "SYSTEM" if not authenticated</returns>
        public static string GetCurrentUsername(IHttpContextAccessor httpContextAccessor)
        {
            var user = httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                var username = user.Identity.Name;
                if (!string.IsNullOrEmpty(username))
                {
                    return username.Contains('\\')
                        ? username.Split('\\').Last()
                        : username;
                }
            }

            return "SYSTEM";
        }

        /// <summary>
        /// Extracts the current username from a ClaimsPrincipal
        /// </summary>
        /// <param name="user">The claims principal (typically from HttpContext.User)</param>
        /// <returns>The current username or "SYSTEM" if not authenticated</returns>
        public static string GetCurrentUsername(ClaimsPrincipal? user)
        {
            if (user?.Identity?.IsAuthenticated == true)
            {
                var username = user.Identity.Name;
                if (!string.IsNullOrEmpty(username))
                {
                    return username.Contains('\\')
                        ? username.Split('\\').Last()
                        : username;
                }
            }

            return "SYSTEM";
        }
    }
}