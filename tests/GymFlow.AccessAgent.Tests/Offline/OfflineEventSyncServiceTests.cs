using GymFlow.AccessAgent.Api;
using GymFlow.AccessAgent.Offline;
using GymFlow.AccessAgent.Security;
using GymFlow.AccessAgent.Services;
using Microsoft.Extensions.Options;
using NSubstitute;
using GymFlow.AccessAgent.Abstractions;

namespace GymFlow.AccessAgent.Tests.Offline;

public class OfflineEventSyncServiceTests
{
    [Fact]
    public async Task SyncPendingAsync_WhenAgentIsNotPaired_ShouldReturnZero()
    {
        var credentialStore =
            Substitute.For<IAgentCredentialStore>();

        var apiClient =
            Substitute.For<IAvelriAccessApiClient>();

        var sessionService =
            Substitute.For<IAgentSessionService>();

        var offlineStore =
            Substitute.For<IAccessOfflineStore>();

        credentialStore
            .LoadAsync(
                Arg.Any<CancellationToken>())
            .Returns((AgentCredentials?)null);

        var options =
            Options.Create(
                new AccessOfflineStoreOptions
                {
                    PermissionCacheMinutes = 30,
                    SyncBatchSize = 100
                });

        var service =
            new OfflineEventSyncService(
                credentialStore,
                apiClient,
                sessionService,
                offlineStore,
                options);

        var result =
            await service.SyncPendingAsync(
                CancellationToken.None);

        Assert.Equal(0, result);

        await sessionService
            .DidNotReceiveWithAnyArgs()
            .GetTokenAsync(
                default!,
                default);
    }

    [Fact]
    public async Task SyncPendingAsync_WhenPendingEventSucceeds_ShouldMarkAsSynced()
    {
        var credentialStore =
            Substitute.For<IAgentCredentialStore>();

        var apiClient =
            Substitute.For<IAvelriAccessApiClient>();

        var sessionService =
            Substitute.For<IAgentSessionService>();

        var offlineStore =
            Substitute.For<IAccessOfflineStore>();

        var credentials =
            new AgentCredentials(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "secret",
                "http://localhost:5137");

        var pendingEvent =
            new PendingAccessEvent(
                Guid.NewGuid(),
                "toletus-litenet2",
                AccessCredentialType.Card,
                "12345",
                DateTime.UtcNow.AddMinutes(-1),
                true,
                "Eligible",
                AccessDecisionSource.OfflineCache,
                DateTime.UtcNow.AddMinutes(-1),
                null);

        credentialStore
            .LoadAsync(
                Arg.Any<CancellationToken>())
            .Returns(credentials);

        sessionService
            .GetTokenAsync(
                credentials,
                Arg.Any<CancellationToken>())
            .Returns("agent-token");

        offlineStore
            .GetPendingEventsAsync(
                100,
                Arg.Any<CancellationToken>())
            .Returns(new[]
            {
            pendingEvent
            });

        var options =
            Options.Create(
                new AccessOfflineStoreOptions
                {
                    PermissionCacheMinutes = 30,
                    SyncBatchSize = 100
                });

        var service =
            new OfflineEventSyncService(
                credentialStore,
                apiClient,
                sessionService,
                offlineStore,
                options);

        var result =
            await service.SyncPendingAsync(
                CancellationToken.None);

        Assert.Equal(1, result);

        await apiClient
            .Received(1)
            .SyncOfflineEventAsync(
                credentials,
                "agent-token",
                pendingEvent,
                Arg.Any<CancellationToken>());

        await offlineStore
            .Received(1)
            .MarkEventSyncedAsync(
                pendingEvent.RequestId,
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncPendingAsync_WhenApiFails_ShouldKeepEventPending()
    {
        var credentialStore =
            Substitute.For<IAgentCredentialStore>();

        var apiClient =
            Substitute.For<IAvelriAccessApiClient>();

        var sessionService =
            Substitute.For<IAgentSessionService>();

        var offlineStore =
            Substitute.For<IAccessOfflineStore>();

        var credentials =
            new AgentCredentials(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "secret",
                "http://localhost:5137");

        var pendingEvent =
            new PendingAccessEvent(
                Guid.NewGuid(),
                "toletus-litenet2",
                AccessCredentialType.Card,
                "12345",
                DateTime.UtcNow.AddMinutes(-1),
                true,
                "Eligible",
                AccessDecisionSource.OfflineCache,
                DateTime.UtcNow.AddMinutes(-1),
                null);

        credentialStore
            .LoadAsync(
                Arg.Any<CancellationToken>())
            .Returns(credentials);

        sessionService
            .GetTokenAsync(
                credentials,
                Arg.Any<CancellationToken>())
            .Returns("agent-token");

        offlineStore
            .GetPendingEventsAsync(
                100,
                Arg.Any<CancellationToken>())
            .Returns(new[]
            {
            pendingEvent
            });

        apiClient
            .SyncOfflineEventAsync(
                credentials,
                "agent-token",
                pendingEvent,
                Arg.Any<CancellationToken>())
            .Returns<Task>(
                _ => throw new HttpRequestException(
                    "API indisponível"));

        var options =
            Options.Create(
                new AccessOfflineStoreOptions
                {
                    PermissionCacheMinutes = 30,
                    SyncBatchSize = 100
                });

        var service =
            new OfflineEventSyncService(
                credentialStore,
                apiClient,
                sessionService,
                offlineStore,
                options);

        var result =
            await service.SyncPendingAsync(
                CancellationToken.None);

        Assert.Equal(0, result);

        await offlineStore
            .DidNotReceiveWithAnyArgs()
            .MarkEventSyncedAsync(
                default,
                default,
                default);
    }

    [Fact]
    public async Task SyncPendingAsync_WhenFirstRequestIsUnauthorized_ShouldRefreshTokenAndRetry()
    {
        var credentialStore =
            Substitute.For<IAgentCredentialStore>();

        var apiClient =
            Substitute.For<IAvelriAccessApiClient>();

        var sessionService =
            Substitute.For<IAgentSessionService>();

        var offlineStore =
            Substitute.For<IAccessOfflineStore>();

        var credentials =
            new AgentCredentials(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "secret",
                "http://localhost:5137");

        var pendingEvent =
            new PendingAccessEvent(
                Guid.NewGuid(),
                "toletus-litenet2",
                AccessCredentialType.Card,
                "12345",
                DateTime.UtcNow.AddMinutes(-1),
                true,
                "Eligible",
                AccessDecisionSource.OfflineCache,
                DateTime.UtcNow.AddMinutes(-1),
                null);

        credentialStore
            .LoadAsync(
                Arg.Any<CancellationToken>())
            .Returns(credentials);

        sessionService
            .GetTokenAsync(
                credentials,
                Arg.Any<CancellationToken>())
            .Returns(
                "expired-token",
                "new-token");

        offlineStore
            .GetPendingEventsAsync(
                100,
                Arg.Any<CancellationToken>())
            .Returns(new[]
            {
            pendingEvent
            });

        apiClient
            .SyncOfflineEventAsync(
                credentials,
                "expired-token",
                pendingEvent,
                Arg.Any<CancellationToken>())
            .Returns<Task>(
                _ => throw new HttpRequestException(
                    "Unauthorized",
                    null,
                    System.Net.HttpStatusCode.Unauthorized));

        var options =
            Options.Create(
                new AccessOfflineStoreOptions
                {
                    PermissionCacheMinutes = 30,
                    SyncBatchSize = 100
                });

        var service =
            new OfflineEventSyncService(
                credentialStore,
                apiClient,
                sessionService,
                offlineStore,
                options);

        var result =
            await service.SyncPendingAsync(
                CancellationToken.None);

        Assert.Equal(1, result);

        sessionService
            .Received(1)
            .InvalidateToken();

        await apiClient
            .Received(1)
            .SyncOfflineEventAsync(
                credentials,
                "new-token",
                pendingEvent,
                Arg.Any<CancellationToken>());

        await offlineStore
            .Received(1)
            .MarkEventSyncedAsync(
                pendingEvent.RequestId,
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>());
    }
}