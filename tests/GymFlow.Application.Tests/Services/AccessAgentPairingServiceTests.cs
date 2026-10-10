using GymFlow.Application.DTOs.AccessAgents;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Security;
using GymFlow.Application.Services;
using GymFlow.Domain.Entities;
using NSubstitute;

namespace GymFlow.Application.Tests.Services;

public class AccessAgentPairingServiceTests
{
    private readonly IAccessAgentRepository
        _accessAgentRepository;

    private readonly IAccessAgentPairingCodeRepository
        _pairingRepository;

    private readonly IAccessAgentPairingCodeService
        _pairingCodeService;

    private readonly IAccessAgentSecretService
        _secretService;

    private readonly AccessAgentPairingService
        _service;

    public AccessAgentPairingServiceTests()
    {
        _accessAgentRepository =
            Substitute.For<IAccessAgentRepository>();

        _pairingRepository =
            Substitute.For<IAccessAgentPairingCodeRepository>();

        _pairingCodeService =
            Substitute.For<IAccessAgentPairingCodeService>();

        _secretService =
            Substitute.For<IAccessAgentSecretService>();

        _service =
            new AccessAgentPairingService(
                _accessAgentRepository,
                _pairingRepository,
                _pairingCodeService,
                _secretService);
    }

    [Fact]
    public async Task CreateCodeAsync_ShouldStoreOnlyHash()
    {
        var gymId =
            Guid.NewGuid();

        var userId =
            Guid.NewGuid();

        _pairingCodeService
            .GenerateCode()
            .Returns("ABCD-EFGH");

        _pairingCodeService
            .Hash("ABCD-EFGH")
            .Returns("hashed-code");

        var result =
            await _service
                .CreateCodeAsync(
                    gymId,
                    userId);

        Assert.NotNull(result);
        Assert.Equal(
            "ABCD-EFGH",
            result.Code);

        await _pairingRepository
            .Received(1)
            .ReplaceActiveAsync(
                Arg.Is<AccessAgentPairingCode>(
                    pairingCode =>
                        pairingCode.GymId ==
                            gymId &&
                        pairingCode.CreatedByUserId ==
                            userId &&
                        pairingCode.CodeHash ==
                            "hashed-code"),
                Arg.Any<DateTime>());
    }

    [Fact]
    public async Task PairAsync_WhenMachineIsNew_ShouldCreateAgent()
    {
        var gymId =
            Guid.NewGuid();

        _pairingCodeService
            .Hash("ABCD-EFGH")
            .Returns("hashed-code");

        _pairingRepository
            .TryConsumeAsync(
                "hashed-code",
                Arg.Any<DateTime>())
            .Returns(
                new AccessAgentPairingCode
                {
                    Id = Guid.NewGuid(),
                    GymId = gymId,
                    CreatedByUserId =
                        Guid.NewGuid(),
                    CodeHash =
                        "hashed-code",
                    ExpiresAt =
                        DateTime.UtcNow
                            .AddMinutes(5)
                });

        _accessAgentRepository
            .GetByMachineNameAsync(
                gymId,
                "RECEPCAO-01")
            .Returns(
                (AccessAgent?)null);

        _secretService
            .GenerateSecret()
            .Returns("raw-secret");

        _secretService
            .Hash("raw-secret")
            .Returns("hashed-secret");

        var result =
            await _service
                .PairAsync(
                    new PairAccessAgentRequest
                    {
                        Code =
                            "ABCD-EFGH",
                        MachineName =
                            " RECEPCAO-01 "
                    });

        Assert.NotNull(result);
        Assert.Equal(
            gymId,
            result.GymId);
        Assert.Equal(
            "RECEPCAO-01",
            result.MachineName);
        Assert.Equal(
            "raw-secret",
            result.Secret);

        await _accessAgentRepository
            .Received(1)
            .AddAsync(
                Arg.Is<AccessAgent>(
                    agent =>
                        agent.GymId ==
                            gymId &&
                        agent.MachineName ==
                            "RECEPCAO-01" &&
                        agent.SecretHash ==
                            "hashed-secret" &&
                        agent.CredentialVersion ==
                            1 &&
                        !agent.ReleaseEnabled));
    }

    [Fact]
    public async Task PairAsync_WhenMachineExists_ShouldRotateCredentialAndFailSafeRelease()
    {
        var gymId =
            Guid.NewGuid();

        var existing =
            new AccessAgent
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                Name =
                    "Catraca principal",
                MachineName =
                    "RECEPCAO-01",
                SecretHash =
                    "old-hash",
                CredentialVersion = 4,
                IsActive = true,
                ReleaseEnabled = true,
                ConfigurationVersion = 7,
                AppliedConfigurationVersion = 7,
                LastSeenAt =
                    DateTime.UtcNow
            };

        _pairingCodeService
            .Hash("ABCD-EFGH")
            .Returns("hashed-code");

        _pairingRepository
            .TryConsumeAsync(
                "hashed-code",
                Arg.Any<DateTime>())
            .Returns(
                new AccessAgentPairingCode
                {
                    Id = Guid.NewGuid(),
                    GymId = gymId,
                    CreatedByUserId =
                        Guid.NewGuid(),
                    CodeHash =
                        "hashed-code",
                    ExpiresAt =
                        DateTime.UtcNow
                            .AddMinutes(5)
                });

        _accessAgentRepository
            .GetByMachineNameAsync(
                gymId,
                "RECEPCAO-01")
            .Returns(existing);

        _secretService
            .GenerateSecret()
            .Returns("new-secret");

        _secretService
            .Hash("new-secret")
            .Returns("new-hash");

        var result =
            await _service
                .PairAsync(
                    new PairAccessAgentRequest
                    {
                        Code =
                            "ABCD-EFGH",
                        MachineName =
                            "RECEPCAO-01"
                    });

        Assert.NotNull(result);

        Assert.Equal(
            "new-hash",
            existing.SecretHash);

        Assert.Equal(
            5,
            existing.CredentialVersion);

        Assert.False(
            existing.ReleaseEnabled);

        Assert.Equal(
            8,
            existing.ConfigurationVersion);

        Assert.Null(
            existing.AppliedConfigurationVersion);

        Assert.Null(
            existing.LastSeenAt);

        await _accessAgentRepository
            .Received(1)
            .UpdateAsync(existing);
    }

    [Fact]
    public async Task PairAsync_WhenCodeIsInvalid_ShouldNotCreateOrUpdateAgent()
    {
        _pairingCodeService
            .Hash("INVALID")
            .Returns("invalid-hash");

        _pairingRepository
            .TryConsumeAsync(
                "invalid-hash",
                Arg.Any<DateTime>())
            .Returns(
                (AccessAgentPairingCode?)null);

        var result =
            await _service
                .PairAsync(
                    new PairAccessAgentRequest
                    {
                        Code = "INVALID",
                        MachineName =
                            "RECEPCAO-01"
                    });

        Assert.Null(result);

        await _accessAgentRepository
            .DidNotReceive()
            .AddAsync(
                Arg.Any<AccessAgent>());

        await _accessAgentRepository
            .DidNotReceive()
            .UpdateAsync(
                Arg.Any<AccessAgent>());
    }
}
