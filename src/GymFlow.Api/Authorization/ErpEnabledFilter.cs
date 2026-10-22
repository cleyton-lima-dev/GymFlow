using GymFlow.Application.Interfaces.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GymFlow.Api.Authorization;

/// <summary>
/// Server-side entitlement check for ERP endpoints. UI visibility is not an authorization boundary.
/// </summary>
public sealed class ErpEnabledFilter(IGymErpPolicy erpPolicy) : IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (!Guid.TryParse(context.HttpContext.User.FindFirst("gym_id")?.Value, out var gymId) || gymId == Guid.Empty)
        {
            context.Result = new UnauthorizedResult();
            return Task.CompletedTask;
        }

        if (!erpPolicy.IsEnabled(gymId))
            context.Result = new ForbidResult();

        return Task.CompletedTask;
    }
}
