using GymFlow.Application.DTOs.Workouts;
using GymFlow.Application.Interfaces.Time;
using GymFlow.Application.Services;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using GymFlow.Infrastructure.Data;
using GymFlow.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.IntegrationTests.Workouts;

public class WorkoutReorderTests
{
    [Fact]
    public async Task UpdateAsync_WhenReorderingDaysAndExercises_ShouldPersistFinalOrder()
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

        var gymId = Guid.NewGuid();

        var user = new User
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            Name = "Aluno Teste",
            Email = "aluno-reorder@teste.com",
            PasswordHash = "hash",
            Role = UserRole.Student,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var student = new Student
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            CreatedAt = DateTime.UtcNow
        };

        var exerciseA = new Exercise
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            Name = "Exercício A",
            MuscleGroup = "Peitoral",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var exerciseB = new Exercise
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            Name = "Exercício B",
            MuscleGroup = "Costas",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var workout = new Workout
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            Student = student,
            GymId = gymId,
            Name = "Treino Original",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var dayA = new WorkoutDay
        {
            Id = Guid.NewGuid(),
            WorkoutId = workout.Id,
            Workout = workout,
            Name = "Dia A",
            Order = 1
        };

        var dayB = new WorkoutDay
        {
            Id = Guid.NewGuid(),
            WorkoutId = workout.Id,
            Workout = workout,
            Name = "Dia B",
            Order = 2
        };

        var workoutExerciseA = new WorkoutExercise
        {
            Id = Guid.NewGuid(),
            WorkoutDayId = dayA.Id,
            WorkoutDay = dayA,
            ExerciseId = exerciseA.Id,
            Exercise = exerciseA,
            Sets = 3,
            Repetitions = "10",
            Order = 1
        };

        var workoutExerciseB = new WorkoutExercise
        {
            Id = Guid.NewGuid(),
            WorkoutDayId = dayA.Id,
            WorkoutDay = dayA,
            ExerciseId = exerciseB.Id,
            Exercise = exerciseB,
            Sets = 3,
            Repetitions = "10",
            Order = 2
        };

        dayA.Exercises.Add(workoutExerciseA);
        dayA.Exercises.Add(workoutExerciseB);

        workout.Days.Add(dayA);
        workout.Days.Add(dayB);

        context.Workouts.Add(workout);

        await context.SaveChangesAsync();

        var service = new WorkoutService(
            new WorkoutRepository(context),
            new StudentRepository(context),
            new ExerciseRepository(context),
            new WorkoutTemplateRepository(context),
            new WorkoutExecutionRepository(context),
            new WorkoutDayProgressRepository(context),
            new UtcGymTimeZoneProvider());

        var request = new UpdateWorkoutRequest
        {
            Name = "Treino Reordenado",
            Days =
            [
                new UpdateWorkoutDayRequest
                {
                    Id = dayB.Id,
                    Name = "Dia B",
                    Order = 1,
                    Exercises =
                    [
                        new UpdateWorkoutExerciseRequest
                        {
                            ExerciseId = exerciseA.Id,
                            Sets = 3,
                            Repetitions = "10",
                            Order = 1
                        }
                    ]
                },
                new UpdateWorkoutDayRequest
                {
                    Id = dayA.Id,
                    Name = "Dia A",
                    Order = 2,
                    Exercises =
                    [
                        new UpdateWorkoutExerciseRequest
                        {
                            Id = workoutExerciseB.Id,
                            ExerciseId = exerciseB.Id,
                            Sets = 3,
                            Repetitions = "10",
                            Order = 1
                        },
                        new UpdateWorkoutExerciseRequest
                        {
                            Id = workoutExerciseA.Id,
                            ExerciseId = exerciseA.Id,
                            Sets = 3,
                            Repetitions = "10",
                            Order = 2
                        }
                    ]
                }
            ]
        };

        await service.UpdateAsync(
                gymId,
                workout.Id,
                request);

        context.ChangeTracker.Clear();

        var persistedWorkout =
            await context.Workouts
                .AsNoTracking()
                .Include(x => x.Days)
                    .ThenInclude(x => x.Exercises)
                .SingleAsync(x => x.Id == workout.Id);

        var orderedDays =
            persistedWorkout.Days
                .OrderBy(x => x.Order)
                .ToList();

        Assert.Equal(dayB.Id, orderedDays[0].Id);
        Assert.Equal(dayA.Id, orderedDays[1].Id);

        var persistedDayA =
            orderedDays.Single(x => x.Id == dayA.Id);

        var orderedExercises =
            persistedDayA.Exercises
                .OrderBy(x => x.Order)
                .ToList();

        Assert.Equal(
            workoutExerciseB.Id,
            orderedExercises[0].Id);

        Assert.Equal(
            workoutExerciseA.Id,
            orderedExercises[1].Id);
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