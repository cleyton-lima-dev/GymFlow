using GymFlow.AccessAgent.Api;

namespace GymFlow.AccessAgent.Services;

public interface IAccessReleaseControl
{
    Task<AccessReleaseConfiguration>
        GetConfigurationAsync(
            CancellationToken cancellationToken);

    Task<bool> IsReleaseEnabledAsync(
        CancellationToken cancellationToken);

    Task ApplyConfigurationAsync(
        bool releaseEnabled,
        long configurationVersion,
        IReadOnlyList<AgentDeviceConfiguration> desiredDevices,
        CancellationToken cancellationToken);
}

public sealed record AccessReleaseConfiguration(
    bool ReleaseEnabled,
    long? ConfigurationVersion,
    IReadOnlyList<AgentDeviceConfiguration> DesiredDevices);