using CrystalGroupHome.SharedRCL.Data;
using Microsoft.AspNetCore.Components;
using System.Security.Claims;
using CrystalGroupHome.SharedRCL.Helpers;

namespace CrystalGroupHome.Internal.Features.CMHub.Pages
{
    public class CMHubBase : ComponentBase, IDisposable
    {
        [Inject] public NavigationManager NavigationManager { get; set; } = default!;
        [Inject] public DebugModeService DebugModeService { get; set; } = default!;
        [Inject] public IWebHostEnvironment Environment { get; set; } = default!;

        // AD Group names - reference central ADGroupRoles for consistency
        public const string CMHubAdminRole = ADGroupRoles.CMHubAdmin;
        public const string CustCommsAdminRole = ADGroupRoles.CustCommsAdmin;
        public const string VendorCommsAdminRole = ADGroupRoles.VendorCommsAdmin;
        public const string SalesSupportManagerRole = ADGroupRoles.SalesSupportManager;
        public const string PurchasingRole = ADGroupRoles.Purchasing;
        public const string TechServicesRole = ADGroupRoles.TechServices;

        protected override void OnInitialized()
        {
            DebugModeService.OnChange += StateHasChanged;
        }

        public static bool IsCMHubAdmin(ClaimsPrincipal? user)
        {
            // When impersonating, use impersonated roles exclusively
            if (AccessOverrides.IsImpersonating)
            {
                return AccessOverrides.IsInRole(user, CMHubAdminRole) || AccessOverrides.IsInRole(user, AccessOverrides.ITRole);
            }

            return (user?.IsInRole(CMHubAdminRole) ?? false)
                || AccessOverrides.IsIT(user)
                || AccessOverrides.IsInDebugRole(CMHubAdminRole);
        }

        public static bool HasCMDexEditPermission(ClaimsPrincipal? user)
        {
            // When impersonating, use impersonated roles exclusively
            if (AccessOverrides.IsImpersonating)
            {
                return IsCMHubAdmin(user) || AccessOverrides.IsInRole(user, SalesSupportManagerRole);
            }

            return IsCMHubAdmin(user)
                || (user?.IsInRole(SalesSupportManagerRole) ?? false)
                || AccessOverrides.IsInDebugRole(SalesSupportManagerRole);
        }

        public static bool HasCustCommsTrackerEditPermission(ClaimsPrincipal? user)
        {
            // When impersonating, use impersonated roles exclusively
            if (AccessOverrides.IsImpersonating)
            {
                return IsCMHubAdmin(user) 
                    || AccessOverrides.IsInRole(user, CustCommsAdminRole) 
                    || AccessOverrides.IsInRole(user, PurchasingRole);
            }

            return IsCMHubAdmin(user)
                || (user?.IsInRole(CustCommsAdminRole) ?? false)
                || (user?.IsInRole(PurchasingRole) ?? false)
                || AccessOverrides.IsInDebugRole(CustCommsAdminRole)
                || AccessOverrides.IsInDebugRole(PurchasingRole);
        }

        public static bool HasCustCommsTaskStatusEditPermission(ClaimsPrincipal? user)
        {
            // When impersonating, use impersonated roles exclusively
            if (AccessOverrides.IsImpersonating)
            {
                return IsCMHubAdmin(user) || AccessOverrides.IsInRole(user, CustCommsAdminRole);
            }

            return IsCMHubAdmin(user)
                || (user?.IsInRole(CustCommsAdminRole) ?? false)
                || AccessOverrides.IsInDebugRole(CustCommsAdminRole);
        }

        public static bool HasCMNotifDocumentEditPermission(ClaimsPrincipal? user)
        {
            // When impersonating, use impersonated roles exclusively
            if (AccessOverrides.IsImpersonating)
            {
                return IsCMHubAdmin(user) || AccessOverrides.IsInRole(user, SalesSupportManagerRole);
            }

            return IsCMHubAdmin(user)
                || (user?.IsInRole(SalesSupportManagerRole) ?? false)
                || AccessOverrides.IsInDebugRole(SalesSupportManagerRole);
        }

        public static bool HasCMNotifCreateLogPermission(ClaimsPrincipal? user)
        {
            // When impersonating, use impersonated roles exclusively
            if (AccessOverrides.IsImpersonating)
            {
                return IsCMHubAdmin(user) || AccessOverrides.IsInRole(user, SalesSupportManagerRole);
            }

            return IsCMHubAdmin(user)
                || (user?.IsInRole(SalesSupportManagerRole) ?? false)
                || AccessOverrides.IsInDebugRole(SalesSupportManagerRole);
        }

        public static bool HasVendorCommsEditPermission(ClaimsPrincipal? user)
        {
            // When impersonating, use impersonated roles exclusively
            if (AccessOverrides.IsImpersonating)
            {
                return IsCMHubAdmin(user) || AccessOverrides.IsInRole(user, VendorCommsAdminRole);
            }

            return IsCMHubAdmin(user)
                || (user?.IsInRole(VendorCommsAdminRole) ?? false)
                || AccessOverrides.IsInDebugRole(VendorCommsAdminRole);
        }

        public static bool HasTechServicesEditPermission(ClaimsPrincipal? user)
        {
            // When impersonating, use impersonated roles exclusively
            if (AccessOverrides.IsImpersonating)
            {
                return IsCMHubAdmin(user) || AccessOverrides.IsInRole(user, TechServicesRole);
            }

            return IsCMHubAdmin(user)
                || (user?.IsInRole(TechServicesRole) ?? false)
                || AccessOverrides.IsInDebugRole(TechServicesRole);
        }

        public void NavigateToLink(string link)
        {
            NavigationManager.NavigateTo(link);
        }

        public void Dispose()
        {
            DebugModeService.OnChange -= StateHasChanged;
            GC.SuppressFinalize(this);
        }
    }
}
