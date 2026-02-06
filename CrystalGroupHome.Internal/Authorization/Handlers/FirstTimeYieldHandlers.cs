using CrystalGroupHome.Internal.Authorization.Requirements;
using CrystalGroupHome.Internal.Features.FirstTimeYield.Pages;
using Microsoft.AspNetCore.Authorization;

namespace CrystalGroupHome.Internal.Authorization.Handlers
{
    public class FirstTimeYieldAdminHandler : AuthorizationHandler<FirstTimeYieldAdminRequirement>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            FirstTimeYieldAdminRequirement requirement)
        {
            if (FirstTimeYield.IsAdmin(context.User))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}