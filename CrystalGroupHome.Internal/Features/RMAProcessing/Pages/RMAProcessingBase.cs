using CrystalGroupHome.SharedRCL.Data;
using Microsoft.AspNetCore.Components;
using System.Security.Claims;

namespace CrystalGroupHome.Internal.Features.RMAProcessing.Pages
{
    public class RMAProcessingBase : ComponentBase
    {
        [Inject] public NavigationManager NavigationManager { get; set; } = default!;

        // AD Group names - reference central ADGroupRoles for consistency
        public const string TechServicesAdminRole = ADGroupRoles.TechServicesCoordinator;
        public const string TechServicesRole = ADGroupRoles.IRMATechServices;
        public const string TechServicesReadOnlyAccessRole = ADGroupRoles.AllEmployees;

        public static bool IsTechServicesAdmin(ClaimsPrincipal? user)
        {
            return (user?.IsInRole(TechServicesAdminRole) ?? false)
                || AccessOverrides.IsIT(user);
        }

        public static bool HasFileDeletePermission(ClaimsPrincipal? user)
        {
            return (user?.IsInRole(TechServicesRole) ?? false)
                || IsTechServicesAdmin(user);
        }

        // Permission check for file upload and metadata editing
        public static bool HasFileUploadEditPermission(ClaimsPrincipal? user)
        {
            return (user?.IsInRole(TechServicesRole) ?? false)
                || IsTechServicesAdmin(user) // Admins can also upload/edit
                || AccessOverrides.IsIT(user);
        }

        // Permission check for accessing the entire RMA Processing section
        public static bool HasRMAProcessingAccess(ClaimsPrincipal? user)
        {
            return (user?.IsInRole(TechServicesReadOnlyAccessRole) ?? false);
        }

        public void NavigateToLink(string link)
        {
            NavigationManager.NavigateTo(link);
        }
    }
}