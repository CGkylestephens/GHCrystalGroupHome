using CrystalGroupHome.Internal.Authorization.Requirements;
using CrystalGroupHome.SharedRCL.Data;
using Microsoft.AspNetCore.Authorization;

namespace CrystalGroupHome.Internal.Authorization.Handlers
{
    public class ITAccessHandler : AuthorizationHandler<ITAccessRequirement>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            ITAccessRequirement requirement)
        {
            if (AccessOverrides.IsIT(context.User))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}