using GymFlow.Application.DTOs.CheckIns;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Time;
using GymFlow.Application.Services;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using NSubstitute;

namespace GymFlow.Application.Tests.Services;

public class CheckInServiceTests
{
    private readonly ICheckInRepository
        _checkInRepository;

    private readonly IStudentRepository
        _studentRepository;

    private readonly IPhysicalAccessEventRepository
        _physicalAccessEventRepository;

    private readonly IGymTimeZoneProvider
        _gymTimeZoneProvider;

    private readonly CheckInService
        _service;

    public CheckInServiceTests()
    {
        _checkInRepository =
            Substitute.For<ICheckInRepository>();

        _studentRepository =
            Substitute.For<IStudentRepository>();

        _physicalAccessEventRepository =
            Substitute.For<IPhysicalAccessEventRepository>();

        _gymTimeZoneProvider =
            Substitute.For<IGymTimeZoneProvider>();

        _checkInRepository
            .AddAsync(
                Arg.Any<CheckIn>())
            .Returns(call =>
                call.Arg<CheckIn>());

        _service =
            new CheckInService(
                _checkInRepository,
                _studentRepository,
                _physicalAccessEventRepository,
                _gymTimeZoneProvider);
    }

    [Fact]
    public async Task CreateManualAsync_WhenValid_ShouldCreateManualCheckIn()
    {
        var gymId = Guid.NewGuid();
        var actorUserId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var student =
            CreateStudent(
                gymId,
                "Aluno Teste");

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        var before =
            DateTime.UtcNow;

        var result =
            await _service.CreateManualAsync(
                gymId,
                actorUserId,
                new CreateManualCheckInRequest
                {
                    RequestId = requestId,
                    StudentId = student.Id
                });

        var after =
            DateTime.UtcNow;

        Assert.NotNull(result);

        Assert.Equal(
            student.Id,
            result.StudentId);

        Assert.Equal(
            CheckInSource.Manual,
            result.Source);

        Assert.Equal(
            actorUserId,
            result.RecordedByUserId);

        Assert.Null(
            result.PhysicalAccessEventId);

        Assert.InRange(
            result.OccurredAt,
            before,
            after);

        await _checkInRepository
            .Received(1)
            .AddAsync(
                Arg.Is<CheckIn>(
                    checkIn =>
                        checkIn.GymId == gymId &&
                        checkIn.StudentId ==
                            student.Id &&
                        checkIn.RequestId ==
                            requestId &&
                        checkIn.Source ==
                            CheckInSource.Manual));
    }

    [Fact]
    public async Task CreateManualAsync_WhenRequestWasAlreadyProcessed_ShouldReturnExistingCheckIn()
    {
        var gymId = Guid.NewGuid();
        var actorUserId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var student =
            CreateStudent(
                gymId,
                "Aluno Teste");

        var existing =
            new CheckIn
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                StudentId = student.Id,
                RequestId = requestId,
                Source = CheckInSource.Manual,
                RecordedByUserId =
                    actorUserId,
                OccurredAt =
                    DateTime.UtcNow.AddMinutes(-1),
                CreatedAt =
                    DateTime.UtcNow.AddMinutes(-1)
            };

        _checkInRepository
            .GetByRequestIdAsync(
                gymId,
                requestId)
            .Returns(existing);

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        var result =
            await _service.CreateManualAsync(
                gymId,
                actorUserId,
                new CreateManualCheckInRequest
                {
                    RequestId = requestId,
                    StudentId = student.Id
                });

        Assert.NotNull(result);

        Assert.Equal(
            existing.Id,
            result.Id);

        await _checkInRepository
            .DidNotReceiveWithAnyArgs()
            .AddAsync(default!);
    }

    [Fact]
    public async Task CreateManualAsync_WhenRequestIdBelongsToAnotherCheckIn_ShouldRejectReuse()
    {
        var gymId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        _checkInRepository
            .GetByRequestIdAsync(
                gymId,
                requestId)
            .Returns(
                new CheckIn
                {
                    Id = Guid.NewGuid(),
                    GymId = gymId,
                    StudentId = Guid.NewGuid(),
                    RequestId = requestId,
                    Source =
                        CheckInSource.PhysicalAccess,
                    OccurredAt =
                        DateTime.UtcNow,
                    CreatedAt =
                        DateTime.UtcNow
                });

        await Assert.ThrowsAsync<
            InvalidOperationException>(
                () =>
                    _service.CreateManualAsync(
                        gymId,
                        Guid.NewGuid(),
                        new CreateManualCheckInRequest
                        {
                            RequestId =
                                requestId,
                            StudentId =
                                Guid.NewGuid()
                        }));

        await _checkInRepository
            .DidNotReceiveWithAnyArgs()
            .AddAsync(default!);
    }

    [Fact]
    public async Task CreateManualAsync_WhenStudentDoesNotExistInGym_ShouldReturnNull()
    {
        var gymId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        _studentRepository
            .GetByIdAndGymIdAsync(
                studentId,
                gymId)
            .Returns((Student?)null);

        var result =
            await _service.CreateManualAsync(
                gymId,
                Guid.NewGuid(),
                new CreateManualCheckInRequest
                {
                    RequestId =
                        Guid.NewGuid(),
                    StudentId =
                        studentId
                });

        Assert.Null(result);

        await _checkInRepository
            .DidNotReceiveWithAnyArgs()
            .AddAsync(default!);
    }

    [Fact]
    public async Task CreateManualAsync_WhenStudentIsArchived_ShouldRejectCheckIn()
    {
        var gymId = Guid.NewGuid();

        var student =
            CreateStudent(
                gymId,
                "Aluno Arquivado");

        student.ArchivedAt =
            DateTime.UtcNow.AddDays(-1);

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        await Assert.ThrowsAsync<
            InvalidOperationException>(
                () =>
                    _service.CreateManualAsync(
                        gymId,
                        Guid.NewGuid(),
                        new CreateManualCheckInRequest
                        {
                            RequestId =
                                Guid.NewGuid(),
                            StudentId =
                                student.Id
                        }));

        await _checkInRepository
            .DidNotReceiveWithAnyArgs()
            .AddAsync(default!);
    }

    [Fact]
    public async Task CreateFromPhysicalPassageAsync_WhenAllowedAccessExists_ShouldCreateCheckIn()
    {
        var gymId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var passageAt =
            DateTime.UtcNow;

        var student =
            CreateStudent(
                gymId,
                "Aluno Catraca");

        var accessEvent =
            new PhysicalAccessEvent
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                RequestId = requestId,
                StudentId = student.Id,
                Decision =
                    PhysicalAccessDecision.Allowed,
                Reason =
                    PhysicalAccessDecisionReason.Eligible,
                Source =
                    PhysicalAccessDecisionSource.Online,
                OccurredAt =
                    passageAt.AddSeconds(-2),
                ProcessedAt =
                    passageAt.AddSeconds(-1)
            };

        _physicalAccessEventRepository
            .GetByRequestIdAsync(
                gymId,
                requestId)
            .Returns(accessEvent);

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        var result =
            await _service
                .CreateFromPhysicalPassageAsync(
                    gymId,
                    new ConfirmPhysicalPassageRequest
                    {
                        RequestId = requestId,
                        OccurredAt = passageAt
                    });

        Assert.NotNull(result);

        Assert.Equal(
            CheckInSource.PhysicalAccess,
            result.Source);

        Assert.Equal(
            student.Id,
            result.StudentId);

        Assert.Equal(
            accessEvent.Id,
            result.PhysicalAccessEventId);

        Assert.Null(
            result.RecordedByUserId);

        Assert.Equal(
            passageAt,
            result.OccurredAt);

        await _checkInRepository
            .Received(1)
            .AddAsync(
                Arg.Is<CheckIn>(
                    checkIn =>
                        checkIn.GymId == gymId &&
                        checkIn.StudentId ==
                            student.Id &&
                        checkIn.RequestId ==
                            requestId &&
                        checkIn.Source ==
                            CheckInSource.PhysicalAccess &&
                        checkIn.PhysicalAccessEventId ==
                            accessEvent.Id &&
                        checkIn.OccurredAt ==
                            passageAt));
    }

    [Fact]
    public async Task CreateFromPhysicalPassageAsync_WhenAccessWasDenied_ShouldRejectCheckIn()
    {
        var gymId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        _physicalAccessEventRepository
            .GetByRequestIdAsync(
                gymId,
                requestId)
            .Returns(
                new PhysicalAccessEvent
                {
                    Id = Guid.NewGuid(),
                    GymId = gymId,
                    RequestId = requestId,
                    StudentId = Guid.NewGuid(),
                    Decision =
                        PhysicalAccessDecision.Denied,
                    Reason =
                        PhysicalAccessDecisionReason
                            .FinancialRestriction,
                    OccurredAt =
                        DateTime.UtcNow,
                    ProcessedAt =
                        DateTime.UtcNow
                });

        await Assert.ThrowsAsync<
            InvalidOperationException>(
                () =>
                    _service
                        .CreateFromPhysicalPassageAsync(
                            gymId,
                            new ConfirmPhysicalPassageRequest
                            {
                                RequestId =
                                    requestId,
                                OccurredAt =
                                    DateTime.UtcNow
                            }));

        await _checkInRepository
            .DidNotReceiveWithAnyArgs()
            .AddAsync(default!);
    }

    [Fact]
    public async Task CreateFromPhysicalPassageAsync_WhenAccessEventDoesNotExist_ShouldReturnNull()
    {
        var gymId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        _physicalAccessEventRepository
            .GetByRequestIdAsync(
                gymId,
                requestId)
            .Returns(
                (PhysicalAccessEvent?)null);

        var result =
            await _service
                .CreateFromPhysicalPassageAsync(
                    gymId,
                    new ConfirmPhysicalPassageRequest
                    {
                        RequestId =
                            requestId,
                        OccurredAt =
                            DateTime.UtcNow
                    });

        Assert.Null(result);

        await _checkInRepository
            .DidNotReceiveWithAnyArgs()
            .AddAsync(default!);
    }

    [Fact]
    public async Task CreateFromPhysicalPassageAsync_WhenAlreadyRegistered_ShouldBeIdempotent()
    {
        var gymId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var student =
            CreateStudent(
                gymId,
                "Aluno Catraca");

        var existing =
            new CheckIn
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                StudentId = student.Id,
                RequestId = requestId,
                Source =
                    CheckInSource.PhysicalAccess,
                PhysicalAccessEventId =
                    Guid.NewGuid(),
                OccurredAt =
                    DateTime.UtcNow.AddMinutes(-1),
                CreatedAt =
                    DateTime.UtcNow.AddMinutes(-1)
            };

        _checkInRepository
            .GetByRequestIdAsync(
                gymId,
                requestId)
            .Returns(existing);

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        var result =
            await _service
                .CreateFromPhysicalPassageAsync(
                    gymId,
                    new ConfirmPhysicalPassageRequest
                    {
                        RequestId =
                            requestId,
                        OccurredAt =
                            DateTime.UtcNow
                    });

        Assert.NotNull(result);

        Assert.Equal(
            existing.Id,
            result.Id);

        Assert.Equal(
            existing.OccurredAt,
            result.OccurredAt);

        await _physicalAccessEventRepository
            .DidNotReceiveWithAnyArgs()
            .GetByRequestIdAsync(
                default,
                default);

        await _checkInRepository
            .DidNotReceiveWithAnyArgs()
            .AddAsync(default!);
    }

    [Fact]
    public async Task GetHistoryAsync_WhenValid_ShouldReturnPagedHistory()
    {
        var gymId =
            Guid.NewGuid();

        var student =
            CreateStudent(
                gymId,
                "Aluno Frequente");

        var first =
            new CheckIn
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                StudentId = student.Id,
                RequestId = Guid.NewGuid(),
                Source =
                    CheckInSource.PhysicalAccess,
                PhysicalAccessEventId =
                    Guid.NewGuid(),
                OccurredAt =
                    new DateTime(
                        2026,
                        10,
                        10,
                        13,
                        30,
                        0,
                        DateTimeKind.Utc),
                CreatedAt =
                    DateTime.UtcNow,
                Student =
                    student
            };

        _gymTimeZoneProvider
            .GetTimeZone(gymId)
            .Returns(
                TimeZoneInfo.Utc);

        _checkInRepository
            .GetPagedAsync(
                gymId,
                student.Id,
                new DateTime(
                    2026,
                    10,
                    10,
                    0,
                    0,
                    0,
                    DateTimeKind.Utc),
                new DateTime(
                    2026,
                    10,
                    12,
                    0,
                    0,
                    0,
                    DateTimeKind.Utc),
                20,
                20)
            .Returns(
                Task.FromResult(
                    (
                        new List<CheckIn>
                        {
                            first
                        },
                        25
                    )));

        var result =
            await _service.GetHistoryAsync(
                gymId,
                student.Id,
                new DateOnly(
                    2026,
                    10,
                    10),
                new DateOnly(
                    2026,
                    10,
                    11),
                page: 2,
                pageSize: 20);

        Assert.Equal(
            2,
            result.Page);

        Assert.Equal(
            20,
            result.PageSize);

        Assert.Equal(
            25,
            result.TotalCount);

        Assert.Equal(
            2,
            result.TotalPages);

        var item =
            Assert.Single(
                result.Items);

        Assert.Equal(
            student.Id,
            item.StudentId);

        Assert.Equal(
            "Aluno Frequente",
            item.StudentName);

        Assert.Equal(
            CheckInSource.PhysicalAccess,
            item.Source);

        Assert.Equal(
            first.OccurredAt,
            item.OccurredAt);
    }

    [Fact]
    public async Task GetHistoryAsync_WhenDateRangeIsInvalid_ShouldThrow()
    {
        await Assert.ThrowsAsync<
            ArgumentException>(
                () =>
                    _service.GetHistoryAsync(
                        Guid.NewGuid(),
                        null,
                        new DateOnly(
                            2026,
                            10,
                            12),
                        new DateOnly(
                            2026,
                            10,
                            10),
                        page: 1,
                        pageSize: 20));

        await _checkInRepository
            .DidNotReceiveWithAnyArgs()
            .GetPagedAsync(
                default,
                default,
                default,
                default,
                default,
                default);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task GetHistoryAsync_WhenPaginationIsInvalid_ShouldThrow(
        int page,
        int pageSize)
    {
        await Assert.ThrowsAsync<
            ArgumentException>(
                () =>
                    _service.GetHistoryAsync(
                        Guid.NewGuid(),
                        null,
                        null,
                        null,
                        page,
                        pageSize));

        await _checkInRepository
            .DidNotReceiveWithAnyArgs()
            .GetPagedAsync(
                default,
                default,
                default,
                default,
                default,
                default);
    }
    private static Student CreateStudent(
        Guid gymId,
        string name)
    {
        var user =
            new User
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                Name = name,
                Email =
                    $"{Guid.NewGuid():N}@gymflow.dev",
                PasswordHash =
                    "stored-hash",
                Role =
                    UserRole.Student,
                IsActive = true,
                CreatedAt =
                    DateTime.UtcNow
            };

        return new Student
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CreatedAt =
                DateTime.UtcNow,
            User = user
        };
    }
}