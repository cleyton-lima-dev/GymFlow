using GymFlow.AccessAgent.Api;
using GymFlow.AccessAgent.Security;
using NSubstitute;

namespace GymFlow.AccessAgent.Tests.Security;

public sealed class AgentPairingIsolationTests
{
    private const string ApiBaseUrl = "http://localhost:5137";
    private const string PairingCode = "synthetic-code";
    private const string MachineName = "TEST-MACHINE";

    [Fact]
    public async Task RepairForSameGymPreservesLocalDataAndReplacesCredential()
    {
        using var files = new TemporaryFiles();
        var databasePath = Path.Combine(files.DirectoryPath, "access-agent.db");
        await File.WriteAllTextAsync(databasePath, "existing-local-state");

        var gymId = Guid.NewGuid();
        var previous = Credentials(gymId);
        var store = new RecordingCredentialStore(previous);
        var client = PairingClient(gymId);

        var result = await PairAsync(client, store, files.Probe(databasePath));

        Assert.True(result.Success);
        Assert.Equal(gymId, store.Current?.GymId);
        Assert.Equal("new-secret", store.Current?.Secret);
        Assert.Equal(1, store.SaveCount);
        Assert.Equal("existing-local-state", await File.ReadAllTextAsync(databasePath));
    }

    [Fact]
    public async Task RepairForAnotherGymPreservesPreviousCredentialAndLocalData()
    {
        using var files = new TemporaryFiles();
        var databasePath = Path.Combine(files.DirectoryPath, "access-agent.db");
        await File.WriteAllTextAsync(databasePath, "existing-local-state");

        var previous = Credentials(Guid.NewGuid());
        var store = new RecordingCredentialStore(previous);
        var client = PairingClient(Guid.NewGuid());

        var result = await PairAsync(client, store, files.Probe(databasePath));

        Assert.False(result.Success);
        Assert.Equal(previous, store.Current);
        Assert.Equal(0, store.SaveCount);
        Assert.Equal("existing-local-state", await File.ReadAllTextAsync(databasePath));
    }

    [Fact]
    public async Task CrossGymRepairDoesNotReplaceProtectedWindowsCredential()
    {
        using var files = new TemporaryFiles();
        var databasePath = Path.Combine(files.DirectoryPath, "access-agent.db");
        var credentialPath = Path.Combine(files.DirectoryPath, "credentials.dat");
        var previous = Credentials(Guid.NewGuid());
        var store = new WindowsAgentCredentialStore(credentialPath);
        await store.SaveAsync(previous, CancellationToken.None);
        await File.WriteAllTextAsync(databasePath, "existing-local-state");

        var result = await PairAsync(
            PairingClient(Guid.NewGuid()),
            store,
            files.Probe(databasePath));

        Assert.False(result.Success);
        Assert.Equal(previous, await store.LoadAsync(CancellationToken.None));
        Assert.Equal("existing-local-state", await File.ReadAllTextAsync(databasePath));
    }

    [Fact]
    public async Task FirstPairingWithoutPreviousStateSucceeds()
    {
        using var files = new TemporaryFiles();
        var gymId = Guid.NewGuid();
        var store = new RecordingCredentialStore(null);
        var client = PairingClient(gymId);

        var result = await PairAsync(
            client,
            store,
            files.Probe(Path.Combine(files.DirectoryPath, "access-agent.db")));

        Assert.True(result.Success);
        Assert.Equal(gymId, store.Current?.GymId);
        Assert.Equal(1, store.SaveCount);
    }

    [Theory]
    [InlineData("access-agent.db")]
    [InlineData("access-agent.db-wal")]
    [InlineData("lookup.key")]
    [InlineData("credentials.partial.tmp")]
    public async Task MissingIdentityWithLocalArtifactsIsRejectedBeforeApiCall(string fileName)
    {
        using var files = new TemporaryFiles();
        await File.WriteAllTextAsync(
            Path.Combine(files.DirectoryPath, fileName),
            "existing-local-state");

        var store = new RecordingCredentialStore(null);
        var client = PairingClient(Guid.NewGuid());

        var result = await PairAsync(
            client,
            store,
            files.Probe(Path.Combine(files.DirectoryPath, "access-agent.db")));

        Assert.False(result.Success);
        Assert.Equal(0, store.SaveCount);
        await client.DidNotReceiveWithAnyArgs().PairAsync(default!, default!, default!, default);
        Assert.True(File.Exists(Path.Combine(files.DirectoryPath, fileName)));
    }

    [Fact]
    public async Task InvalidPreviousIdentityIsRejectedBeforeApiCall()
    {
        using var files = new TemporaryFiles();
        var store = new RecordingCredentialStore(Credentials(Guid.Empty));
        var client = PairingClient(Guid.NewGuid());

        var result = await PairAsync(
            client,
            store,
            files.Probe(Path.Combine(files.DirectoryPath, "access-agent.db")));

        Assert.False(result.Success);
        Assert.Equal(0, store.SaveCount);
        await client.DidNotReceiveWithAnyArgs().PairAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task UnreadablePreviousCredentialIsRejectedBeforeApiCall()
    {
        using var files = new TemporaryFiles();
        var credentialPath = Path.Combine(files.DirectoryPath, "credentials.dat");
        await File.WriteAllTextAsync(credentialPath, "damaged-credential");

        var store = new WindowsAgentCredentialStore(credentialPath);
        var client = PairingClient(Guid.NewGuid());

        var result = await PairAsync(
            client,
            store,
            files.Probe(Path.Combine(files.DirectoryPath, "access-agent.db")));

        Assert.False(result.Success);
        await client.DidNotReceiveWithAnyArgs().PairAsync(default!, default!, default!, default);
        Assert.Equal("damaged-credential", await File.ReadAllTextAsync(credentialPath));
    }

    [Fact]
    public async Task IdentityChangedDuringApiCallIsNotOverwritten()
    {
        using var files = new TemporaryFiles();
        var previous = Credentials(Guid.NewGuid());
        var replacement = Credentials(previous.GymId);
        var store = new RecordingCredentialStore(previous)
        {
            ChangeOnSecondLoad = replacement
        };
        var client = PairingClient(previous.GymId);

        var result = await PairAsync(
            client,
            store,
            files.Probe(Path.Combine(files.DirectoryPath, "access-agent.db")));

        Assert.False(result.Success);
        Assert.Equal(replacement, store.Current);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public async Task SaveFailureLeavesPreviousCredentialUntouched()
    {
        using var files = new TemporaryFiles();
        var previous = Credentials(Guid.NewGuid());
        var store = new RecordingCredentialStore(previous)
        {
            FailSave = true
        };
        var client = PairingClient(previous.GymId);

        var result = await PairAsync(
            client,
            store,
            files.Probe(Path.Combine(files.DirectoryPath, "access-agent.db")));

        Assert.False(result.Success);
        Assert.Equal(previous, store.Current);
    }

    [Fact]
    public async Task ConcurrentPairingsCannotReplaceFirstTenantIdentity()
    {
        using var files = new TemporaryFiles();
        var store = new RecordingCredentialStore(null);
        var firstGym = Guid.NewGuid();
        var secondClient = PairingClient(Guid.NewGuid());
        var firstClient = new BlockingPairingClient(firstGym);
        var probe = files.Probe(Path.Combine(files.DirectoryPath, "access-agent.db"));

        var firstPairing = PairAsync(firstClient, store, probe);
        await firstClient.RequestStarted.Task;

        var secondResult = await PairAsync(secondClient, store, probe);
        Assert.False(secondResult.Success);
        await secondClient.DidNotReceiveWithAnyArgs()
            .PairAsync(default!, default!, default!, default);

        firstClient.Complete();
        Assert.True((await firstPairing).Success);
        Assert.Equal(firstGym, store.Current?.GymId);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task WindowsCredentialReplacementLeavesOnlyCompleteProtectedCredential()
    {
        using var files = new TemporaryFiles();
        var credentialPath = Path.Combine(files.DirectoryPath, "credentials.dat");
        var store = new WindowsAgentCredentialStore(credentialPath);
        var previous = Credentials(Guid.NewGuid());
        var replacement = previous with { Secret = "new-secret" };

        await store.SaveAsync(previous, CancellationToken.None);
        await store.SaveAsync(replacement, CancellationToken.None);

        Assert.Equal(replacement, await store.LoadAsync(CancellationToken.None));
        Assert.Single(Directory.EnumerateFiles(files.DirectoryPath));
    }

    private static AgentCredentials Credentials(Guid gymId) =>
        new(Guid.NewGuid(), gymId, "old-secret", ApiBaseUrl);

    private static IAccessAgentPairingClient PairingClient(Guid gymId)
    {
        var client = Substitute.For<IAccessAgentPairingClient>();
        client.PairAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PairAccessAgentResult?>(
                new PairAccessAgentResult(
                    Guid.NewGuid(), gymId, "Agent", MachineName, "new-secret")));
        return client;
    }

    private static Task<PairingOutcome> PairAsync(
        IAccessAgentPairingClient client,
        IAgentCredentialStore store,
        ILocalAgentStateProbe stateProbe) =>
        AgentPairingBootstrap.PairWithCodeAsync(
            client, store, stateProbe, ApiBaseUrl, PairingCode,
            MachineName, CancellationToken.None);

    private sealed class RecordingCredentialStore(
        AgentCredentials? current) : IAgentCredentialStore
    {
        public AgentCredentials? Current { get; private set; } = current;
        public AgentCredentials? ChangeOnSecondLoad { get; init; }
        public bool FailSave { get; init; }
        public int SaveCount { get; private set; }
        private int _loadCount;

        public Task<AgentCredentials?> LoadAsync(CancellationToken cancellationToken)
        {
            _loadCount++;
            if (_loadCount == 2 && ChangeOnSecondLoad is not null)
                Current = ChangeOnSecondLoad;
            return Task.FromResult(Current);
        }

        public Task SaveAsync(
            AgentCredentials credentials,
            CancellationToken cancellationToken)
        {
            SaveCount++;
            if (FailSave)
                throw new IOException("Simulated write failure");
            Current = credentials;
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingPairingClient(Guid gymId) : IAccessAgentPairingClient
    {
        public TaskCompletionSource RequestStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<PairAccessAgentResult?> _result =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<PairAccessAgentResult?> PairAsync(
            string apiBaseUrl,
            string pairingCode,
            string machineName,
            CancellationToken cancellationToken)
        {
            RequestStarted.SetResult();
            return _result.Task;
        }

        public void Complete() => _result.SetResult(
            new PairAccessAgentResult(
                Guid.NewGuid(), gymId, "Agent", MachineName, "new-secret"));
    }

    private sealed class TemporaryFiles : IDisposable
    {
        public string DirectoryPath { get; } = Path.Combine(
            Path.GetTempPath(), $"avelri-pairing-test-{Guid.NewGuid():N}");

        public TemporaryFiles() => Directory.CreateDirectory(DirectoryPath);

        public FileSystemLocalAgentStateProbe Probe(string databasePath) =>
            new(DirectoryPath, databasePath);

        public void Dispose() => Directory.Delete(DirectoryPath, recursive: true);
    }
}
