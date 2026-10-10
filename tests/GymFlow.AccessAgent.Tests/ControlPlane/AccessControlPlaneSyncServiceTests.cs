using System.Net;
using GymFlow.AccessAgent.Api;
using GymFlow.AccessAgent.ControlPlane;
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

        credentialStore
            .LoadAsync(
                Arg.Any<CancellationToken>())
            .Returns((AgentCredentials?)null);

        var service =
            CreateService(
                credentialStore,
                apiClient,
                sessionService,
                releaseControl);

        var result =
            await service.SyncAsync(
                CancellationToken.None);

        Assert.False(result);

        await apiClient
            .DidNotReceiveWithAnyArgs()
            .HeartbeatAsync(
                default!,
                default!,
                default,
                default);
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

        Assert.True(result);

        await dependencies.ReleaseControl
            .DidNotReceiveWithAnyArgs()
            .ApplyConfigurationAsync(
                default,
                default,
                default);

        await dependencies.ApiClient
            .Received(1)
            .HeartbeatAsync(
                dependencies.Credentials,
                "token",
                1,
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

        Assert.True(result);

        await dependencies.ReleaseControl
            .Received(1)
            .ApplyConfigurationAsync(
                true,
                2,
                Arg.Any<CancellationToken>());

        await dependencies.ApiClient
            .Received(1)
            .HeartbeatAsync(
                dependencies.Credentials,
                "token",
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

        Assert.True(result);

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

        var credentials =
            new AgentCredentials(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "secret",
                "https://api.avelri.test");

        credentialStore
            .LoadAsync(
                Arg.Any<CancellationToken>())
            .Returns(credentials);

        releaseControl
            .GetConfigurationAsync(
                Arg.Any<CancellationToken>())
            .Returns(
                new AccessReleaseConfiguration(
                    false,
                    1));

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
                Arg.Any<CancellationToken>())
            .Returns<Task<AgentControlPlaneHeartbeatResult>>(
                _ => throw new HttpRequestException(
                    "Unauthorized",
                    null,
                    HttpStatusCode.Unauthorized));

        apiClient
            .HeartbeatAsync(
                credentials,
                "fresh-token",
                1,
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
                releaseControl);

        var result =
            await service.SyncAsync(
                CancellationToken.None);

        Assert.True(result);

        sessionService
            .Received(1)
            .InvalidateToken();

        await apiClient
            .Received(1)
            .HeartbeatAsync(
                credentials,
                "fresh-token",
                1,
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

        var credentials =
            new AgentCredentials(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "secret",
                "https://api.avelri.test");

        credentialStore
            .LoadAsync(
                Arg.Any<CancellationToken>())
            .Returns(credentials);

        sessionService
            .GetTokenAsync(
                credentials,
                Arg.Any<CancellationToken>())
            .Returns("token");

        var service =
            CreateService(
                credentialStore,
                apiClient,
                sessionService,
                releaseControl);

        return new Dependencies(
            credentials,
            apiClient,
            releaseControl,
            service);
    }

    private static AccessControlPlaneSyncService
        CreateService(
            IAgentCredentialStore credentialStore,
            IAvelriAccessApiClient apiClient,
            IAgentSessionService sessionService,
            IAccessReleaseControl releaseControl)
    {
        return new AccessControlPlaneSyncService(
            credentialStore,
            apiClient,
            sessionService,
            releaseControl);
    }

    private sealed record Dependencies(
        AgentCredentials Credentials,
        IAvelriAccessApiClient ApiClient,
        IAccessReleaseControl ReleaseControl,
        AccessControlPlaneSyncService Service);
}
