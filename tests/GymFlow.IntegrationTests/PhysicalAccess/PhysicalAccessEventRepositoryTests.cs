using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using GymFlow.Infrastructure.Data;
using GymFlow.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.IntegrationTests.PhysicalAccess;

public class PhysicalAccessEventRepositoryTests
{
    [Fact]
    public async Task GetPagedAsync_WhenStudentBelongsToAnotherGym_ShouldReturnEmpty()
    {
        await using var connection =
            new SqliteConnection("Data Source=:memory:");

        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);

        await context.Database.EnsureCreatedAsync();

        var gymA = Guid.NewGuid();
        var gymB = Guid.NewGuid();
        var studentBId = Guid.NewGuid();

        var userB = new User
        {
            Id = Guid.NewGuid(),
            GymId = gymB,
            Name = "Aluno Academia B",
            Email = $"{Guid.NewGuid():N}@teste.com",
            PasswordHash = "hash",
            Role = UserRole.Student,
            IsActive = true
        };

        var studentB = new Student
        {
            Id = studentBId,
            UserId = userB.Id,
            User = userB
        };

        var occurredAt = new DateTime(
            2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);

        context.Users.Add(userB);
        context.Students.Add(studentB);

        context.PhysicalAccessEvents.Add(
            new PhysicalAccessEvent
            {
                Id = Guid.NewGuid(),
                GymId = gymB,
                RequestId = Guid.NewGuid(),
                StudentId = studentBId,
                Decision = PhysicalAccessDecision.Allowed,
                Reason = PhysicalAccessDecisionReason.Eligible,
                Source = PhysicalAccessDecisionSource.Online,
                OccurredAt = occurredAt,
                ProcessedAt = occurredAt
            });

        await context.SaveChangesAsync();

        var repository = new PhysicalAccessEventRepository(context);

        var result = await repository.GetPagedAsync(
            gymA,
            studentBId,
            null,
            null,
            null,
            null,
            0,
            20);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }
    [Fact]
    public async Task GetPagedAsync_ShouldPaginateOnlyRequestedGym()
    {
        await using var connection =
            new SqliteConnection("Data Source=:memory:");

        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);

        await context.Database.EnsureCreatedAsync();

        var gymA = Guid.NewGuid();
        var gymB = Guid.NewGuid();

        var baseline = new DateTime(
            2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);

        for (var i = 0; i < 5; i++)
        {
            context.PhysicalAccessEvents.Add(
                new PhysicalAccessEvent
                {
                    Id = Guid.NewGuid(),
                    GymId = gymA,
                    RequestId = Guid.NewGuid(),
                    Decision = PhysicalAccessDecision.Allowed,
                    Reason = PhysicalAccessDecisionReason.Eligible,
                    Source = PhysicalAccessDecisionSource.Online,
                    OccurredAt = baseline.AddMinutes(i),
                    ProcessedAt = baseline.AddMinutes(i)
                });
        }

        for (var i = 0; i < 3; i++)
        {
            context.PhysicalAccessEvents.Add(
                new PhysicalAccessEvent
                {
                    Id = Guid.NewGuid(),
                    GymId = gymB,
                    RequestId = Guid.NewGuid(),
                    Decision = PhysicalAccessDecision.Denied,
                    Reason = PhysicalAccessDecisionReason.CredentialNotFound,
                    Source = PhysicalAccessDecisionSource.Online,
                    OccurredAt = baseline.AddMinutes(i),
                    ProcessedAt = baseline.AddMinutes(i)
                });
        }

        await context.SaveChangesAsync();

        var repository = new PhysicalAccessEventRepository(context);

        var firstPage = await repository.GetPagedAsync(
            gymA, null, null, null, null, null, 0, 2);

        var secondPage = await repository.GetPagedAsync(
            gymA, null, null, null, null, null, 2, 2);

        Assert.Equal(5, firstPage.TotalCount);
        Assert.Equal(5, secondPage.TotalCount);

        Assert.Equal(2, firstPage.Items.Count);
        Assert.Equal(2, secondPage.Items.Count);

        Assert.All(firstPage.Items,
            item => Assert.Equal(gymA, item.GymId));

        Assert.All(secondPage.Items,
            item => Assert.Equal(gymA, item.GymId));

        Assert.Empty(
            firstPage.Items.Select(item => item.Id)
                .Intersect(secondPage.Items.Select(item => item.Id)));

        Assert.True(
            firstPage.Items[0].OccurredAt >
            firstPage.Items[1].OccurredAt);

        Assert.True(
            firstPage.Items[1].OccurredAt >
            secondPage.Items[0].OccurredAt);
    }
    [Fact]
    public async Task GetPagedAsync_ShouldIsolateGymAndApplyFilters()
    {
        await using var connection =
            new SqliteConnection("Data Source=:memory:");

        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);

        await context.Database.EnsureCreatedAsync();

        var gymA = Guid.NewGuid();
        var gymB = Guid.NewGuid();

        var occurredAt = new DateTime(
            2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);

        var eventA = new PhysicalAccessEvent
        {
            Id = Guid.NewGuid(),
            GymId = gymA,
            RequestId = Guid.NewGuid(),
            Decision = PhysicalAccessDecision.Denied,
            Reason = PhysicalAccessDecisionReason.CredentialNotFound,
            Source = PhysicalAccessDecisionSource.OfflineCache,
            OccurredAt = occurredAt,
            ProcessedAt = occurredAt
        };

        var eventB = new PhysicalAccessEvent
        {
            Id = Guid.NewGuid(),
            GymId = gymB,
            RequestId = Guid.NewGuid(),
            Decision = PhysicalAccessDecision.Denied,
            Reason = PhysicalAccessDecisionReason.CredentialNotFound,
            Source = PhysicalAccessDecisionSource.OfflineCache,
            OccurredAt = occurredAt,
            ProcessedAt = occurredAt
        };

        context.PhysicalAccessEvents.AddRange(eventA, eventB);

        await context.SaveChangesAsync();

        var repository = new PhysicalAccessEventRepository(context);

        var result = await repository.GetPagedAsync(
            gymA,
            null,
            PhysicalAccessDecision.Denied,
            PhysicalAccessDecisionSource.OfflineCache,
            occurredAt.AddHours(-1),
            occurredAt.AddHours(1),
            0,
            20);

        var item = Assert.Single(result.Items);

        Assert.Equal(eventA.Id, item.Id);
        Assert.Equal(gymA, item.GymId);
        Assert.Null(item.StudentId);
        Assert.Equal(1, result.TotalCount);

        Assert.DoesNotContain(
            result.Items,
            accessEvent => accessEvent.GymId == gymB);
    }
}
