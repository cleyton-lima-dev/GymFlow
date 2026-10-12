using System.Net;
using GymFlow.AccessAgent.Api;
using GymFlow.AccessAgent.Offline;
using GymFlow.AccessAgent.Security;
using GymFlow.AccessAgent.Services;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GymFlow.AccessAgent.Tests.Offline;

public class PhysicalPassageSyncServiceTests
{
    [Fact]
    public async Task SyncPendingAsync_WhenPassageSucceeds_ShouldMarkSynced()
    {
        var dependencies =
            CreateDependencies();

        var passage =
            CreatePassage();

        dependencies.PassageStore
            .GetPendingAsync(
                100,
                Arg.Any<CancellationToken>())
            .Returns([passage]);

        dependencies.SessionService
            .GetTokenAsync(
                dependencies.Credentials,
                Arg.Any<CancellationToken>())
            .Returns("agent-token");

        var result =
            await dependencies.Service
                .SyncPendingAsync(
                    CancellationToken.None);

        Assert.Equal(
            1,
            result);

        await dependencies.ApiClient
            .Received(1)
            .ConfirmPhysicalPassageAsync(
                dependencies.Credentials,
                "agent-token",
                passage.RequestId,
                passage.OccurredAt,
                Arg.Any<CancellationToken>());

        await dependencies.PassageStore
            .Received(1)
            .MarkSyncedAsync(
                passage.RequestId,
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncPendingAsync_WhenAccessEventIsNotAvailable_ShouldKeepPending()
    {
        var dependencies =
            CreateDependencies();

        var passage =
            CreatePassage();

        dependencies.PassageStore
            .GetPendingAsync(
                100,
                Arg.Any<CancellationToken>())
            .Returns([passage]);

        dependencies.SessionService
            .GetTokenAsync(
                dependencies.Credentials,
                Arg.Any<CancellationToken>())
            .Returns("agent-token");

        dependencies.ApiClient
            .ConfirmPhysicalPassageAsync(
                dependencies.Credentials,
                "agent-token",
                passage.RequestId,
                passage.OccurredAt,
                Arg.Any<CancellationToken>())
            .Returns<Task>(
                _ => throw new HttpRequestException(
                    "Not found",
                    null,
                    HttpStatusCode.NotFound));

        var result =
            await dependencies.Service
                .SyncPendingAsync(
                    CancellationToken.None);

        Assert.Equal(
            0,
            result);

        await dependencies.PassageStore
            .DidNotReceive()
            .MarkSyncedAsync(
                Arg.Any<Guid>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>());

        dependencies.OperationalHealthStore
            .Received(1)
            .RecordFailure(
                "PhysicalPassageSync.AccessEventNotAvailable",
                Arg.Any<DateTime>());
    }

    [Fact]
    public async Task SyncPendingAsync_WhenFirstTokenIsUnauthorized_ShouldRefreshAndRetry()
    {
        var dependencies =
            CreateDependencies();

        var passage =
            CreatePassage();

        dependencies.PassageStore
            .GetPendingAsync(
                100,
                Arg.Any<CancellationToken>())
            .Returns([passage]);

        dependencies.SessionService
            .GetTokenAsync(
                dependencies.Credentials,
                Arg.Any<CancellationToken>())
            .Returns(
                "expired-token",
                "new-token");

        dependencies.ApiClient
            .ConfirmPhysicalPassageAsync(
                dependencies.Credentials,
                "expired-token",
                passage.RequestId,
                passage.OccurredAt,
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
            1,
            result);

        dependencies.SessionService
            .Received(1)
            .InvalidateToken();

        await dependencies.ApiClient
            .Received(1)
            .ConfirmPhysicalPassageAsync(
                dependencies.Credentials,
                "new-token",
                passage.RequestId,
                passage.OccurredAt,
                Arg.Any<CancellationToken>());

        await dependencies.PassageStore
            .Received(1)
            .MarkSyncedAsync(
                passage.RequestId,
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncPendingAsync_WhenApiIsUnavailable_ShouldKeepPending()
    {
        var dependencies =
            CreateDependencies();

        var passage =
            CreatePassage();

        dependencies.PassageStore
            .GetPendingAsync(
                100,
                Arg.Any<CancellationToken>())
            .Returns([passage]);

        dependencies.SessionService
            .GetTokenAsync(
                dependencies.Credentials,
                Arg.Any<CancellationToken>())
            .Returns("agent-token");

        dependencies.ApiClient
            .ConfirmPhysicalPassageAsync(
                dependencies.Credentials,
                "agent-token",
                passage.RequestId,
                passage.OccurredAt,
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

        await dependencies.PassageStore
            .DidNotReceive()
            .MarkSyncedAsync(
                Arg.Any<Guid>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>());

        dependencies.OperationalHealthStore
            .Received(1)
            .RecordFailure(
                "PhysicalPassageSync.ApiUnavailable",
                Arg.Any<DateTime>());
    }

    private static PendingPhysicalPassage
        CreatePassage()
    {
        var now =
            DateTime.UtcNow;

        return new PendingPhysicalPassage(
            Guid.NewGuid(),
            now.AddSeconds(-2),
            now,
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

        var passageStore =
            Substitute.For<IPhysicalPassageOfflineStore>();

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
            .Returns(credentials);

        var options =
            Options.Create(
                new AccessOfflineStoreOptions
                {
                    SyncBatchSize = 100
                });

        var service =
            new PhysicalPassageSyncService(
                credentialStore,
                apiClient,
                sessionService,
                passageStore,
                operationalHealthStore,
                options);

        return new Dependencies(
            credentials,
            credentialStore,
            apiClient,
            sessionService,
            passageStore,
            operationalHealthStore,
            service);
    }

    private sealed record Dependencies(
        AgentCredentials Credentials,
        IAgentCredentialStore CredentialStore,
        IAvelriAccessApiClient ApiClient,
        IAgentSessionService SessionService,
        IPhysicalPassageOfflineStore PassageStore,
        IAccessOperationalHealthStore OperationalHealthStore,
        PhysicalPassageSyncService Service);
}