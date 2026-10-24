using GymFlow.Application.DTOs.PhysicalAccess;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Security;
using GymFlow.Application.Interfaces.Time;
using GymFlow.Application.Services;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using NSubstitute;

namespace GymFlow.Application.Tests.Services;

public sealed class PhysicalAccessPinLimiterBypassTests
{
    [Theory]
    [InlineData(PhysicalAccessCredentialType.Card, "synthetic-card")]
    [InlineData(PhysicalAccessCredentialType.BiometricExternalId, "synthetic-biometric")]
    public async Task EligibleNonPinCredentialsBypassBlockedPinLimiter(
        PhysicalAccessCredentialType credentialType, string externalIdentifier)
    {
        var gymId = Guid.NewGuid();
        var student = new Student
        {
            Id = Guid.NewGuid(), GymId = gymId, IsActive = true
        };
        var credential = new PhysicalAccessCredential
        {
            Id = Guid.NewGuid(), GymId = gymId,
            StudentId = student.Id, Student = student,
            Type = credentialType, ProviderKey = "synthetic",
            ExternalIdentifier = externalIdentifier, IsActive = true
        };
        var credentials = Substitute.For<IPhysicalAccessCredentialRepository>();
        credentials.GetByExternalIdentifierAsync(gymId, credential.ProviderKey,
                credentialType, externalIdentifier)
            .Returns(credential);
        var events = Substitute.For<IPhysicalAccessEventRepository>();
        events.AddAsync(Arg.Any<PhysicalAccessEvent>())
            .Returns(call => call.Arg<PhysicalAccessEvent>());
        var policy = Substitute.For<IGymErpPolicy>();
        policy.IsEnabled(gymId).Returns(false);
        var limiter = Substitute.For<IPhysicalAccessPinAttemptLimiter>();
        limiter.TryReserveAsync(Arg.Any<Guid>())
            .Returns((PhysicalAccessPinReservation?)null);
        var service = new PhysicalAccessService(
            credentials,
            Substitute.For<IPhysicalAccessOverrideRepository>(),
            events,
            Substitute.For<IEnrollmentRepository>(),
            Substitute.For<IChargeRepository>(),
            Substitute.For<IGymTimeZoneProvider>(),
            policy,
            Substitute.For<IPhysicalAccessPinProtector>(),
            limiter);

        var result = await service.DecideAsync(gymId,
            new PhysicalAccessDecisionRequest
            {
                RequestId = Guid.NewGuid(), ProviderKey = credential.ProviderKey,
                CredentialType = credentialType, ExternalIdentifier = externalIdentifier,
                OccurredAt = DateTime.UtcNow
            });

        Assert.Equal(PhysicalAccessDecision.Allowed, result.Decision);
        Assert.Equal(PhysicalAccessDecisionReason.Eligible, result.Reason);
        Assert.Equal(student.Id, result.StudentId);
        await limiter.DidNotReceive().TryReserveAsync(Arg.Any<Guid>());
        await limiter.DidNotReceive().CompleteAsync(
            Arg.Any<PhysicalAccessPinReservation>(), Arg.Any<bool>());
    }
}
