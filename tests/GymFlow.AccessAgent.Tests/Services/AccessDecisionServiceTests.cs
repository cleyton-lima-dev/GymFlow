using GymFlow.AccessAgent.Abstractions;
using GymFlow.AccessAgent.Api;
using GymFlow.AccessAgent.Security;
using GymFlow.AccessAgent.Services;
using NSubstitute;
using System.Net;
using GymFlow.AccessAgent.Offline;
using Microsoft.Extensions.Options;

namespace GymFlow.AccessAgent.Tests.Services;

public class AccessDecisionServiceTests
{
    [Fact]
    public async Task DecideAsync_WhenAgentIsNotPaired_ReturnsDenied()
    {
        var credentialStore =
            Substitute.For<IAgentCredentialStore>();

        var apiClient =
            Substitute.For<IAvelriAccessApiClient>();

        credentialStore
            .LoadAsync(Arg.Any<CancellationToken>())
            .Returns((AgentCredentials?)null);

        var service =
            CreateService(
    credentialStore,
    apiClient);

        var attempt =
            new DeviceAccessAttempt(
                Guid.NewGuid(),
                "12345",
                AccessCredentialType.Card,
                DateTime.UtcNow);

        var result =
            await service.DecideAsync(
                "toletus-litenet2",
                attempt,
                CancellationToken.None);

        Assert.False(result.Allowed);
        Assert.Equal(
            "AgentNotPaired",
            result.Reason);

        await apiClient
            .DidNotReceive()
            .LoginAsync(
                Arg.Any<AgentCredentials>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DecideAsync_WhenAuthenticationFails_ReturnsDenied()
    {
        var credentialStore =
            Substitute.For<IAgentCredentialStore>();

        var apiClient =
            Substitute.For<IAvelriAccessApiClient>();

        var credentials =
            new AgentCredentials(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "secret",
                "https://api.avelri.test");

        credentialStore
            .LoadAsync(Arg.Any<CancellationToken>())
            .Returns(credentials);

        apiClient
            .LoginAsync(
                credentials,
                Arg.Any<CancellationToken>())
            .Returns((AgentLoginResult?)null);

        var service =
            CreateService(
    credentialStore,
    apiClient);

        var attempt =
            new DeviceAccessAttempt(
                Guid.NewGuid(),
                "12345",
                AccessCredentialType.Card,
                DateTime.UtcNow);

        var result =
            await service.DecideAsync(
                "toletus-litenet2",
                attempt,
                CancellationToken.None);

        Assert.False(result.Allowed);
        Assert.Equal(
            "AgentAuthenticationFailed",
            result.Reason);

        await apiClient
            .DidNotReceive()
            .DecideAsync(
                Arg.Any<AgentCredentials>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<DeviceAccessAttempt>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DecideAsync_WhenApiAllowsAccess_ReturnsAllowed()
    {
        var credentialStore =
            Substitute.For<IAgentCredentialStore>();

        var apiClient =
            Substitute.For<IAvelriAccessApiClient>();

        var credentials =
            new AgentCredentials(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "secret",
                "https://api.avelri.test");

        credentialStore
            .LoadAsync(Arg.Any<CancellationToken>())
            .Returns(credentials);

        apiClient
            .LoginAsync(
                credentials,
                Arg.Any<CancellationToken>())
            .Returns(
                new AgentLoginResult(
                    credentials.AgentId,
                    credentials.GymId,
                    "Agent Test",
                    "jwt-token"));

        var attempt =
            new DeviceAccessAttempt(
                Guid.NewGuid(),
                "12345",
                AccessCredentialType.Card,
                DateTime.UtcNow);

        apiClient
            .DecideAsync(
                credentials,
                "jwt-token",
                "toletus-litenet2",
                attempt,
                Arg.Any<CancellationToken>())
            .Returns(
                new AgentAccessDecisionResult(
                    attempt.RequestId,
                    true,
                    "Eligible",
                    DateTime.UtcNow));

        var service =
            CreateService(
    credentialStore,
    apiClient);

        var result =
            await service.DecideAsync(
                "toletus-litenet2",
                attempt,
                CancellationToken.None);

        Assert.True(result.Allowed);
        Assert.Equal(
            "Eligible",
            result.Reason);
    }

    [Fact]
    public async Task DecideAsync_WhenCalledMultipleTimes_ReusesAuthenticationToken()
    {
        var credentialStore =
            Substitute.For<IAgentCredentialStore>();

        var apiClient =
            Substitute.For<IAvelriAccessApiClient>();

        var credentials =
            new AgentCredentials(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "secret",
                "https://api.avelri.test");

        credentialStore
            .LoadAsync(Arg.Any<CancellationToken>())
            .Returns(credentials);

        apiClient
            .LoginAsync(
                credentials,
                Arg.Any<CancellationToken>())
            .Returns(
                new AgentLoginResult(
                    credentials.AgentId,
                    credentials.GymId,
                    "Agent Test",
                    "jwt-token"));

        apiClient
            .DecideAsync(
                credentials,
                "jwt-token",
                "toletus-litenet2",
                Arg.Any<DeviceAccessAttempt>(),
                Arg.Any<CancellationToken>())
            .Returns(
                callInfo =>
                {
                    var attempt =
                        callInfo.ArgAt<DeviceAccessAttempt>(3);

                    return new AgentAccessDecisionResult(
                        attempt.RequestId,
                        true,
                        "Eligible",
                        DateTime.UtcNow);
                });

        var service =
            CreateService(
    credentialStore,
    apiClient);

        var firstAttempt =
            new DeviceAccessAttempt(
                Guid.NewGuid(),
                "11111",
                AccessCredentialType.Card,
                DateTime.UtcNow);

        var secondAttempt =
            new DeviceAccessAttempt(
                Guid.NewGuid(),
                "22222",
                AccessCredentialType.Card,
                DateTime.UtcNow);

        await service.DecideAsync(
            "toletus-litenet2",
            firstAttempt,
            CancellationToken.None);

        await service.DecideAsync(
            "toletus-litenet2",
            secondAttempt,
            CancellationToken.None);

        await apiClient
            .Received(1)
            .LoginAsync(
                credentials,
                Arg.Any<CancellationToken>());

        await apiClient
            .Received(2)
            .DecideAsync(
                credentials,
                "jwt-token",
                "toletus-litenet2",
                Arg.Any<DeviceAccessAttempt>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DecideAsync_WhenTokenExpires_ReauthenticatesAndRetriesOnce()
    {
        var credentialStore =
            Substitute.For<IAgentCredentialStore>();

        var apiClient =
            Substitute.For<IAvelriAccessApiClient>();

        var credentials =
            new AgentCredentials(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "secret",
                "https://api.avelri.test");

        credentialStore
            .LoadAsync(Arg.Any<CancellationToken>())
            .Returns(credentials);

        apiClient
            .LoginAsync(
                credentials,
                Arg.Any<CancellationToken>())
            .Returns(
                new AgentLoginResult(
                    credentials.AgentId,
                    credentials.GymId,
                    "Agent Test",
                    "token-1"),
                new AgentLoginResult(
                    credentials.AgentId,
                    credentials.GymId,
                    "Agent Test",
                    "token-2"));

        var attempt =
            new DeviceAccessAttempt(
                Guid.NewGuid(),
                "12345",
                AccessCredentialType.Card,
                DateTime.UtcNow);

        apiClient
            .DecideAsync(
                credentials,
                "token-1",
                "toletus-litenet2",
                attempt,
                Arg.Any<CancellationToken>())
            .Returns<Task<AgentAccessDecisionResult>>(
                _ => throw new HttpRequestException(
                    "Unauthorized",
                    null,
                    HttpStatusCode.Unauthorized));

        apiClient
            .DecideAsync(
                credentials,
                "token-2",
                "toletus-litenet2",
                attempt,
                Arg.Any<CancellationToken>())
            .Returns(
                new AgentAccessDecisionResult(
                    attempt.RequestId,
                    true,
                    "Eligible",
                    DateTime.UtcNow));

        var service =
            CreateService(
    credentialStore,
    apiClient);

        var result =
            await service.DecideAsync(
                "toletus-litenet2",
                attempt,
                CancellationToken.None);

        Assert.True(result.Allowed);
        Assert.Equal(
            "Eligible",
            result.Reason);

        await apiClient
            .Received(2)
            .LoginAsync(
                credentials,
                Arg.Any<CancellationToken>());

        await apiClient
            .Received(1)
            .DecideAsync(
                credentials,
                "token-1",
                "toletus-litenet2",
                attempt,
                Arg.Any<CancellationToken>());

        await apiClient
            .Received(1)
            .DecideAsync(
                credentials,
                "token-2",
                "toletus-litenet2",
                attempt,
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DecideAsync_WhenNetworkFailsAndNoCache_ReturnsDeniedAndQueuesEvent()
    {
        var credentialStore =
            Substitute.For<IAgentCredentialStore>();

        var apiClient =
            Substitute.For<IAvelriAccessApiClient>();

        var offlineStore =
            Substitute.For<IAccessOfflineStore>();

        var credentials =
            new AgentCredentials(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "secret",
                "https://api.avelri.test");

        credentialStore
            .LoadAsync(Arg.Any<CancellationToken>())
            .Returns(credentials);

        apiClient
            .LoginAsync(
                credentials,
                Arg.Any<CancellationToken>())
            .Returns(
                new AgentLoginResult(
                    credentials.AgentId,
                    credentials.GymId,
                    "Agent Test",
                    "jwt-token"));

        var attempt =
            new DeviceAccessAttempt(
                Guid.NewGuid(),
                "12345",
                AccessCredentialType.Card,
                DateTime.UtcNow);

        apiClient
            .DecideAsync(
                credentials,
                "jwt-token",
                "toletus-litenet2",
                attempt,
                Arg.Any<CancellationToken>())
            .Returns<Task<AgentAccessDecisionResult>>(
                _ => throw new HttpRequestException(
                    "Network unavailable"));

        offlineStore
            .GetPermissionAsync(
                "toletus-litenet2",
                AccessCredentialType.Card,
                "12345",
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns((CachedAccessPermission?)null);

        var options =
            Options.Create(
                new AccessOfflineStoreOptions
                {
                    PermissionCacheMinutes = 30,
                    SyncBatchSize = 100
                });

        var service =
            new AccessDecisionService(
                credentialStore,
                apiClient,
                offlineStore,
options,
new AgentSessionService(apiClient));

        var result =
            await service.DecideAsync(
                "toletus-litenet2",
                attempt,
                CancellationToken.None);

        Assert.False(result.Allowed);

        Assert.Equal(
            "OfflineNoCachedPermission",
            result.Reason);

        await offlineStore
            .Received(1)
            .EnqueueEventAsync(
                Arg.Is<PendingAccessEvent>(
                    accessEvent =>
                        accessEvent.RequestId ==
                            attempt.RequestId &&
                        accessEvent.ProviderKey ==
                            "toletus-litenet2" &&
                        accessEvent.ExternalIdentifier ==
                            "12345" &&
                        !accessEvent.Allowed &&
                        accessEvent.Reason ==
                            "OfflineNoCachedPermission"),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DecideAsync_WhenNetworkFailsAndValidCacheAllows_ReturnsAllowedAndQueuesEvent()
    {
        var credentialStore =
            Substitute.For<IAgentCredentialStore>();

        var apiClient =
            Substitute.For<IAvelriAccessApiClient>();

        var offlineStore =
            Substitute.For<IAccessOfflineStore>();

        var credentials =
            new AgentCredentials(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "secret",
                "https://api.avelri.test");

        credentialStore
            .LoadAsync(Arg.Any<CancellationToken>())
            .Returns(credentials);

        apiClient
            .LoginAsync(
                credentials,
                Arg.Any<CancellationToken>())
            .Returns(
                new AgentLoginResult(
                    credentials.AgentId,
                    credentials.GymId,
                    "Agent Test",
                    "jwt-token"));

        var attempt =
            new DeviceAccessAttempt(
                Guid.NewGuid(),
                "12345",
                AccessCredentialType.Card,
                DateTime.UtcNow);

        apiClient
            .DecideAsync(
                credentials,
                "jwt-token",
                "toletus-litenet2",
                attempt,
                Arg.Any<CancellationToken>())
            .Returns<Task<AgentAccessDecisionResult>>(
                _ => throw new HttpRequestException(
                    "Network unavailable"));

        var now = DateTime.UtcNow;

        offlineStore
            .GetPermissionAsync(
                "toletus-litenet2",
                AccessCredentialType.Card,
                "12345",
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns(
                new CachedAccessPermission(
                    "toletus-litenet2",
                    AccessCredentialType.Card,
                    "12345",
                    true,
                    "Eligible",
                    now.AddMinutes(20),
                    now.AddMinutes(-10)));

        var options =
            Options.Create(
                new AccessOfflineStoreOptions
                {
                    PermissionCacheMinutes = 30,
                    SyncBatchSize = 100
                });

        var service =
            new AccessDecisionService(
                credentialStore,
                apiClient,
                offlineStore,
options,
new AgentSessionService(apiClient));

        var result =
            await service.DecideAsync(
                "toletus-litenet2",
                attempt,
                CancellationToken.None);

        Assert.True(result.Allowed);

        Assert.Equal(
    "Eligible",
    result.Reason);

        await offlineStore
            .Received(1)
            .EnqueueEventAsync(
                Arg.Is<PendingAccessEvent>(
    accessEvent =>
        accessEvent.RequestId == attempt.RequestId &&
        accessEvent.Allowed &&
        accessEvent.Reason == "Eligible" &&
        accessEvent.Source ==
            AccessDecisionSource.OfflineCache),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DecideAsync_WhenOnlineDecisionSucceeds_UpdatesOfflineCache()
    {
        var credentialStore =
            Substitute.For<IAgentCredentialStore>();

        var apiClient =
            Substitute.For<IAvelriAccessApiClient>();

        var offlineStore =
            Substitute.For<IAccessOfflineStore>();

        var credentials =
            new AgentCredentials(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "secret",
                "https://api.avelri.test");

        credentialStore
            .LoadAsync(Arg.Any<CancellationToken>())
            .Returns(credentials);

        apiClient
            .LoginAsync(
                credentials,
                Arg.Any<CancellationToken>())
            .Returns(
                new AgentLoginResult(
                    credentials.AgentId,
                    credentials.GymId,
                    "Agent Test",
                    "jwt-token"));

        var attempt =
            new DeviceAccessAttempt(
                Guid.NewGuid(),
                "12345",
                AccessCredentialType.Card,
                DateTime.UtcNow);

        apiClient
            .DecideAsync(
                credentials,
                "jwt-token",
                "toletus-litenet2",
                attempt,
                Arg.Any<CancellationToken>())
            .Returns(
                new AgentAccessDecisionResult(
                    attempt.RequestId,
                    true,
                    "Eligible",
                    DateTime.UtcNow));

        var options =
            Options.Create(
                new AccessOfflineStoreOptions
                {
                    PermissionCacheMinutes = 30,
                    SyncBatchSize = 100
                });

        var service =
            new AccessDecisionService(
                credentialStore,
                apiClient,
                offlineStore,
options,
new AgentSessionService(apiClient));

        var before =
            DateTime.UtcNow;

        var result =
            await service.DecideAsync(
                "toletus-litenet2",
                attempt,
                CancellationToken.None);

        var after =
            DateTime.UtcNow;

        Assert.True(result.Allowed);

        await offlineStore
            .Received(1)
            .UpsertPermissionAsync(
                Arg.Is<CachedAccessPermission>(
                    permission =>
                        permission.ProviderKey ==
                            "toletus-litenet2" &&
                        permission.CredentialType ==
                            AccessCredentialType.Card &&
                        permission.ExternalIdentifier ==
                            "12345" &&
                        permission.Allowed &&
                        permission.Reason ==
                            "Eligible" &&
                        permission.ValidUntil >=
                            before.AddMinutes(30) &&
                        permission.ValidUntil <=
                            after.AddMinutes(30)),
                Arg.Any<CancellationToken>());
    }



    private static AccessDecisionService CreateService(
    IAgentCredentialStore credentialStore,
    IAvelriAccessApiClient apiClient)
    {
        var offlineStore =
            Substitute.For<IAccessOfflineStore>();

        var options =
            Options.Create(
                new AccessOfflineStoreOptions
                {
                    PermissionCacheMinutes = 30,
                    SyncBatchSize = 100
                });

        return new AccessDecisionService(
            credentialStore,
            apiClient,
            offlineStore,
options,
new AgentSessionService(apiClient));
    }
}