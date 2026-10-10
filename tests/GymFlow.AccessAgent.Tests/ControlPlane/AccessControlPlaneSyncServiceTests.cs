using System.Net;
using GymFlow.AccessAgent.Abstractions;
using GymFlow.AccessAgent.Api;
using GymFlow.AccessAgent.ControlPlane;
using GymFlow.AccessAgent.Offline;
using GymFlow.AccessAgent.Security;
using GymFlow.AccessAgent.Services;
using NSubstitute;

namespace GymFlow.AccessAgent.Tests.ControlPlane;

public class AccessControlPlaneSyncServiceTests
{
    [Fact]
    public async Task SyncAsync_WhenAgentIsNotPaired_ShouldReturnFalse()
    {
        var credentialStore =
            Substitute.For<IAgentCredentialStore>();

        var apiClient =
            Substitute.For<IAvelriAccessApiClient>();

        var sessionService =
            Substitute.For<IAgentSessionService>();

        var releaseControl =
            Substitute.For<IAccessReleaseControl>();

        var offlineStore =
            Substitute.For<IAccessOfflineStore>();

        credentialStore
            .LoadAsync(
                Arg.Any<CancellationToken>())
            .Returns(
                (AgentCredentials?)null);

        var service =
            CreateService(
                credentialStore,
                apiClient,
                sessionService,
                releaseControl,
                offlineStore,
                []);

        var result =
            await service.SyncAsync(
                CancellationToken.None);

        Assert.False(
            result);
    }

    [Fact]
    public async Task SyncAsync_WhenConfigurationIsCurrent_ShouldNotRewriteLocalState()
    {
        var dependencies =
            CreateDependencies();

        dependencies.ReleaseControl
            .GetConfigurationAsync(
                Arg.Any<CancellationToken>())
            .Returns(
                new AccessReleaseConfiguration(
                    false,
                    1));

        dependencies.ApiClient
            .HeartbeatAsync(
                dependencies.Credentials,
                "token",
                1,
                0,
                Arg.Any<
                    IReadOnlyCollection<
                        AccessDeviceRuntimeStatus>>(),
                Arg.Any<DateTime?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(
                new AgentControlPlaneHeartbeatResult(
                    false,
                    1,
                    DateTime.UtcNow));

        var result =
            await dependencies.Service
                .SyncAsync(
                    CancellationToken.None);

        Assert.True(
            result);

        await dependencies.ReleaseControl
            .DidNotReceive()
            .ApplyConfigurationAsync(
                Arg.Any<bool>(),
                Arg.Any<long>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_WhenConfigurationChanged_ShouldApplyAndConfirmVersion()
    {
        var dependencies =
            CreateDependencies();

        dependencies.ReleaseControl
            .GetConfigurationAsync(
                Arg.Any<CancellationToken>())
            .Returns(
                new AccessReleaseConfiguration(
                    false,
                    1));

        dependencies.ApiClient
            .HeartbeatAsync(
                dependencies.Credentials,
                "token",
                1,
                0,
                Arg.Any<
                    IReadOnlyCollection<
                        AccessDeviceRuntimeStatus>>(),
                Arg.Any<DateTime?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(
                new AgentControlPlaneHeartbeatResult(
                    true,
                    2,
                    DateTime.UtcNow));

        dependencies.ApiClient
            .HeartbeatAsync(
                dependencies.Credentials,
                "token",
                2,
                0,
                Arg.Any<
                    IReadOnlyCollection<
                        AccessDeviceRuntimeStatus>>(),
                Arg.Any<DateTime?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(
                new AgentControlPlaneHeartbeatResult(
                    true,
                    2,
                    DateTime.UtcNow));

        var result =
            await dependencies.Service
                .SyncAsync(
                    CancellationToken.None);

        Assert.True(
            result);

        await dependencies.ReleaseControl
            .Received(1)
            .ApplyConfigurationAsync(
                true,
                2,
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_WhenConfigurationChangesDuringConfirmation_ShouldApplyNewestState()
    {
        var dependencies =
            CreateDependencies();

        dependencies.ReleaseControl
            .GetConfigurationAsync(
                Arg.Any<CancellationToken>())
            .Returns(
                new AccessReleaseConfiguration(
                    false,
                    1));

        dependencies.ApiClient
            .HeartbeatAsync(
                dependencies.Credentials,
                "token",
                1,
                0,
                Arg.Any<
                    IReadOnlyCollection<
                        AccessDeviceRuntimeStatus>>(),
                Arg.Any<DateTime?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(
                new AgentControlPlaneHeartbeatResult(
                    true,
                    2,
                    DateTime.UtcNow));

        dependencies.ApiClient
            .HeartbeatAsync(
                dependencies.Credentials,
                "token",
                2,
                0,
                Arg.Any<
                    IReadOnlyCollection<
                        AccessDeviceRuntimeStatus>>(),
                Arg.Any<DateTime?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(
                new AgentControlPlaneHeartbeatResult(
                    false,
                    3,
                    DateTime.UtcNow));

        var result =
            await dependencies.Service
                .SyncAsync(
                    CancellationToken.None);

        Assert.True(
            result);

        await dependencies.ReleaseControl
            .Received(1)
            .ApplyConfigurationAsync(
                true,
                2,
                Arg.Any<CancellationToken>());

        await dependencies.ReleaseControl
            .Received(1)
            .ApplyConfigurationAsync(
                false,
                3,
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_WhenTokenExpired_ShouldRefreshAndRetry()
    {
        var credentialStore =
            Substitute.For<IAgentCredentialStore>();

        var apiClient =
            Substitute.For<IAvelriAccessApiClient>();

        var sessionService =
            Substitute.For<IAgentSessionService>();

        var releaseControl =
            Substitute.For<IAccessReleaseControl>();

        var offlineStore =
            Substitute.For<IAccessOfflineStore>();

        var credentials =
            new AgentCredentials(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "secret",
                "https://api.avelri.test");

        credentialStore
            .LoadAsync(
                Arg.Any<CancellationToken>())
            .Returns(
                credentials);

        releaseControl
            .GetConfigurationAsync(
                Arg.Any<CancellationToken>())
            .Returns(
                new AccessReleaseConfiguration(
                    false,
                    1));

        offlineStore
            .CountPendingEventsAsync(
                Arg.Any<CancellationToken>())
            .Returns(
                0);

        sessionService
            .GetTokenAsync(
                credentials,
                Arg.Any<CancellationToken>())
            .Returns(
                "expired-token",
                "fresh-token");

        apiClient
            .HeartbeatAsync(
                credentials,
                "expired-token",
                1,
                0,
                Arg.Any<
                    IReadOnlyCollection<
                        AccessDeviceRuntimeStatus>>(),
                Arg.Any<DateTime?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns<
                Task<
                    AgentControlPlaneHeartbeatResult>>(
                _ => throw new HttpRequestException(
                    "Unauthorized",
                    null,
                    HttpStatusCode.Unauthorized));

        apiClient
            .HeartbeatAsync(
                credentials,
                "fresh-token",
                1,
                0,
                Arg.Any<
                    IReadOnlyCollection<
                        AccessDeviceRuntimeStatus>>(),
                Arg.Any<DateTime?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(
                new AgentControlPlaneHeartbeatResult(
                    false,
                    1,
                    DateTime.UtcNow));

        var service =
            CreateService(
                credentialStore,
                apiClient,
                sessionService,
                releaseControl,
                offlineStore,
                []);

        var result =
            await service.SyncAsync(
                CancellationToken.None);

        Assert.True(
            result);

        sessionService
            .Received(1)
            .InvalidateToken();
    }

    [Fact]
    public async Task SyncAsync_ShouldSendPendingCountAndDeviceStatus()
    {
        var dependencies =
            CreateDependencies();

        dependencies.ReleaseControl
            .GetConfigurationAsync(
                Arg.Any<CancellationToken>())
            .Returns(
                new AccessReleaseConfiguration(
                    false,
                    1));

        dependencies.OfflineStore
            .CountPendingEventsAsync(
                Arg.Any<CancellationToken>())
            .Returns(
                4);

        dependencies.Adapter
            .GetRuntimeStatus()
            .Returns(
                new AccessDeviceRuntimeStatus(
                    "front-door",
                    "toletus-litenet2",
                    true,
                    true,
                    "192.168.0.50:7878"));

        dependencies.ApiClient
            .HeartbeatAsync(
                dependencies.Credentials,
                "token",
                1,
                4,
                Arg.Any<
                    IReadOnlyCollection<
                        AccessDeviceRuntimeStatus>>(),
                Arg.Any<DateTime?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(
                new AgentControlPlaneHeartbeatResult(
                    false,
                    1,
                    DateTime.UtcNow));

        var result =
            await dependencies.Service
                .SyncAsync(
                    CancellationToken.None);

        Assert.True(
            result);

        await dependencies.ApiClient
            .Received(1)
            .HeartbeatAsync(
                dependencies.Credentials,
                "token",
                1,
                4,
                Arg.Is<
                    IReadOnlyCollection<
                        AccessDeviceRuntimeStatus>>(
                    devices =>
                        devices.Count == 1 &&
                        devices.Single().DeviceKey ==
                            "front-door" &&
                        devices.Single().ProviderKey ==
                            "toletus-litenet2" &&
                        devices.Single().Enabled &&
                        devices.Single().Connected &&
                        devices.Single().Endpoint ==
                            "192.168.0.50:7878"),
                Arg.Any<DateTime?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>());
    }

    private static Dependencies
        CreateDependencies()
    {
        var credentialStore =
            Substitute.For<IAgentCredentialStore>();

        var apiClient =
            Substitute.For<IAvelriAccessApiClient>();

        var sessionService =
            Substitute.For<IAgentSessionService>();

        var releaseControl =
            Substitute.For<IAccessReleaseControl>();

        var offlineStore =
            Substitute.For<IAccessOfflineStore>();

        var adapter =
            Substitute.For<IAccessDeviceAdapter>();

        var credentials =
            new AgentCredentials(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "secret",
                "https://api.avelri.test");

        credentialStore
            .LoadAsync(
                Arg.Any<CancellationToken>())
            .Returns(
                credentials);

        sessionService
            .GetTokenAsync(
                credentials,
                Arg.Any<CancellationToken>())
            .Returns(
                "token");

        offlineStore
            .CountPendingEventsAsync(
                Arg.Any<CancellationToken>())
            .Returns(
                0);

        adapter
            .GetRuntimeStatus()
            .Returns(
                new AccessDeviceRuntimeStatus(
                    "primary",
                    "toletus-litenet2",
                    true,
                    false,
                    "192.168.0.50:7878"));

        var service =
            CreateService(
                credentialStore,
                apiClient,
                sessionService,
                releaseControl,
                offlineStore,
                [adapter]);

        return new Dependencies(
            credentials,
            apiClient,
            releaseControl,
            offlineStore,
            adapter,
            service);
    }

    private static AccessControlPlaneSyncService
        CreateService(
            IAgentCredentialStore credentialStore,
            IAvelriAccessApiClient apiClient,
            IAgentSessionService sessionService,
            IAccessReleaseControl releaseControl,
            IAccessOfflineStore offlineStore,
            IEnumerable<IAccessDeviceAdapter> adapters,
            IAccessOperationalHealthStore? operationalHealthStore = null)
    {
        operationalHealthStore ??=
            Substitute.For<
                IAccessOperationalHealthStore>();

        operationalHealthStore
            .GetSnapshot()
            .Returns(
                new AccessOperationalHealthSnapshot(
                    null,
                    null,
                    null));

        return new AccessControlPlaneSyncService(
            credentialStore,
            apiClient,
            sessionService,
            releaseControl,
            offlineStore,
            adapters,
            operationalHealthStore);
    }

    private sealed record Dependencies(
        AgentCredentials Credentials,
        IAvelriAccessApiClient ApiClient,
        IAccessReleaseControl ReleaseControl,
        IAccessOfflineStore OfflineStore,
        IAccessDeviceAdapter Adapter,
        AccessControlPlaneSyncService Service);
}
