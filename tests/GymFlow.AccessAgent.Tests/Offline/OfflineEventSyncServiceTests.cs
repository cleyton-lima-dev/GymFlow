using System.Net;
using GymFlow.AccessAgent.Abstractions;
using GymFlow.AccessAgent.Api;
using GymFlow.AccessAgent.Offline;
using GymFlow.AccessAgent.Security;
using GymFlow.AccessAgent.Services;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GymFlow.AccessAgent.Tests.Offline;

public class OfflineEventSyncServiceTests
{
    [Fact]
    public async Task SyncPendingAsync_WhenAgentIsNotPaired_ShouldReturnZero()
    {
        var dependencies =
            CreateDependencies();

        dependencies.CredentialStore
            .LoadAsync(
                Arg.Any<CancellationToken>())
            .Returns(
                (AgentCredentials?)null);

        var result =
            await dependencies.Service
                .SyncPendingAsync(
                    CancellationToken.None);

        Assert.Equal(
            0,
            result);

        await dependencies.SessionService
            .DidNotReceive()
            .GetTokenAsync(
                Arg.Any<AgentCredentials>(),
                Arg.Any<CancellationToken>());

        dependencies.OperationalHealthStore
            .DidNotReceive()
            .RecordFailure(
                Arg.Any<string>(),
                Arg.Any<DateTime>());
    }

    [Fact]
    public async Task SyncPendingAsync_WhenPendingEventSucceeds_ShouldMarkAndRecordSync()
    {
        var dependencies =
            CreateDependencies();

        var pendingEvent =
            CreatePendingEvent();

        ConfigurePendingEvent(
            dependencies,
            pendingEvent);

        dependencies.SessionService
            .GetTokenAsync(
                dependencies.Credentials,
                Arg.Any<CancellationToken>())
            .Returns(
                "agent-token");

        dependencies.ApiClient
            .SyncOfflineEventAsync(
                dependencies.Credentials,
                "agent-token",
                pendingEvent,
                Arg.Any<CancellationToken>())
            .Returns(
                Task.CompletedTask);

        var result =
            await dependencies.Service
                .SyncPendingAsync(
                    CancellationToken.None);

        Assert.Equal(
            1,
            result);

        await dependencies.OfflineStore
            .Received(1)
            .MarkEventSyncedAsync(
                pendingEvent.RequestId,
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>());

        dependencies.OperationalHealthStore
            .Received(1)
            .RecordOfflineSync(
                Arg.Any<DateTime>());

        dependencies.OperationalHealthStore
            .DidNotReceive()
            .RecordFailure(
                Arg.Any<string>(),
                Arg.Any<DateTime>());
    }

    [Fact]
    public async Task SyncPendingAsync_WhenApiFails_ShouldKeepPendingAndRecordFailure()
    {
        var dependencies =
            CreateDependencies();

        var pendingEvent =
            CreatePendingEvent();

        ConfigurePendingEvent(
            dependencies,
            pendingEvent);

        dependencies.SessionService
            .GetTokenAsync(
                dependencies.Credentials,
                Arg.Any<CancellationToken>())
            .Returns(
                "agent-token");

        dependencies.ApiClient
            .SyncOfflineEventAsync(
                dependencies.Credentials,
                "agent-token",
                pendingEvent,
                Arg.Any<CancellationToken>())
            .Returns<Task>(
                _ => throw new HttpRequestException(
                    "API indisponível"));

        var result =
            await dependencies.Service
                .SyncPendingAsync(
                    CancellationToken.None);

        Assert.Equal(
            0,
            result);

        await dependencies.OfflineStore
            .DidNotReceive()
            .MarkEventSyncedAsync(
                Arg.Any<Guid>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>());

        dependencies.OperationalHealthStore
            .Received(1)
            .RecordFailure(
                "OfflineSync.ApiUnavailable",
                Arg.Any<DateTime>());
    }

    [Fact]
    public async Task SyncPendingAsync_WhenFirstRequestIsUnauthorized_ShouldRefreshAndRetry()
    {
        var dependencies =
            CreateDependencies();

        var pendingEvent =
            CreatePendingEvent();

        ConfigurePendingEvent(
            dependencies,
            pendingEvent);

        dependencies.SessionService
            .GetTokenAsync(
                dependencies.Credentials,
                Arg.Any<CancellationToken>())
            .Returns(
                "expired-token",
                "new-token");

        dependencies.ApiClient
            .SyncOfflineEventAsync(
                dependencies.Credentials,
                "expired-token",
                pendingEvent,
                Arg.Any<CancellationToken>())
            .Returns<Task>(
                _ => throw new HttpRequestException(
                    "Unauthorized",
                    null,
                    HttpStatusCode.Unauthorized));

        dependencies.ApiClient
            .SyncOfflineEventAsync(
                dependencies.Credentials,
                "new-token",
                pendingEvent,
                Arg.Any<CancellationToken>())
            .Returns(
                Task.CompletedTask);

        var result =
            await dependencies.Service
                .SyncPendingAsync(
                    CancellationToken.None);

        Assert.Equal(
            1,
            result);

        dependencies.SessionService
            .Received(1)
            .InvalidateToken();

        dependencies.OperationalHealthStore
            .Received(1)
            .RecordOfflineSync(
                Arg.Any<DateTime>());

        dependencies.OperationalHealthStore
            .DidNotReceive()
            .RecordFailure(
                Arg.Any<string>(),
                Arg.Any<DateTime>());
    }

    [Fact]
    public async Task SyncPendingAsync_WhenRetryIsUnauthorized_ShouldRecordFailure()
    {
        var dependencies =
            CreateDependencies();

        var pendingEvent =
            CreatePendingEvent();

        ConfigurePendingEvent(
            dependencies,
            pendingEvent);

        dependencies.SessionService
            .GetTokenAsync(
                dependencies.Credentials,
                Arg.Any<CancellationToken>())
            .Returns(
                "expired-token",
                "new-token");

        dependencies.ApiClient
            .SyncOfflineEventAsync(
                dependencies.Credentials,
                "expired-token",
                pendingEvent,
                Arg.Any<CancellationToken>())
            .Returns<Task>(
                _ => throw new HttpRequestException(
                    "Unauthorized",
                    null,
                    HttpStatusCode.Unauthorized));

        dependencies.ApiClient
            .SyncOfflineEventAsync(
                dependencies.Credentials,
                "new-token",
                pendingEvent,
                Arg.Any<CancellationToken>())
            .Returns<Task>(
                _ => throw new HttpRequestException(
                    "Unauthorized",
                    null,
                    HttpStatusCode.Unauthorized));

        var result =
            await dependencies.Service
                .SyncPendingAsync(
                    CancellationToken.None);

        Assert.Equal(
            0,
            result);

        dependencies.OperationalHealthStore
            .Received(1)
            .RecordFailure(
                "OfflineSync.Unauthorized",
                Arg.Any<DateTime>());

        await dependencies.OfflineStore
            .DidNotReceive()
            .MarkEventSyncedAsync(
                Arg.Any<Guid>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncPendingAsync_WhenPendingExistsAndTokenIsUnavailable_ShouldRecordFailure()
    {
        var dependencies =
            CreateDependencies();

        var pendingEvent =
            CreatePendingEvent();

        ConfigurePendingEvent(
            dependencies,
            pendingEvent);

        dependencies.SessionService
            .GetTokenAsync(
                dependencies.Credentials,
                Arg.Any<CancellationToken>())
            .Returns(
                (string?)null);

        var result =
            await dependencies.Service
                .SyncPendingAsync(
                    CancellationToken.None);

        Assert.Equal(
            0,
            result);

        dependencies.OperationalHealthStore
            .Received(1)
            .RecordFailure(
                "OfflineSync.AuthenticationUnavailable",
                Arg.Any<DateTime>());
    }

    private static void ConfigurePendingEvent(
        Dependencies dependencies,
        PendingAccessEvent pendingEvent)
    {
        dependencies.OfflineStore
            .GetPendingEventsAsync(
                100,
                Arg.Any<CancellationToken>())
            .Returns(
                [pendingEvent]);
    }

    private static PendingAccessEvent
        CreatePendingEvent()
    {
        var now =
            DateTime.UtcNow;

        return new PendingAccessEvent(
            Guid.NewGuid(),
            "toletus-litenet2",
            AccessCredentialType.Card,
            "12345",
            now.AddMinutes(-1),
            true,
            "Eligible",
            AccessDecisionSource.OfflineCache,
            now.AddMinutes(-1),
            null);
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

        var offlineStore =
            Substitute.For<IAccessOfflineStore>();

        var operationalHealthStore =
            Substitute.For<IAccessOperationalHealthStore>();

        var credentials =
            new AgentCredentials(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "secret",
                "http://localhost:5137");

        credentialStore
            .LoadAsync(
                Arg.Any<CancellationToken>())
            .Returns(
                credentials);

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
                operationalHealthStore,
                options);

        return new Dependencies(
            credentials,
            credentialStore,
            apiClient,
            sessionService,
            offlineStore,
            operationalHealthStore,
            service);
    }

    private sealed record Dependencies(
        AgentCredentials Credentials,
        IAgentCredentialStore CredentialStore,
        IAvelriAccessApiClient ApiClient,
        IAgentSessionService SessionService,
        IAccessOfflineStore OfflineStore,
        IAccessOperationalHealthStore OperationalHealthStore,
        OfflineEventSyncService Service);
}
