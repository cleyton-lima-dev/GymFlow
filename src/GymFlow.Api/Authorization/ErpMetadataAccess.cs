using GymFlow.Application.Interfaces.Security;

namespace GymFlow.Api.Authorization;

// Optional ERP enrichment must fail closed without taking the shared student registry offline.
public sealed class ErpMetadataAccess(IGymErpPolicy policy, ILogger<ErpMetadataAccess> logger)
{
    public bool IsEnabled(Guid gymId)
    {
        try { return policy.IsEnabled(gymId); }
        catch (Exception ex)
        {
            logger.LogError(ex, "ERP metadata authorization unavailable for gym {GymId}; omitting enrichment", gymId);
            return false;
        }
    }
}
