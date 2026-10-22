using Microsoft.AspNetCore.Mvc.Filters;

namespace GymFlow.Api.Authorization;

// Reports share one route family; only the financial kinds require the ERP product.
public sealed class ErpReportFilter(ErpEnabledFilter erpFilter) : IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var kind = context.RouteData.Values["kind"] as string;
        return string.Equals(kind, "financial", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "enrollments", StringComparison.OrdinalIgnoreCase)
            ? erpFilter.OnAuthorizationAsync(context)
            : Task.CompletedTask;
    }
}
