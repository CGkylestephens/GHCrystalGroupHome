using CrystalGroupHome.Internal.Authorization.Requirements;
using CrystalGroupHome.Internal.Features.CMHub.Pages;
using Microsoft.AspNetCore.Authorization;

namespace CrystalGroupHome.Internal.Authorization.Handlers
{
    public class CMHubAdminHandler : AuthorizationHandler<CMHubAdminRequirement>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            CMHubAdminRequirement requirement)
        {
            if (CMHubBase.IsCMHubAdmin(context.User))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }

    public class CMHubVendorCommsEditHandler : AuthorizationHandler<CMHubVendorCommsEditRequirement>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            CMHubVendorCommsEditRequirement requirement)
        {
            if (CMHubBase.HasVendorCommsEditPermission(context.User))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }

    public class CMHubCustCommsEditHandler : AuthorizationHandler<CMHubCustCommsEditRequirement>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            CMHubCustCommsEditRequirement requirement)
        {
            if (CMHubBase.HasCustCommsTrackerEditPermission(context.User))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }

    public class CMHubCustCommsTaskStatusEditHandler : AuthorizationHandler<CMHubCustCommsTaskStatusEditRequirement>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            CMHubCustCommsTaskStatusEditRequirement requirement)
        {
            if (CMHubBase.HasCustCommsTaskStatusEditPermission(context.User))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }

    public class CMHubCMDexEditHandler : AuthorizationHandler<CMHubCMDexEditRequirement>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            CMHubCMDexEditRequirement requirement)
        {
            if (CMHubBase.HasCMDexEditPermission(context.User))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }

    public class CMHubCMNotifDocumentEditHandler : AuthorizationHandler<CMHubCMNotifDocumentEditRequirement>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            CMHubCMNotifDocumentEditRequirement requirement)
        {
            if (CMHubBase.HasCMNotifDocumentEditPermission(context.User))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }

    public class CMHubCMNotifCreateLogHandler : AuthorizationHandler<CMHubCMNotifCreateLogRequirement>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            CMHubCMNotifCreateLogRequirement requirement)
        {
            if (CMHubBase.HasCMNotifCreateLogPermission(context.User))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }

    public class CMHubTechServicesEditHandler : AuthorizationHandler<CMHubTechServicesEditRequirement>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            CMHubTechServicesEditRequirement requirement)
        {
            if (CMHubBase.HasTechServicesEditPermission(context.User))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}