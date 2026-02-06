using System.Security.Claims;
using CrystalGroupHome.SharedRCL.Services;

namespace CrystalGroupHome.SharedRCL.Data
{
    /// <summary>
    /// Provides permission override checks, particularly for impersonation scenarios.
    /// Uses a simple static holder that must be set before each render cycle by MainLayout.
    /// 
    /// IMPORTANT: Impersonation is completely disabled in production environments.
    /// The ImpersonationService itself enforces this, providing defense in depth.
    /// </summary>
    public static class AccessOverrides
    {
        /// <summary>
        /// IT Role constant - kept for backward compatibility, use ADGroupRoles.IT instead
        /// </summary>
        public const string ITRole = ADGroupRoles.IT;

        /// <summary>
        /// The current impersonation service. This is set by MainLayout before each render
        /// and during OnChange events. In Blazor Server, renders are single-threaded per circuit,
        /// so this pattern works as long as it's set at the start of each render cycle.
        /// </summary>
        public static ImpersonationService? ImpersonationService { get; set; }

        /// <summary>
        /// Checks if the user is an IT user, taking impersonation into account.
        /// When impersonating, only returns true if the impersonated persona has IT role.
        /// Impersonation checks are disabled in production (enforced by ImpersonationService).
        /// </summary>
        public static bool IsIT(ClaimsPrincipal? user)
        {
            // ImpersonationService.IsImpersonating already returns false in production
            if (ImpersonationService?.IsImpersonating == true)
            {
                return ImpersonationService.HasRole(ITRole);
            }

            return user?.IsInRole(ITRole) ?? false;
        }

        /// <summary>
        /// Checks if the user should be considered as having a specific role,
        /// taking impersonation into account.
        /// Impersonation checks are disabled in production (enforced by ImpersonationService).
        /// </summary>
        /// <param name="user">The current user's ClaimsPrincipal</param>
        /// <param name="role">The role to check</param>
        /// <returns>True if the user (or impersonated persona) has the role</returns>
        public static bool IsInRole(ClaimsPrincipal? user, string role)
        {
            // ImpersonationService.IsImpersonating already returns false in production
            if (ImpersonationService?.IsImpersonating == true)
            {
                return ImpersonationService.HasRole(role);
            }

            // Not impersonating - check real user's roles
            return user?.IsInRole(role) ?? false;
        }

        /// <summary>
        /// Checks if impersonation is active and the persona has the specified role.
        /// This is the primary method for checking debug/impersonation roles.
        /// Always returns false in production (enforced by ImpersonationService).
        /// </summary>
        public static bool IsInDebugRole(string role)
        {
            // ImpersonationService.IsImpersonating already returns false in production
            if (ImpersonationService?.IsImpersonating == true)
            {
                return ImpersonationService.HasRole(role);
            }

            return false;
        }

        /// <summary>
        /// Checks if impersonation is currently active and would DENY a specific role.
        /// This is used to ensure that when impersonating, we don't fall through to the real user's permissions.
        /// Always returns false in production (enforced by ImpersonationService).
        /// </summary>
        /// <param name="role">The role to check</param>
        /// <returns>True if impersonating and the persona does NOT have the role</returns>
        public static bool IsDeniedByImpersonation(string role)
        {
            // ImpersonationService.IsImpersonating already returns false in production
            if (ImpersonationService?.IsImpersonating != true)
                return false;

            return ImpersonationService.IsDeniedRole(role);
        }

        /// <summary>
        /// Checks whether impersonation is currently active.
        /// Always returns false in production (enforced by ImpersonationService).
        /// </summary>
        public static bool IsImpersonating => ImpersonationService?.IsImpersonating == true;
    }
}
