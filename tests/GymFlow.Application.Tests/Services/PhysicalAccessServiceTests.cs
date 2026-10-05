using GymFlow.Application.DTOs.PhysicalAccess;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Time;
using GymFlow.Application.Services;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using NSubstitute;

namespace GymFlow.Application.Tests.Services;

public class PhysicalAccessServiceTests
{
    private readonly IPhysicalAccessCredentialRepository
        _credentialRepository;

    private readonly IPhysicalAccessOverrideRepository
        _overrideRepository;

    private readonly IPhysicalAccessEventRepository
        _eventRepository;

    private readonly IEnrollmentRepository
        _enrollmentRepository;

    private readonly IChargeRepository
        _chargeRepository;

    private readonly IGymTimeZoneProvider
        _gymTimeZoneProvider;

    private readonly PhysicalAccessService _service;

    public PhysicalAccessServiceTests()
    {
        _credentialRepository =
            Substitute.For<IPhysicalAccessCredentialRepository>();

        _overrideRepository =
            Substitute.For<IPhysicalAccessOverrideRepository>();

        _eventRepository =
            Substitute.For<IPhysicalAccessEventRepository>();

        _enrollmentRepository =
            Substitute.For<IEnrollmentRepository>();

        _chargeRepository =
            Substitute.For<IChargeRepository>();

        _gymTimeZoneProvider =
            Substitute.For<IGymTimeZoneProvider>();

        _eventRepository
            .AddAsync(
             Arg.Any<PhysicalAccessEvent>())
            .Returns(call =>
            call.Arg<PhysicalAccessEvent>());

        _service = new PhysicalAccessService(
            _credentialRepository,
            _overrideRepository,
            _eventRepository,
            _enrollmentRepository,
            _chargeRepository,
            _gymTimeZoneProvider);
    }

    [Fact]
    public async Task DecideAsync_WhenRequestWasAlreadyProcessed_ShouldReturnPreviousDecision()
    {
        var gymId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var existingEvent = new PhysicalAccessEvent
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            RequestId = requestId,
            StudentId = Guid.NewGuid(),
            CredentialId = Guid.NewGuid(),
            Decision = PhysicalAccessDecision.Allowed,
            Reason = PhysicalAccessDecisionReason.Eligible,
            OccurredAt = DateTime.UtcNow.AddMinutes(-1),
            ProcessedAt = DateTime.UtcNow
        };

        _eventRepository
            .GetByRequestIdAsync(
                gymId,
                requestId)
            .Returns(existingEvent);

        var request = new PhysicalAccessDecisionRequest
        {
            RequestId = requestId,
            ProviderKey = "toletus",
            CredentialType =
                PhysicalAccessCredentialType.BiometricExternalId,
            ExternalIdentifier = "1847",
            OccurredAt = DateTime.UtcNow
        };

        var result =
            await _service.DecideAsync(
                gymId,
                request);

        Assert.Equal(
            PhysicalAccessDecision.Allowed,
            result.Decision);

        Assert.Equal(
            PhysicalAccessDecisionReason.Eligible,
            result.Reason);

        Assert.Equal(
            existingEvent.StudentId,
            result.StudentId);

        await _credentialRepository
            .DidNotReceiveWithAnyArgs()
            .GetByExternalIdentifierAsync(
                default,
                default!,
                default,
                default!);

        await _eventRepository
            .DidNotReceiveWithAnyArgs()
            .AddAsync(default!);
    }

    [Fact]
    public async Task DecideAsync_WhenCredentialDoesNotExist_ShouldDenyAccess()
    {
        var gymId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var request = new PhysicalAccessDecisionRequest
        {
            RequestId = requestId,
            ProviderKey = "toletus",
            CredentialType =
                PhysicalAccessCredentialType.BiometricExternalId,
            ExternalIdentifier = "9999",
            OccurredAt = DateTime.UtcNow
        };

        _eventRepository
            .GetByRequestIdAsync(
                gymId,
                requestId)
            .Returns((PhysicalAccessEvent?)null);

        _credentialRepository
            .GetByExternalIdentifierAsync(
                gymId,
                "toletus",
                PhysicalAccessCredentialType.BiometricExternalId,
                "9999")
            .Returns((PhysicalAccessCredential?)null);

        var result =
            await _service.DecideAsync(
                gymId,
                request);

        Assert.Equal(
            PhysicalAccessDecision.Denied,
            result.Decision);

        Assert.Equal(
            PhysicalAccessDecisionReason.CredentialNotFound,
            result.Reason);

        Assert.Null(result.StudentId);
        Assert.Null(result.CredentialId);

        await _eventRepository
            .Received(1)
            .AddAsync(
                Arg.Is<PhysicalAccessEvent>(
                    accessEvent =>
                        accessEvent.GymId == gymId &&
                        accessEvent.RequestId == requestId &&
                        accessEvent.Decision ==
                            PhysicalAccessDecision.Denied &&
                        accessEvent.Reason ==
                            PhysicalAccessDecisionReason
                                .CredentialNotFound));
    }

    [Fact]
    public async Task DecideAsync_WhenManualBlockExists_ShouldDenyAccess()
    {
        var gymId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();

        var student = new Student
        {
            Id = studentId,
            User = new User
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                Name = "Aluno Teste",
                IsActive = true,
                Role = UserRole.Student
            }
        };

        var credential = new PhysicalAccessCredential
        {
            Id = credentialId,
            GymId = gymId,
            StudentId = studentId,
            Type =
                PhysicalAccessCredentialType.BiometricExternalId,
            ProviderKey = "toletus",
            ExternalIdentifier = "1847",
            IsActive = true,
            Student = student
        };

        var request = new PhysicalAccessDecisionRequest
        {
            RequestId = Guid.NewGuid(),
            ProviderKey = "toletus",
            CredentialType =
                PhysicalAccessCredentialType.BiometricExternalId,
            ExternalIdentifier = "1847",
            OccurredAt = DateTime.UtcNow
        };

        _credentialRepository
            .GetByExternalIdentifierAsync(
                gymId,
                "toletus",
                PhysicalAccessCredentialType.BiometricExternalId,
                "1847")
            .Returns(credential);

        _overrideRepository
            .GetByStudentAsync(
                studentId,
                gymId)
            .Returns(new PhysicalAccessOverride
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                StudentId = studentId,
                Type = PhysicalAccessOverrideType.Block,
                ActorUserId = Guid.NewGuid()
            });

        var result =
            await _service.DecideAsync(
                gymId,
                request);

        Assert.Equal(
            PhysicalAccessDecision.Denied,
            result.Decision);

        Assert.Equal(
            PhysicalAccessDecisionReason.ManualBlock,
            result.Reason);

        await _enrollmentRepository
            .DidNotReceiveWithAnyArgs()
            .GetActiveByStudentAsync(
                default,
                default,
                default);
    }

    [Fact]
    public async Task DecideAsync_WhenManualAllowExists_ShouldAllowAccessWithoutCheckingFinancialRules()
    {
        var gymId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();

        var student = new Student
        {
            Id = studentId,
            User = new User
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                Name = "Aluno Teste",
                IsActive = true,
                Role = UserRole.Student
            }
        };

        var credential = new PhysicalAccessCredential
        {
            Id = credentialId,
            GymId = gymId,
            StudentId = studentId,
            Type =
                PhysicalAccessCredentialType.BiometricExternalId,
            ProviderKey = "toletus",
            ExternalIdentifier = "1847",
            IsActive = true,
            Student = student
        };

        var request = new PhysicalAccessDecisionRequest
        {
            RequestId = Guid.NewGuid(),
            ProviderKey = "toletus",
            CredentialType =
                PhysicalAccessCredentialType.BiometricExternalId,
            ExternalIdentifier = "1847",
            OccurredAt = DateTime.UtcNow
        };

        _credentialRepository
            .GetByExternalIdentifierAsync(
                gymId,
                "toletus",
                PhysicalAccessCredentialType.BiometricExternalId,
                "1847")
            .Returns(credential);

        _overrideRepository
            .GetByStudentAsync(
                studentId,
                gymId)
            .Returns(new PhysicalAccessOverride
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                StudentId = studentId,
                Type = PhysicalAccessOverrideType.Allow,
                ActorUserId = Guid.NewGuid()
            });

        var result =
            await _service.DecideAsync(
                gymId,
                request);

        Assert.Equal(
            PhysicalAccessDecision.Allowed,
            result.Decision);

        Assert.Equal(
            PhysicalAccessDecisionReason.ManualOverride,
            result.Reason);

        await _enrollmentRepository
            .DidNotReceiveWithAnyArgs()
            .GetActiveByStudentAsync(
                default,
                default,
                default);

        await _chargeRepository
            .DidNotReceiveWithAnyArgs()
            .GetByEnrollmentIdAsync(
                default,
                default);
    }

    [Fact]
    public async Task DecideAsync_WhenNoValidEnrollmentExists_ShouldDenyAccess()
    {
        var gymId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var student = new Student
        {
            Id = studentId,
            User = new User
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                Name = "Aluno Teste",
                IsActive = true,
                Role = UserRole.Student
            }
        };

        var credential = new PhysicalAccessCredential
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            StudentId = studentId,
            Type =
                PhysicalAccessCredentialType.BiometricExternalId,
            ProviderKey = "toletus",
            ExternalIdentifier = "1847",
            IsActive = true,
            Student = student
        };

        var request = new PhysicalAccessDecisionRequest
        {
            RequestId = Guid.NewGuid(),
            ProviderKey = "toletus",
            CredentialType =
                PhysicalAccessCredentialType.BiometricExternalId,
            ExternalIdentifier = "1847",
            OccurredAt = DateTime.UtcNow
        };

        _credentialRepository
            .GetByExternalIdentifierAsync(
                gymId,
                "toletus",
                PhysicalAccessCredentialType.BiometricExternalId,
                "1847")
            .Returns(credential);

        _overrideRepository
            .GetByStudentAsync(
                studentId,
                gymId)
            .Returns((PhysicalAccessOverride?)null);

        _gymTimeZoneProvider
            .GetTimeZone(gymId)
            .Returns(TimeZoneInfo.Utc);

        _enrollmentRepository
            .GetActiveByStudentAsync(
                studentId,
                gymId,
                Arg.Any<DateOnly>())
            .Returns((Enrollment?)null);

        var result =
            await _service.DecideAsync(
                gymId,
                request);

        Assert.Equal(
            PhysicalAccessDecision.Denied,
            result.Decision);

        Assert.Equal(
            PhysicalAccessDecisionReason.NoValidEnrollment,
            result.Reason);
    }

    [Fact]
    public async Task DecideAsync_WhenChargeIsNotPaid_ShouldDenyAccess()
    {
        var gymId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var student = new Student
        {
            Id = studentId,
            User = new User
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                Name = "Aluno Teste",
                IsActive = true,
                Role = UserRole.Student
            }
        };

        var credential = new PhysicalAccessCredential
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            StudentId = studentId,
            Type =
                PhysicalAccessCredentialType.BiometricExternalId,
            ProviderKey = "toletus",
            ExternalIdentifier = "1847",
            IsActive = true,
            Student = student
        };

        var enrollment = new Enrollment
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            Status = EnrollmentStatus.Active
        };

        var request = new PhysicalAccessDecisionRequest
        {
            RequestId = Guid.NewGuid(),
            ProviderKey = "toletus",
            CredentialType =
                PhysicalAccessCredentialType.BiometricExternalId,
            ExternalIdentifier = "1847",
            OccurredAt = DateTime.UtcNow
        };

        _credentialRepository
            .GetByExternalIdentifierAsync(
                gymId,
                "toletus",
                PhysicalAccessCredentialType.BiometricExternalId,
                "1847")
            .Returns(credential);

        _overrideRepository
            .GetByStudentAsync(
                studentId,
                gymId)
            .Returns((PhysicalAccessOverride?)null);

        _gymTimeZoneProvider
            .GetTimeZone(gymId)
            .Returns(TimeZoneInfo.Utc);

        _enrollmentRepository
            .GetActiveByStudentAsync(
                studentId,
                gymId,
                Arg.Any<DateOnly>())
            .Returns(enrollment);

        _chargeRepository
            .GetByEnrollmentIdAsync(
                enrollment.Id,
                gymId)
            .Returns(new Charge
            {
                Id = Guid.NewGuid(),
                EnrollmentId = enrollment.Id,
                Amount = 150m,
                DueDate =
                    DateOnly.FromDateTime(
                        DateTime.UtcNow),
                Status = ChargeStatus.Pending
            });

        var result =
            await _service.DecideAsync(
                gymId,
                request);

        Assert.Equal(
            PhysicalAccessDecision.Denied,
            result.Decision);

        Assert.Equal(
            PhysicalAccessDecisionReason.FinancialRestriction,
            result.Reason);
    }

    [Fact]
    public async Task DecideAsync_WhenStudentIsEligible_ShouldAllowAccess()
    {
        var gymId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var student = new Student
        {
            Id = studentId,
            User = new User
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                Name = "Aluno Teste",
                IsActive = true,
                Role = UserRole.Student
            }
        };

        var credential = new PhysicalAccessCredential
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            StudentId = studentId,
            Type =
                PhysicalAccessCredentialType.BiometricExternalId,
            ProviderKey = "toletus",
            ExternalIdentifier = "1847",
            IsActive = true,
            Student = student
        };

        var enrollment = new Enrollment
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            Status = EnrollmentStatus.Active
        };

        var request = new PhysicalAccessDecisionRequest
        {
            RequestId = Guid.NewGuid(),
            ProviderKey = "toletus",
            CredentialType =
                PhysicalAccessCredentialType.BiometricExternalId,
            ExternalIdentifier = "1847",
            OccurredAt = DateTime.UtcNow
        };

        _credentialRepository
            .GetByExternalIdentifierAsync(
                gymId,
                "toletus",
                PhysicalAccessCredentialType.BiometricExternalId,
                "1847")
            .Returns(credential);

        _overrideRepository
            .GetByStudentAsync(
                studentId,
                gymId)
            .Returns((PhysicalAccessOverride?)null);

        _gymTimeZoneProvider
            .GetTimeZone(gymId)
            .Returns(TimeZoneInfo.Utc);

        _enrollmentRepository
            .GetActiveByStudentAsync(
                studentId,
                gymId,
                Arg.Any<DateOnly>())
            .Returns(enrollment);

        _chargeRepository
            .GetByEnrollmentIdAsync(
                enrollment.Id,
                gymId)
            .Returns(new Charge
            {
                Id = Guid.NewGuid(),
                EnrollmentId = enrollment.Id,
                Amount = 150m,
                PaidAmount = 150m,
                DueDate =
                    DateOnly.FromDateTime(
                        DateTime.UtcNow),
                Status = ChargeStatus.Paid,
                PaidAt = DateTime.UtcNow
            });

        var result =
            await _service.DecideAsync(
                gymId,
                request);

        Assert.Equal(
            PhysicalAccessDecision.Allowed,
            result.Decision);

        Assert.Equal(
            PhysicalAccessDecisionReason.Eligible,
            result.Reason);

        await _eventRepository
            .Received(1)
            .AddAsync(
                Arg.Is<PhysicalAccessEvent>(
                    accessEvent =>
                        accessEvent.StudentId == studentId &&
                        accessEvent.Decision ==
                            PhysicalAccessDecision.Allowed &&
                        accessEvent.Reason ==
                            PhysicalAccessDecisionReason.Eligible));
    }

    [Fact]
    public async Task DecideAsync_WhenCredentialIsInactive_ShouldDenyAccess()
    {
        var gymId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var student = new Student
        {
            Id = studentId,
            User = new User
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                Name = "Aluno Teste",
                IsActive = true,
                Role = UserRole.Student
            }
        };

        var credential = new PhysicalAccessCredential
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            StudentId = studentId,
            Type =
                PhysicalAccessCredentialType.BiometricExternalId,
            ProviderKey = "toletus",
            ExternalIdentifier = "1847",
            IsActive = false,
            Student = student
        };

        var request = new PhysicalAccessDecisionRequest
        {
            RequestId = Guid.NewGuid(),
            ProviderKey = "toletus",
            CredentialType =
                PhysicalAccessCredentialType.BiometricExternalId,
            ExternalIdentifier = "1847",
            OccurredAt = DateTime.UtcNow
        };

        _credentialRepository
            .GetByExternalIdentifierAsync(
                gymId,
                "toletus",
                PhysicalAccessCredentialType.BiometricExternalId,
                "1847")
            .Returns(credential);

        var result =
            await _service.DecideAsync(
                gymId,
                request);

        Assert.Equal(
            PhysicalAccessDecision.Denied,
            result.Decision);

        Assert.Equal(
            PhysicalAccessDecisionReason.CredentialInactive,
            result.Reason);
    }

    [Fact]
    public async Task DecideAsync_WhenStudentIsInactive_ShouldDenyAccess()
    {
        var gymId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var student = new Student
        {
            Id = studentId,
            User = new User
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                Name = "Aluno Teste",
                IsActive = false,
                Role = UserRole.Student
            }
        };

        var credential = new PhysicalAccessCredential
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            StudentId = studentId,
            Type =
                PhysicalAccessCredentialType.BiometricExternalId,
            ProviderKey = "toletus",
            ExternalIdentifier = "1847",
            IsActive = true,
            Student = student
        };

        var request = new PhysicalAccessDecisionRequest
        {
            RequestId = Guid.NewGuid(),
            ProviderKey = "toletus",
            CredentialType =
                PhysicalAccessCredentialType.BiometricExternalId,
            ExternalIdentifier = "1847",
            OccurredAt = DateTime.UtcNow
        };

        _credentialRepository
            .GetByExternalIdentifierAsync(
                gymId,
                "toletus",
                PhysicalAccessCredentialType.BiometricExternalId,
                "1847")
            .Returns(credential);

        var result =
            await _service.DecideAsync(
                gymId,
                request);

        Assert.Equal(
            PhysicalAccessDecision.Denied,
            result.Decision);

        Assert.Equal(
            PhysicalAccessDecisionReason.StudentInactive,
            result.Reason);

        await _overrideRepository
            .DidNotReceiveWithAnyArgs()
            .GetByStudentAsync(
                default,
                default);
    }

    [Fact]
    public async Task DecideAsync_WhenStudentIsArchived_ShouldDenyAccess()
    {
        var gymId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var student = new Student
        {
            Id = studentId,
            ArchivedAt = DateTime.UtcNow.AddDays(-1),
            User = new User
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                Name = "Aluno Arquivado",
                IsActive = false,
                Role = UserRole.Student
            }
        };

        var credential = new PhysicalAccessCredential
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            StudentId = studentId,
            Type =
                PhysicalAccessCredentialType.BiometricExternalId,
            ProviderKey = "toletus",
            ExternalIdentifier = "1847",
            IsActive = true,
            Student = student
        };

        var request = new PhysicalAccessDecisionRequest
        {
            RequestId = Guid.NewGuid(),
            ProviderKey = "toletus",
            CredentialType =
                PhysicalAccessCredentialType.BiometricExternalId,
            ExternalIdentifier = "1847",
            OccurredAt = DateTime.UtcNow
        };

        _credentialRepository
            .GetByExternalIdentifierAsync(
                gymId,
                "toletus",
                PhysicalAccessCredentialType.BiometricExternalId,
                "1847")
            .Returns(credential);

        var result =
            await _service.DecideAsync(
                gymId,
                request);

        Assert.Equal(
            PhysicalAccessDecision.Denied,
            result.Decision);

        Assert.Equal(
            PhysicalAccessDecisionReason.StudentArchived,
            result.Reason);

        await _overrideRepository
            .DidNotReceiveWithAnyArgs()
            .GetByStudentAsync(
                default,
                default);
    }

    [Fact]
    public async Task DecideAsync_WhenRequestIdIsEmpty_ShouldThrow()
    {
        var request = new PhysicalAccessDecisionRequest
        {
            RequestId = Guid.Empty,
            ProviderKey = "toletus",
            CredentialType =
                PhysicalAccessCredentialType.BiometricExternalId,
            ExternalIdentifier = "1847",
            OccurredAt = DateTime.UtcNow
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.DecideAsync(
                Guid.NewGuid(),
                request));

        await _eventRepository
            .DidNotReceive()
            .AddAsync(
                Arg.Any<PhysicalAccessEvent>());
    }

    [Fact]
    public async Task DecideAsync_WhenProviderKeyIsEmpty_ShouldThrow()
    {
        var request = new PhysicalAccessDecisionRequest
        {
            RequestId = Guid.NewGuid(),
            ProviderKey = " ",
            CredentialType =
                PhysicalAccessCredentialType.BiometricExternalId,
            ExternalIdentifier = "1847",
            OccurredAt = DateTime.UtcNow
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.DecideAsync(
                Guid.NewGuid(),
                request));
    }

    [Fact]
    public async Task DecideAsync_WhenCredentialTypeIsInvalid_ShouldThrow()
    {
        var request = new PhysicalAccessDecisionRequest
        {
            RequestId = Guid.NewGuid(),
            ProviderKey = "toletus",
            CredentialType =
                (PhysicalAccessCredentialType)999,
            ExternalIdentifier = "1847",
            OccurredAt = DateTime.UtcNow
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.DecideAsync(
                Guid.NewGuid(),
                request));
    }

    [Fact]
    public async Task DecideAsync_WhenExternalIdentifierIsEmpty_ShouldThrow()
    {
        var request = new PhysicalAccessDecisionRequest
        {
            RequestId = Guid.NewGuid(),
            ProviderKey = "toletus",
            CredentialType =
                PhysicalAccessCredentialType.BiometricExternalId,
            ExternalIdentifier = " ",
            OccurredAt = DateTime.UtcNow
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.DecideAsync(
                Guid.NewGuid(),
                request));
    }

    [Fact]
    public async Task DecideAsync_WhenOccurredAtIsDefault_ShouldThrow()
    {
        var request = new PhysicalAccessDecisionRequest
        {
            RequestId = Guid.NewGuid(),
            ProviderKey = "toletus",
            CredentialType =
                PhysicalAccessCredentialType.BiometricExternalId,
            ExternalIdentifier = "1847",
            OccurredAt = default
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.DecideAsync(
                Guid.NewGuid(),
                request));
    }

    [Fact]
    public async Task DecideAsync_WhenConcurrentRequestWasAlreadyPersisted_ShouldReturnPersistedDecision()
    {
        var gymId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var request = new PhysicalAccessDecisionRequest
        {
            RequestId = requestId,
            ProviderKey = "toletus",
            CredentialType =
                PhysicalAccessCredentialType.BiometricExternalId,
            ExternalIdentifier = "1847",
            OccurredAt = DateTime.UtcNow
        };

        _credentialRepository
            .GetByExternalIdentifierAsync(
                gymId,
                "toletus",
                PhysicalAccessCredentialType.BiometricExternalId,
                "1847")
            .Returns((PhysicalAccessCredential?)null);

        var persistedEvent =
            new PhysicalAccessEvent
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                RequestId = requestId,
                Decision = PhysicalAccessDecision.Allowed,
                Reason =
                    PhysicalAccessDecisionReason.ManualOverride,
                OccurredAt = request.OccurredAt,
                ProcessedAt = DateTime.UtcNow
            };

        _eventRepository
            .AddAsync(
                Arg.Any<PhysicalAccessEvent>())
            .Returns(persistedEvent);

        var result =
            await _service.DecideAsync(
                gymId,
                request);

        Assert.Equal(
            PhysicalAccessDecision.Allowed,
            result.Decision);

        Assert.Equal(
            PhysicalAccessDecisionReason.ManualOverride,
            result.Reason);

        Assert.Equal(
            requestId,
            result.RequestId);
    }
}