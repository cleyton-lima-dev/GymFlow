using System.Security.Claims;

namespace GymFlow.Api.Maintenance;

public sealed class GymMaintenanceMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, GymMaintenanceService maintenance)
    {
        // Liveness is global and exposes no tenant data.
        if (context.Request.Path.Equals("/health", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }
        if (context.User.Identity?.IsAuthenticated == true &&
            Guid.TryParse(context.User.FindFirst("gym_id")?.Value, out var gymId) && gymId != Guid.Empty)
        {
            var isAgent = context.User.FindFirst("identity_type")?.Value == "access_agent";
            var userId = Guid.TryParse(context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var parsed)
                ? parsed : Guid.Empty;
            if (isAgent || !await maintenance.IsAllowedAsync(gymId, userId, context.RequestAborted))
            {
                var active = await maintenance.GetAsync(gymId, context.RequestAborted);
                if (active?.IsActive == true)
                {
                    await DenyAsync(context);
                    return;
                }
            }
        }
        await next(context);
    }

    public static async Task DenyAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.RetryAfter = "300";
        await context.Response.WriteAsJsonAsync(new { code = "GYM_MAINTENANCE",
            message = "Academia temporariamente em manutenção." });
    }
}
