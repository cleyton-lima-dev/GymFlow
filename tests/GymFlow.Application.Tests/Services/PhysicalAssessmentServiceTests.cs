using GymFlow.Application.DTOs.PhysicalAssessments;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Time;
using GymFlow.Application.Services;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using NSubstitute;
using GymFlow.Application.Exceptions;

namespace GymFlow.Application.Tests.Services;

public class PhysicalAssessmentServiceTests
{
    private readonly IPhysicalAssessmentRepository _physicalAssessmentRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IGymTimeZoneProvider _gymTimeZoneProvider;
    private readonly PhysicalAssessmentService _service;

    public PhysicalAssessmentServiceTests()
    {
        _physicalAssessmentRepository =
            Substitute.For<IPhysicalAssessmentRepository>();

        _studentRepository =
            Substitute.For<IStudentRepository>();

        _gymTimeZoneProvider =
            Substitute.For<IGymTimeZoneProvider>();

        _gymTimeZoneProvider
            .GetTimeZone(Arg.Any<Guid>())
            .Returns(TimeZoneInfo.Utc);

        _service = new PhysicalAssessmentService(
            _physicalAssessmentRepository,
            _studentRepository,
            _gymTimeZoneProvider);
    }

    [Fact]
    public async Task CreateAsync_WithValidData_ShouldCreateAssessment()
    {
        var gymId = Guid.NewGuid();
        var student = CreateStudent(gymId);

        var request = new CreatePhysicalAssessmentRequest
        {
            AssessmentDate =
                DateOnly.FromDateTime(DateTime.UtcNow),
            WeightKg = 82.4m,
            HeightCm = 178m,
            BodyFatPercentage = 18.2m,
            ChestCm = 102m,
            WaistCm = 86m,
            AbdomenCm = 88m,
            HipCm = 104m,
            RightArmCm = 36m,
            LeftArmCm = 35.5m,
            RightThighCm = 61m,
            LeftThighCm = 60.5m,
            RightCalfCm = 37m,
            LeftCalfCm = 36.5m,
            Notes = "  Avaliação inicial  "
        };

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        _physicalAssessmentRepository
            .ExistsForDateAsync(
                student.Id,
                gymId,
                request.AssessmentDate)
            .Returns(false);

        PhysicalAssessment? captured = null;

        _physicalAssessmentRepository
            .When(x => x.AddAsync(
                Arg.Any<PhysicalAssessment>()))
            .Do(call =>
                captured =
                    call.Arg<PhysicalAssessment>());

        var result = await _service.CreateAsync(
            student.Id,
            gymId,
            request);

        Assert.Equal(
            CreatePhysicalAssessmentResult.Success,
            result);

        Assert.NotNull(captured);
        Assert.NotEqual(Guid.Empty, captured.Id);

        Assert.Equal(
            student.Id,
            captured.StudentId);

        Assert.Equal(
            request.AssessmentDate,
            captured.AssessmentDate);

        Assert.Equal(82.4m, captured.WeightKg);
        Assert.Equal(178m, captured.HeightCm);
        Assert.Equal(18.2m, captured.BodyFatPercentage);

        Assert.Equal(
            "Avaliação inicial",
            captured.Notes);

        Assert.Equal(
            captured.CreatedAt,
            captured.UpdatedAt);

        await _physicalAssessmentRepository
            .Received(1)
            .AddAsync(captured);

        await _physicalAssessmentRepository
            .Received(1)
            .SaveChangesAsync();
    }

    [Fact]
    public async Task CreateAsync_WhenStudentDoesNotExist_ShouldReturnStudentNotFound()
    {
        var gymId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        _studentRepository
            .GetByIdAndGymIdAsync(
                studentId,
                gymId)
            .Returns((Student?)null);

        var result = await _service.CreateAsync(
            studentId,
            gymId,
            CreateValidRequest());

        Assert.Equal(
            CreatePhysicalAssessmentResult.StudentNotFound,
            result);

        await _physicalAssessmentRepository
            .DidNotReceive()
            .ExistsForDateAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<DateOnly>());

        await _physicalAssessmentRepository
            .DidNotReceive()
            .AddAsync(
                Arg.Any<PhysicalAssessment>());
    }

    [Fact]
    public async Task CreateAsync_WhenAssessmentAlreadyExistsForDate_ShouldReturnConflictResult()
    {
        var gymId = Guid.NewGuid();
        var student = CreateStudent(gymId);
        var request = CreateValidRequest();

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        _physicalAssessmentRepository
            .ExistsForDateAsync(
                student.Id,
                gymId,
                request.AssessmentDate)
            .Returns(true);

        var result = await _service.CreateAsync(
            student.Id,
            gymId,
            request);

        Assert.Equal(
            CreatePhysicalAssessmentResult
                .AssessmentAlreadyExistsForDate,
            result);

        await _physicalAssessmentRepository
            .DidNotReceive()
            .AddAsync(
                Arg.Any<PhysicalAssessment>());

        await _physicalAssessmentRepository
            .DidNotReceive()
            .SaveChangesAsync();
    }

    [Fact]
    public async Task CreateAsync_WithFutureAssessmentDate_ShouldThrowArgumentException()
    {
        var gymId = Guid.NewGuid();
        var student = CreateStudent(gymId);

        var request = CreateValidRequest();

        request.AssessmentDate =
            DateOnly.FromDateTime(
                DateTime.UtcNow.AddDays(1));

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(
                student.Id,
                gymId,
                request));

        await _physicalAssessmentRepository
            .DidNotReceive()
            .AddAsync(
                Arg.Any<PhysicalAssessment>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateAsync_WithInvalidWeight_ShouldThrowArgumentException(
        int weight)
    {
        var gymId = Guid.NewGuid();
        var student = CreateStudent(gymId);

        var request = CreateValidRequest();
        request.WeightKg = weight;

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(
                student.Id,
                gymId,
                request));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateAsync_WithInvalidHeight_ShouldThrowArgumentException(
        int height)
    {
        var gymId = Guid.NewGuid();
        var student = CreateStudent(gymId);

        var request = CreateValidRequest();
        request.HeightCm = height;

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(
                student.Id,
                gymId,
                request));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task CreateAsync_WithInvalidBodyFatPercentage_ShouldThrowArgumentException(
        int bodyFat)
    {
        var gymId = Guid.NewGuid();
        var student = CreateStudent(gymId);

        var request = CreateValidRequest();
        request.BodyFatPercentage = bodyFat;

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(
                student.Id,
                gymId,
                request));
    }

    [Fact]
    public async Task CreateAsync_WithNotesLongerThan500Characters_ShouldThrowArgumentException()
    {
        var gymId = Guid.NewGuid();
        var student = CreateStudent(gymId);

        var request = CreateValidRequest();
        request.Notes = new string('a', 501);

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(
                student.Id,
                gymId,
                request));
    }

    [Fact]
    public async Task CreateAsync_WithDefaultAssessmentDate_ShouldRejectRequest()
    {
        var gymId = Guid.NewGuid();
        var student = CreateStudent(gymId);

        var request = CreateValidRequest();
        request.AssessmentDate = default;

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(
                student.Id,
                gymId,
                request));

        await _physicalAssessmentRepository
            .DidNotReceive()
            .AddAsync(
                Arg.Any<PhysicalAssessment>());
    }

    [Fact]
    public async Task GetLatestAsync_WhenThereIsNoAssessment_ShouldReturnNull()
    {
        var gymId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        _physicalAssessmentRepository
            .GetLatestByStudentAsync(
                studentId,
                gymId)
            .Returns((PhysicalAssessment?)null);

        var result = await _service.GetLatestAsync(
            studentId,
            gymId);

        Assert.Null(result);

        await _physicalAssessmentRepository
            .Received(1)
            .GetLatestByStudentAsync(
                studentId,
                gymId);
    }

    [Fact]
    public async Task GetLatestAsync_WhenAssessmentIsToday_ShouldReturnNextAssessmentInTwoMonthsAndNotDue()
    {
        var gymId = Guid.NewGuid();

        var today =
            DateOnly.FromDateTime(DateTime.UtcNow);

        var assessment =
            CreateAssessment(
                Guid.NewGuid(),
                today);

        _physicalAssessmentRepository
            .GetLatestByStudentAsync(
                assessment.StudentId,
                gymId)
            .Returns(assessment);

        var result = await _service.GetLatestAsync(
            assessment.StudentId,
            gymId);

        Assert.NotNull(result);

        Assert.Equal(
            today,
            result.AssessmentDate);

        Assert.Equal(
            today.AddMonths(2),
            result.NextAssessmentDate);

        Assert.False(
            result.IsReassessmentDue);
    }

    [Fact]
    public async Task GetLatestAsync_WhenNextAssessmentDateHasPassed_ShouldReturnDue()
    {
        var gymId = Guid.NewGuid();

        var assessment =
            CreateAssessment(
                Guid.NewGuid(),
                new DateOnly(2020, 1, 15));

        _physicalAssessmentRepository
            .GetLatestByStudentAsync(
                assessment.StudentId,
                gymId)
            .Returns(assessment);

        var result = await _service.GetLatestAsync(
            assessment.StudentId,
            gymId);

        Assert.NotNull(result);

        Assert.Equal(
            new DateOnly(2020, 3, 15),
            result.NextAssessmentDate);

        Assert.True(
            result.IsReassessmentDue);
    }

    [Theory]
    [InlineData(
    2023, 12, 31,
    2024, 2, 29)]
    [InlineData(
    2024, 12, 31,
    2025, 2, 28)]
    public async Task GetLatestAsync_ShouldUseDateOnlyAddMonthsForEndOfMonthDates(
    int year,
    int month,
    int day,
    int expectedYear,
    int expectedMonth,
    int expectedDay)
    {
        var gymId = Guid.NewGuid();

        var assessmentDate =
            new DateOnly(
                year,
                month,
                day);

        var assessment =
            CreateAssessment(
                Guid.NewGuid(),
                assessmentDate);

        _physicalAssessmentRepository
            .GetLatestByStudentAsync(
                assessment.StudentId,
                gymId)
            .Returns(assessment);

        var result = await _service.GetLatestAsync(
            assessment.StudentId,
            gymId);

        Assert.NotNull(result);

        Assert.Equal(
            new DateOnly(
                expectedYear,
                expectedMonth,
                expectedDay),
            result.NextAssessmentDate);
    }

    [Fact]
    public async Task GetByIdAsync_WhenAssessmentExists_ShouldReturnMappedAssessment()
    {
        var gymId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var assessment =
            CreateAssessment(
                studentId,
                new DateOnly(2026, 1, 10));

        _physicalAssessmentRepository
            .GetByIdAsync(
                assessment.Id,
                studentId,
                gymId)
            .Returns(assessment);

        var result = await _service.GetByIdAsync(
            assessment.Id,
            studentId,
            gymId);

        Assert.NotNull(result);

        Assert.Equal(
            assessment.Id,
            result.Id);

        Assert.Equal(
            studentId,
            result.StudentId);

        Assert.Equal(
            assessment.AssessmentDate,
            result.AssessmentDate);

        Assert.Equal(
            assessment.WeightKg,
            result.WeightKg);

        Assert.Equal(
            assessment.HeightCm,
            result.HeightCm);

        Assert.Equal(
            assessment.Notes,
            result.Notes);

        await _physicalAssessmentRepository
            .Received(1)
            .GetByIdAsync(
                assessment.Id,
                studentId,
                gymId);
    }

    [Fact]
    public async Task GetByIdAsync_WhenRepositoryDoesNotFindAssessment_ShouldReturnNull()
    {
        var assessmentId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var gymId = Guid.NewGuid();

        _physicalAssessmentRepository
            .GetByIdAsync(
                assessmentId,
                studentId,
                gymId)
            .Returns((PhysicalAssessment?)null);

        var result = await _service.GetByIdAsync(
            assessmentId,
            studentId,
            gymId);

        Assert.Null(result);

        await _physicalAssessmentRepository
            .Received(1)
            .GetByIdAsync(
                assessmentId,
                studentId,
                gymId);
    }

    [Fact]
    public async Task GetHistoryAsync_WithValidParameters_ShouldReturnPagedHistory()
    {
        var gymId = Guid.NewGuid();
        var student = CreateStudent(gymId);
        var studentId = student.Id;

        _studentRepository
            .GetByIdAndGymIdAsync(
                studentId,
                gymId)
            .Returns(student);

        var assessment1 = CreateAssessment(
            studentId,
            new DateOnly(2026, 6, 10));

        var assessment2 = CreateAssessment(
            studentId,
            new DateOnly(2026, 4, 10));

        _physicalAssessmentRepository
            .GetHistoryByStudentAsync(
                studentId,
                gymId,
                2,
                20)
            .Returns(
                new List<PhysicalAssessment>
                {
                assessment1,
                assessment2
                });

        _physicalAssessmentRepository
            .CountByStudentAsync(
                studentId,
                gymId)
            .Returns(25);

        var result = await _service.GetHistoryAsync(
            studentId,
            gymId,
            page: 2,
            pageSize: 20);

        Assert.Equal(2, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(25, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(2, result.Items.Count);

        Assert.Equal(
            assessment1.Id,
            result.Items[0].Id);

        Assert.Equal(
            assessment1.AssessmentDate,
            result.Items[0].AssessmentDate);

        Assert.Equal(
            assessment1.AssessmentDate.AddMonths(2),
            result.Items[0].NextAssessmentDate);

        Assert.Equal(
            assessment2.Id,
            result.Items[1].Id);

        await _physicalAssessmentRepository
            .Received(1)
            .GetHistoryByStudentAsync(
                studentId,
                gymId,
                2,
                20);

        await _physicalAssessmentRepository
            .Received(1)
            .CountByStudentAsync(
                studentId,
                gymId);
    }

    [Fact]
    public async Task GetHistoryAsync_WithPageLessThanOne_ShouldThrowArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.GetHistoryAsync(
                Guid.NewGuid(),
                Guid.NewGuid(),
                page: 0,
                pageSize: 20));

        await _physicalAssessmentRepository
            .DidNotReceive()
            .GetHistoryByStudentAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<int>(),
                Arg.Any<int>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task GetHistoryAsync_WithInvalidPageSize_ShouldThrowArgumentException(
    int pageSize)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.GetHistoryAsync(
                Guid.NewGuid(),
                Guid.NewGuid(),
                page: 1,
                pageSize: pageSize));

        await _physicalAssessmentRepository
            .DidNotReceive()
            .GetHistoryByStudentAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<int>(),
                Arg.Any<int>());
    }

    [Fact]
    public async Task GetLatestForUserAsync_ShouldResolveStudentUsingUserIdAndGymId()
    {
        var gymId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var student = CreateStudent(gymId);
        student.UserId = userId;
        student.User.Id = userId;

        var assessment = CreateAssessment(
            student.Id,
            new DateOnly(2026, 6, 10));

        _studentRepository
            .GetByUserIdAndGymIdAsync(
                userId,
                gymId)
            .Returns(student);

        _physicalAssessmentRepository
            .GetLatestByStudentAsync(
                student.Id,
                gymId)
            .Returns(assessment);

        var result = await _service.GetLatestForUserAsync(
            userId,
            gymId);

        Assert.NotNull(result);
        Assert.Equal(
            assessment.Id,
            result.Id);

        await _studentRepository
            .Received(1)
            .GetByUserIdAndGymIdAsync(
                userId,
                gymId);

        await _physicalAssessmentRepository
            .Received(1)
            .GetLatestByStudentAsync(
                student.Id,
                gymId);
    }

    [Fact]
    public async Task GetLatestForUserAsync_WhenStudentDoesNotExist_ShouldReturnNull()
    {
        var gymId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _studentRepository
            .GetByUserIdAndGymIdAsync(
                userId,
                gymId)
            .Returns((Student?)null);

        var result = await _service.GetLatestForUserAsync(
            userId,
            gymId);

        Assert.Null(result);

        await _physicalAssessmentRepository
            .DidNotReceive()
            .GetLatestByStudentAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid>());
    }

    [Fact]
    public async Task GetHistoryForUserAsync_ShouldReturnHistoryForResolvedStudent()
    {
        var gymId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var student = CreateStudent(gymId);
        student.UserId = userId;
        student.User.Id = userId;

        _studentRepository
            .GetByUserIdAndGymIdAsync(
                userId,
                gymId)
            .Returns(student);

        _physicalAssessmentRepository
            .GetHistoryByStudentAsync(
                student.Id,
                gymId,
                1,
                20)
            .Returns(
                new List<PhysicalAssessment>());

        _physicalAssessmentRepository
            .CountByStudentAsync(
                student.Id,
                gymId)
            .Returns(0);

        var result = await _service.GetHistoryForUserAsync(
            userId,
            gymId,
            page: 1,
            pageSize: 20);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);

        await _studentRepository
            .Received(1)
            .GetByUserIdAndGymIdAsync(
                userId,
                gymId);

        await _physicalAssessmentRepository
            .Received(1)
            .GetHistoryByStudentAsync(
                student.Id,
                gymId,
                1,
                20);
    }

    [Fact]
    public async Task GetHistoryForUserAsync_WhenStudentDoesNotExist_ShouldThrowKeyNotFoundException()
    {
        var gymId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _studentRepository
            .GetByUserIdAndGymIdAsync(
                userId,
                gymId)
            .Returns((Student?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.GetHistoryForUserAsync(
                userId,
                gymId,
                page: 1,
                pageSize: 20));

        await _physicalAssessmentRepository
            .DidNotReceive()
            .GetHistoryByStudentAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<int>(),
                Arg.Any<int>());
    }

    [Fact]
    public async Task GetByIdForUserAsync_ShouldReturnAssessmentForResolvedStudent()
    {
        var gymId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var student = CreateStudent(gymId);
        student.UserId = userId;
        student.User.Id = userId;

        var assessment = CreateAssessment(
            student.Id,
            new DateOnly(2026, 6, 10));

        _studentRepository
            .GetByUserIdAndGymIdAsync(
                userId,
                gymId)
            .Returns(student);

        _physicalAssessmentRepository
            .GetByIdAsync(
                assessment.Id,
                student.Id,
                gymId)
            .Returns(assessment);

        var result = await _service.GetByIdForUserAsync(
            assessment.Id,
            userId,
            gymId);

        Assert.NotNull(result);
        Assert.Equal(
            assessment.Id,
            result.Id);

        await _physicalAssessmentRepository
            .Received(1)
            .GetByIdAsync(
                assessment.Id,
                student.Id,
                gymId);
    }

    [Fact]
    public async Task GetByIdForUserAsync_WhenStudentDoesNotExist_ShouldReturnNull()
    {
        var gymId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _studentRepository
            .GetByUserIdAndGymIdAsync(
                userId,
                gymId)
            .Returns((Student?)null);

        var result = await _service.GetByIdForUserAsync(
            Guid.NewGuid(),
            userId,
            gymId);

        Assert.Null(result);

        await _physicalAssessmentRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<Guid>());
    }

    [Theory]
    [InlineData(nameof(CreatePhysicalAssessmentRequest.ChestCm))]
    [InlineData(nameof(CreatePhysicalAssessmentRequest.WaistCm))]
    [InlineData(nameof(CreatePhysicalAssessmentRequest.AbdomenCm))]
    [InlineData(nameof(CreatePhysicalAssessmentRequest.HipCm))]
    [InlineData(nameof(CreatePhysicalAssessmentRequest.RightArmCm))]
    [InlineData(nameof(CreatePhysicalAssessmentRequest.LeftArmCm))]
    [InlineData(nameof(CreatePhysicalAssessmentRequest.RightThighCm))]
    [InlineData(nameof(CreatePhysicalAssessmentRequest.LeftThighCm))]
    [InlineData(nameof(CreatePhysicalAssessmentRequest.RightCalfCm))]
    [InlineData(nameof(CreatePhysicalAssessmentRequest.LeftCalfCm))]
    public async Task CreateAsync_WithZeroOptionalMeasurement_ShouldThrowArgumentException(
    string field)
    {
        var gymId = Guid.NewGuid();
        var student = CreateStudent(gymId);

        var request = CreateValidRequest();

        SetNumericField(
            request,
            field,
            0m);

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(
                student.Id,
                gymId,
                request));

        await _physicalAssessmentRepository
            .DidNotReceive()
            .AddAsync(
                Arg.Any<PhysicalAssessment>());
    }

    [Theory]
    [InlineData(nameof(CreatePhysicalAssessmentRequest.WeightKg))]
    [InlineData(nameof(CreatePhysicalAssessmentRequest.HeightCm))]
    [InlineData(nameof(CreatePhysicalAssessmentRequest.ChestCm))]
    [InlineData(nameof(CreatePhysicalAssessmentRequest.WaistCm))]
    [InlineData(nameof(CreatePhysicalAssessmentRequest.AbdomenCm))]
    [InlineData(nameof(CreatePhysicalAssessmentRequest.HipCm))]
    [InlineData(nameof(CreatePhysicalAssessmentRequest.RightArmCm))]
    [InlineData(nameof(CreatePhysicalAssessmentRequest.LeftArmCm))]
    [InlineData(nameof(CreatePhysicalAssessmentRequest.RightThighCm))]
    [InlineData(nameof(CreatePhysicalAssessmentRequest.LeftThighCm))]
    [InlineData(nameof(CreatePhysicalAssessmentRequest.RightCalfCm))]
    [InlineData(nameof(CreatePhysicalAssessmentRequest.LeftCalfCm))]
    public async Task CreateAsync_WithValueOutsideDatabasePrecision_ShouldThrowArgumentException(
    string field)
    {
        var gymId = Guid.NewGuid();
        var student = CreateStudent(gymId);

        var request = CreateValidRequest();

        SetNumericField(
            request,
            field,
            1000m);

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(
                student.Id,
                gymId,
                request));

        await _physicalAssessmentRepository
            .DidNotReceive()
            .AddAsync(
                Arg.Any<PhysicalAssessment>());

        await _physicalAssessmentRepository
            .DidNotReceive()
            .SaveChangesAsync();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public async Task CreateAsync_WithBodyFatAtAllowedBoundary_ShouldCreateAssessment(
    int bodyFat)
    {
        var gymId = Guid.NewGuid();
        var student = CreateStudent(gymId);

        var request = CreateValidRequest();

        request.BodyFatPercentage =
            bodyFat;

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        _physicalAssessmentRepository
            .ExistsForDateAsync(
                student.Id,
                gymId,
                request.AssessmentDate)
            .Returns(false);

        var result = await _service.CreateAsync(
            student.Id,
            gymId,
            request);

        Assert.Equal(
            CreatePhysicalAssessmentResult.Success,
            result);

        await _physicalAssessmentRepository
            .Received(1)
            .AddAsync(
                Arg.Is<PhysicalAssessment>(
                    assessment =>
                        assessment.BodyFatPercentage ==
                        bodyFat));

        await _physicalAssessmentRepository
            .Received(1)
            .SaveChangesAsync();
    }

    [Fact]
    public async Task CreateAsync_WithAllOptionalMeasurementsNull_ShouldCreateAssessment()
    {
        var gymId = Guid.NewGuid();
        var student = CreateStudent(gymId);

        var request = CreateValidRequest();

        request.BodyFatPercentage = null;

        request.ChestCm = null;
        request.WaistCm = null;
        request.AbdomenCm = null;
        request.HipCm = null;

        request.RightArmCm = null;
        request.LeftArmCm = null;

        request.RightThighCm = null;
        request.LeftThighCm = null;

        request.RightCalfCm = null;
        request.LeftCalfCm = null;

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        _physicalAssessmentRepository
            .ExistsForDateAsync(
                student.Id,
                gymId,
                request.AssessmentDate)
            .Returns(false);

        PhysicalAssessment? captured = null;

        _physicalAssessmentRepository
            .When(x => x.AddAsync(
                Arg.Any<PhysicalAssessment>()))
            .Do(call =>
                captured =
                    call.Arg<PhysicalAssessment>());

        var result = await _service.CreateAsync(
            student.Id,
            gymId,
            request);

        Assert.Equal(
            CreatePhysicalAssessmentResult.Success,
            result);

        Assert.NotNull(captured);

        Assert.Null(captured.BodyFatPercentage);

        Assert.Null(captured.ChestCm);
        Assert.Null(captured.WaistCm);
        Assert.Null(captured.AbdomenCm);
        Assert.Null(captured.HipCm);

        Assert.Null(captured.RightArmCm);
        Assert.Null(captured.LeftArmCm);

        Assert.Null(captured.RightThighCm);
        Assert.Null(captured.LeftThighCm);

        Assert.Null(captured.RightCalfCm);
        Assert.Null(captured.LeftCalfCm);

        await _physicalAssessmentRepository
            .Received(1)
            .SaveChangesAsync();
    }

    [Theory]
    [InlineData(nameof(CreatePhysicalAssessmentRequest.WeightKg))]
    [InlineData(nameof(CreatePhysicalAssessmentRequest.ChestCm))]
    public async Task CreateAsync_WithMaximumDatabaseDecimalValue_ShouldCreateAssessment(
    string field)
    {
        var gymId = Guid.NewGuid();
        var student = CreateStudent(gymId);

        var request = CreateValidRequest();

        SetNumericField(
            request,
            field,
            999.99m);

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        _physicalAssessmentRepository
            .ExistsForDateAsync(
                student.Id,
                gymId,
                request.AssessmentDate)
            .Returns(false);

        PhysicalAssessment? captured = null;

        _physicalAssessmentRepository
            .When(x => x.AddAsync(
                Arg.Any<PhysicalAssessment>()))
            .Do(call =>
                captured =
                    call.Arg<PhysicalAssessment>());

        var result = await _service.CreateAsync(
            student.Id,
            gymId,
            request);

        Assert.Equal(
            CreatePhysicalAssessmentResult.Success,
            result);

        Assert.NotNull(captured);

        if (field ==
            nameof(CreatePhysicalAssessmentRequest.WeightKg))
        {
            Assert.Equal(
                999.99m,
                captured.WeightKg);
        }
        else
        {
            Assert.Equal(
                999.99m,
                captured.ChestCm);
        }

        await _physicalAssessmentRepository
            .Received(1)
            .AddAsync(
                Arg.Any<PhysicalAssessment>());

        await _physicalAssessmentRepository
            .Received(1)
            .SaveChangesAsync();
    }

    [Fact]
    public async Task CreateAsync_WhenDatabaseReportsConcurrentDateConflict_ShouldReturnConflictResult()
    {
        var gymId = Guid.NewGuid();
        var student = CreateStudent(gymId);
        var request = CreateValidRequest();

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        _physicalAssessmentRepository
            .ExistsForDateAsync(
                student.Id,
                gymId,
                request.AssessmentDate)
            .Returns(false);

        _physicalAssessmentRepository
            .SaveChangesAsync()
            .Returns(Task.FromException(
                new PhysicalAssessmentDateConflictException(
                    new Exception("Unique constraint."))));

        var result = await _service.CreateAsync(
            student.Id,
            gymId,
            request);

        Assert.Equal(
            CreatePhysicalAssessmentResult
                .AssessmentAlreadyExistsForDate,
            result);
    }

    [Fact]
    public async Task CreateAsync_WhenSaveFailsForUnrelatedReason_ShouldPropagateException()
    {
        var gymId = Guid.NewGuid();
        var student = CreateStudent(gymId);
        var request = CreateValidRequest();

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        _physicalAssessmentRepository
            .ExistsForDateAsync(
                student.Id,
                gymId,
                request.AssessmentDate)
            .Returns(false);

        _physicalAssessmentRepository
            .SaveChangesAsync()
            .Returns(Task.FromException(
                new InvalidOperationException(
                    "Falha não relacionada.")));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateAsync(
                student.Id,
                gymId,
                request));
    }

    [Fact]
    public async Task GetHistoryAsync_WhenStudentDoesNotExistInGym_ShouldThrowKeyNotFoundException()
    {
        var gymId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        _studentRepository
            .GetByIdAndGymIdAsync(
                studentId,
                gymId)
            .Returns((Student?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.GetHistoryAsync(
                studentId,
                gymId,
                page: 1,
                pageSize: 20));

        await _studentRepository
            .Received(1)
            .GetByIdAndGymIdAsync(
                studentId,
                gymId);

        await _physicalAssessmentRepository
            .DidNotReceive()
            .GetHistoryByStudentAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<int>(),
                Arg.Any<int>());

        await _physicalAssessmentRepository
            .DidNotReceive()
            .CountByStudentAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid>());
    }

    [Fact]
    public async Task UpdateAsync_WithValidData_ShouldUpdateAssessmentWithoutChangingIdentityOrDate()
    {
        var gymId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var assessment = CreateAssessment(
            studentId,
            new DateOnly(2026, 9, 4));

        var originalId = assessment.Id;
        var originalStudentId = assessment.StudentId;
        var originalDate = assessment.AssessmentDate;
        var originalCreatedAt = assessment.CreatedAt;

        assessment.UpdatedAt = DateTime.UtcNow.AddDays(-1);

        _physicalAssessmentRepository
            .GetByIdForUpdateAsync(
                assessment.Id,
                studentId,
                gymId)
            .Returns(assessment);

        var request = CreateValidUpdateRequest();
        request.WeightKg = 82.5m;
        request.WaistCm = 83m;
        request.Notes = "  Corrigido  ";

        var result = await _service.UpdateAsync(
            assessment.Id,
            studentId,
            gymId,
            request);

        Assert.True(result);

        Assert.Equal(originalId, assessment.Id);
        Assert.Equal(originalStudentId, assessment.StudentId);
        Assert.Equal(originalDate, assessment.AssessmentDate);
        Assert.Equal(originalCreatedAt, assessment.CreatedAt);

        Assert.Equal(82.5m, assessment.WeightKg);
        Assert.Equal(83m, assessment.WaistCm);
        Assert.Equal("Corrigido", assessment.Notes);

        Assert.True(
            assessment.UpdatedAt >
            originalCreatedAt);

        await _physicalAssessmentRepository
            .Received(1)
            .SaveChangesAsync();
    }

    [Fact]
    public async Task UpdateAsync_WhenAssessmentDoesNotExist_ShouldReturnFalse()
    {
        var assessmentId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var gymId = Guid.NewGuid();

        _physicalAssessmentRepository
            .GetByIdForUpdateAsync(
                assessmentId,
                studentId,
                gymId)
            .Returns((PhysicalAssessment?)null);

        var result = await _service.UpdateAsync(
            assessmentId,
            studentId,
            gymId,
            CreateValidUpdateRequest());

        Assert.False(result);

        await _physicalAssessmentRepository
            .DidNotReceive()
            .SaveChangesAsync();
    }

    [Fact]
    public async Task UpdateAsync_WithInvalidWeight_ShouldThrowArgumentException()
    {
        var gymId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var assessment = CreateAssessment(
            studentId,
            new DateOnly(2026, 9, 4));

        _physicalAssessmentRepository
            .GetByIdForUpdateAsync(
                assessment.Id,
                studentId,
                gymId)
            .Returns(assessment);

        var request = CreateValidUpdateRequest();
        request.WeightKg = 0;

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateAsync(
                assessment.Id,
                studentId,
                gymId,
                request));

        await _physicalAssessmentRepository
            .DidNotReceive()
            .SaveChangesAsync();
    }

    [Fact]
    public async Task UpdateAsync_WithNotesLongerThan500Characters_ShouldThrowArgumentException()
    {
        var gymId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var assessment = CreateAssessment(
            studentId,
            new DateOnly(2026, 9, 4));

        _physicalAssessmentRepository
            .GetByIdForUpdateAsync(
                assessment.Id,
                studentId,
                gymId)
            .Returns(assessment);

        var request = CreateValidUpdateRequest();
        request.Notes = new string('a', 501);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateAsync(
                assessment.Id,
                studentId,
                gymId,
                request));

        await _physicalAssessmentRepository
            .DidNotReceive()
            .SaveChangesAsync();
    }

    private static UpdatePhysicalAssessmentRequest CreateValidUpdateRequest()
    {
        return new UpdatePhysicalAssessmentRequest
        {
            WeightKg = 80m,
            HeightCm = 175m,
            BodyFatPercentage = 18m,
            ChestCm = 100m,
            WaistCm = 85m,
            AbdomenCm = 87m,
            HipCm = 100m,
            RightArmCm = 35m,
            LeftArmCm = 35m,
            RightThighCm = 60m,
            LeftThighCm = 60m,
            RightCalfCm = 37m,
            LeftCalfCm = 37m,
            Notes = "Observação"
        };
    }

    private static void SetNumericField(
    CreatePhysicalAssessmentRequest request,
    string field,
    decimal value)
    {
        switch (field)
        {
            case nameof(CreatePhysicalAssessmentRequest.WeightKg):
                request.WeightKg = value;
                break;

            case nameof(CreatePhysicalAssessmentRequest.HeightCm):
                request.HeightCm = value;
                break;

            case nameof(CreatePhysicalAssessmentRequest.ChestCm):
                request.ChestCm = value;
                break;

            case nameof(CreatePhysicalAssessmentRequest.WaistCm):
                request.WaistCm = value;
                break;

            case nameof(CreatePhysicalAssessmentRequest.AbdomenCm):
                request.AbdomenCm = value;
                break;

            case nameof(CreatePhysicalAssessmentRequest.HipCm):
                request.HipCm = value;
                break;

            case nameof(CreatePhysicalAssessmentRequest.RightArmCm):
                request.RightArmCm = value;
                break;

            case nameof(CreatePhysicalAssessmentRequest.LeftArmCm):
                request.LeftArmCm = value;
                break;

            case nameof(CreatePhysicalAssessmentRequest.RightThighCm):
                request.RightThighCm = value;
                break;

            case nameof(CreatePhysicalAssessmentRequest.LeftThighCm):
                request.LeftThighCm = value;
                break;

            case nameof(CreatePhysicalAssessmentRequest.RightCalfCm):
                request.RightCalfCm = value;
                break;

            case nameof(CreatePhysicalAssessmentRequest.LeftCalfCm):
                request.LeftCalfCm = value;
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(field),
                    field,
                    "Campo numérico desconhecido.");
        }
    }
    private static PhysicalAssessment CreateAssessment(
    Guid studentId,
    DateOnly assessmentDate)
    {
        return new PhysicalAssessment
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            AssessmentDate = assessmentDate,

            WeightKg = 80m,
            HeightCm = 175m,
            BodyFatPercentage = 18m,

            ChestCm = 100m,
            WaistCm = 85m,
            AbdomenCm = 87m,
            HipCm = 100m,

            RightArmCm = 35m,
            LeftArmCm = 35m,

            RightThighCm = 60m,
            LeftThighCm = 60m,

            RightCalfCm = 37m,
            LeftCalfCm = 37m,

            Notes = "Observação",

            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static CreatePhysicalAssessmentRequest CreateValidRequest()
    {
        return new CreatePhysicalAssessmentRequest
        {
            AssessmentDate =
                DateOnly.FromDateTime(DateTime.UtcNow),

            WeightKg = 80m,
            HeightCm = 175m,
            BodyFatPercentage = 18m,
            ChestCm = 100m,
            WaistCm = 85m,
            AbdomenCm = 87m,
            HipCm = 100m,
            RightArmCm = 35m,
            LeftArmCm = 35m,
            RightThighCm = 60m,
            LeftThighCm = 60m,
            RightCalfCm = 37m,
            LeftCalfCm = 37m,
            Notes = "Observação"
        };
    }

    private static Student CreateStudent(Guid gymId)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            Name = "Aluno Teste",
            Email = "aluno@gymflow.dev",
            PasswordHash = "stored-hash",
            Role = UserRole.Student,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        return new Student
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            CreatedAt = DateTime.UtcNow
        };
    }
}
