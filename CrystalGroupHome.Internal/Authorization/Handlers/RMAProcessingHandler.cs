using CrystalGroupHome.Internal.Authorization.Requirements;
using CrystalGroupHome.Internal.Features.RMAProcessing.Pages;
using Microsoft.AspNetCore.Authorization;

namespace CrystalGroupHome.Internal.Authorization.Handlers
{
    public class RMAProcessingAccessHandler : AuthorizationHandler<RMAProcessingAccessRequirement>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            RMAProcessingAccessRequirement requirement)
        {
            if (RMAProcessingBase.HasRMAProcessingAccess(context.User))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}