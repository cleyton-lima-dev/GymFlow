using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using GymFlow.Infrastructure.Data;
using GymFlow.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.IntegrationTests.Users;

public class UserRepositoryTests
{
    [Fact]
    public async Task GetProfessorsByGymIdAsync_ShouldReturnOnlyProfessorsFromRequestedGym()
    {
        await using var connection =
            new SqliteConnection("Data Source=:memory:");

        await connection.OpenAsync();

        var options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

        await using var context =
            new AppDbContext(options);

        await context.Database.EnsureCreatedAsync();

        var gymA = Guid.NewGuid();
        var gymB = Guid.NewGuid();

        context.Users.AddRange(
            new User
            {
                Id = Guid.NewGuid(),
                GymId = gymA,
                Name = "Professor Academia A",
                Email = "professor-a@teste.com",
                PasswordHash = "hash",
                Role = UserRole.Professor,
                IsActive = true
            },
            new User
            {
                Id = Guid.NewGuid(),
                GymId = gymB,
                Name = "Professor Academia B",
                Email = "professor-b@teste.com",
                PasswordHash = "hash",
                Role = UserRole.Professor,
                IsActive = true
            },
            new User
            {
                Id = Guid.NewGuid(),
                GymId = gymA,
                Name = "Admin Academia A",
                Email = "admin-a@teste.com",
                PasswordHash = "hash",
                Role = UserRole.Admin,
                IsActive = true
            });

        await context.SaveChangesAsync();

        var repository =
            new UserRepository(context);

        var result =
            await repository.GetProfessorsByGymIdAsync(gymA);

        var professor =
            Assert.Single(result);

        Assert.Equal(
            "Professor Academia A",
            professor.Name);

        Assert.Equal(
            gymA,
            professor.GymId);

        Assert.DoesNotContain(
            result,
            user => user.GymId == gymB);

        Assert.DoesNotContain(
            result,
            user => user.Role != UserRole.Professor);
    }
}