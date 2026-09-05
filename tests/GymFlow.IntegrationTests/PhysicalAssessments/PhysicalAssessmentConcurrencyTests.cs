using GymFlow.Application.DTOs.PhysicalAssessments;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Time;
using GymFlow.Application.Services;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using GymFlow.Infrastructure.Data;
using GymFlow.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.IntegrationTests.PhysicalAssessments;

public class PhysicalAssessmentConcurrencyTests
{
    [Fact]
    public async Task CreateAsync_WhenAnotherRequestCreatesSameDateAfterPreCheck_ShouldReturnConflict()
    {
        await using var connection =
            new SqliteConnection("Data Source=:memory:");

        await connection.OpenAsync();

        var options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

        await using (var setupContext =
            new AppDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync();

            var gymId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var studentId = Guid.NewGuid();

            setupContext.Users.Add(
                new User
                {
                    Id = userId,
                    GymId = gymId,
                    Name = "Aluno Teste",
                    Email = "aluno-concorrencia@gymflow.dev",
                    PasswordHash = "hash",
                    Role = UserRole.Student,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });

            setupContext.Students.Add(
                new Student
                {
                    Id = studentId,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                });

            await setupContext.SaveChangesAsync();

            await ExecuteRaceScenario(
                options,
                gymId,
                studentId);
        }
    }

    private static async Task ExecuteRaceScenario(
        DbContextOptions<AppDbContext> options,
        Guid gymId,
        Guid studentId)
    {
        await using var primaryContext =
            new AppDbContext(options);

        var realAssessmentRepository =
            new PhysicalAssessmentRepository(
                primaryContext);

        var raceRepository =
            new RaceInjectingPhysicalAssessmentRepository(
                realAssessmentRepository,
                options);

        var studentRepository =
            new StudentRepository(primaryContext);

        var service =
            new PhysicalAssessmentService(
                raceRepository,
                studentRepository,
                new UtcGymTimeZoneProvider());

        var assessmentDate =
            DateOnly.FromDateTime(DateTime.UtcNow);

        var request =
            new CreatePhysicalAssessmentRequest
            {
                AssessmentDate = assessmentDate,
                WeightKg = 80m,
                HeightCm = 175m,
                BodyFatPercentage = 18m
            };

        var result = await service.CreateAsync(
            studentId,
            gymId,
            request);

        Assert.Equal(
            CreatePhysicalAssessmentResult
                .AssessmentAlreadyExistsForDate,
            result);

        await using var verificationContext =
            new AppDbContext(options);

        var persistedCount =
            await verificationContext
                .PhysicalAssessments
                .CountAsync(x =>
                    x.StudentId == studentId &&
                    x.AssessmentDate == assessmentDate);

        Assert.Equal(
            1,
            persistedCount);
    }

    private sealed class RaceInjectingPhysicalAssessmentRepository
        : IPhysicalAssessmentRepository
    {
        private readonly IPhysicalAssessmentRepository _inner;
        private readonly DbContextOptions<AppDbContext> _options;

        private PhysicalAssessment? _pendingAssessment;
        private bool _raceInjected;

        public RaceInjectingPhysicalAssessmentRepository(
            IPhysicalAssessmentRepository inner,
            DbContextOptions<AppDbContext> options)
        {
            _inner = inner;
            _options = options;
        }

        public async Task AddAsync(
            PhysicalAssessment assessment)
        {
            _pendingAssessment = assessment;

            await _inner.AddAsync(assessment);
        }

        public Task<PhysicalAssessment?> GetByIdAsync(
            Guid assessmentId,
            Guid studentId,
            Guid gymId)
        {
            return _inner.GetByIdAsync(
                assessmentId,
                studentId,
                gymId);
        }

        public Task<PhysicalAssessment?> GetByIdForUpdateAsync(
             Guid assessmentId,
             Guid studentId,
             Guid gymId)
        {
            return _inner.GetByIdForUpdateAsync(
                assessmentId,
                studentId,
                gymId);
        }

        public Task<PhysicalAssessment?> GetLatestByStudentAsync(
            Guid studentId,
            Guid gymId)
        {
            return _inner.GetLatestByStudentAsync(
                studentId,
                gymId);
        }

        public Task<List<PhysicalAssessment>> GetHistoryByStudentAsync(
            Guid studentId,
            Guid gymId,
            int page,
            int pageSize)
        {
            return _inner.GetHistoryByStudentAsync(
                studentId,
                gymId,
                page,
                pageSize);
        }

        public Task<int> CountByStudentAsync(
            Guid studentId,
            Guid gymId)
        {
            return _inner.CountByStudentAsync(
                studentId,
                gymId);
        }

        public Task<bool> ExistsForDateAsync(
            Guid studentId,
            Guid gymId,
            DateOnly assessmentDate)
        {
            return _inner.ExistsForDateAsync(
                studentId,
                gymId,
                assessmentDate);
        }

        public async Task SaveChangesAsync()
        {
            if (!_raceInjected &&
                _pendingAssessment is not null)
            {
                _raceInjected = true;

                await using var competingContext =
                    new AppDbContext(_options);

                competingContext.PhysicalAssessments.Add(
                    new PhysicalAssessment
                    {
                        Id = Guid.NewGuid(),

                        StudentId =
                            _pendingAssessment.StudentId,

                        AssessmentDate =
                            _pendingAssessment.AssessmentDate,

                        WeightKg =
                            _pendingAssessment.WeightKg,

                        HeightCm =
                            _pendingAssessment.HeightCm,

                        BodyFatPercentage =
                            _pendingAssessment.BodyFatPercentage,

                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });

                await competingContext.SaveChangesAsync();
            }

            await _inner.SaveChangesAsync();
        }
    }

    private sealed class UtcGymTimeZoneProvider
        : IGymTimeZoneProvider
    {
        public TimeZoneInfo GetTimeZone(Guid gymId)
        {
            return TimeZoneInfo.Utc;
        }
    }
}