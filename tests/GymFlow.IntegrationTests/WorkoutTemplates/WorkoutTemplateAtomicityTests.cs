using GymFlow.Application.DTOs.WorkoutTemplates;
using GymFlow.Application.Services;
using GymFlow.Domain.Entities;
using GymFlow.Infrastructure.Data;
using GymFlow.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GymFlow.IntegrationTests.WorkoutTemplates;

public class WorkoutTemplateAtomicityTests
{
    [Fact]
    public async Task UpdateAsync_WhenFinalSaveFails_ShouldNotPersistPartialChanges()
    {
        await using var connection =
            new SqliteConnection("Data Source=:memory:");

        await connection.OpenAsync();

        var interceptor =
            new FailWhenNewTemplateDayIsSavedInterceptor();

        var options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .AddInterceptors(interceptor)
                .Options;

        var gymId = Guid.NewGuid();

        var exercise = new Exercise
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            Name = "Supino Reto",
            MuscleGroup = "Peitoral",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var template = new WorkoutTemplate
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            Name = "Modelo Original",
            Description = "Descrição original",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var oldDay = new WorkoutTemplateDay
        {
            Id = Guid.NewGuid(),
            WorkoutTemplateId = template.Id,
            WorkoutTemplate = template,
            Name = "Dia Antigo",
            Order = 1
        };

        oldDay.Exercises.Add(
            new WorkoutTemplateExercise
            {
                Id = Guid.NewGuid(),
                WorkoutTemplateDayId = oldDay.Id,
                WorkoutTemplateDay = oldDay,
                ExerciseId = exercise.Id,
                Exercise = exercise,
                Sets = 3,
                Repetitions = "10",
                RestSeconds = 60,
                Order = 1
            });

        template.Days.Add(oldDay);

        await using var context =
            new AppDbContext(options);

        await context.Database.EnsureCreatedAsync();

        context.Exercises.Add(exercise);
        context.WorkoutTemplates.Add(template);

        await context.SaveChangesAsync();

        // A partir daqui simularemos uma falha ao tentar
        // persistir os NOVOS dias do modelo.
        interceptor.Enabled = true;

        var templateRepository =
            new WorkoutTemplateRepository(context);

        var exerciseRepository =
            new ExerciseRepository(context);

        var service =
            new WorkoutTemplateService(
                templateRepository,
                exerciseRepository);

        var request = new UpdateWorkoutTemplateRequest
        {
            Name = "Modelo Atualizado",
            Description = "Descrição atualizada",
            Days =
            [
                new UpdateWorkoutTemplateDayRequest
                {
                    Name = "Novo Dia",
                    Order = 1,
                    Exercises =
                    [
                        new UpdateWorkoutTemplateExerciseRequest
                        {
                            ExerciseId = exercise.Id,
                            Sets = 4,
                            Repetitions = "12",
                            RestSeconds = 60,
                            Order = 1
                        }
                    ]
                }
            ]
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateAsync(
                template.Id,
                gymId,
                request));

        // Um novo DbContext impede que o ChangeTracker
        // esconda o estado realmente persistido.
        await using var verificationContext =
            new AppDbContext(options);

        var persistedTemplate =
            await verificationContext.WorkoutTemplates
                .AsNoTracking()
                .Include(x => x.Days)
                .SingleAsync(x => x.Id == template.Id);

        // Se a atualização for atômica, a falha deve deixar
        // o modelo exatamente como estava antes.
        Assert.Equal(
            "Modelo Original",
            persistedTemplate.Name);

        Assert.Equal(
            "Descrição original",
            persistedTemplate.Description);

        var persistedDay =
            Assert.Single(persistedTemplate.Days);

        Assert.Equal(
            "Dia Antigo",
            persistedDay.Name);
    }

    private sealed class FailWhenNewTemplateDayIsSavedInterceptor
        : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }

        public override ValueTask<InterceptionResult<int>>
            SavingChangesAsync(
                DbContextEventData eventData,
                InterceptionResult<int> result,
                CancellationToken cancellationToken = default)
        {
            if (Enabled &&
                eventData.Context is not null &&
                eventData.Context.ChangeTracker
                    .Entries<WorkoutTemplateDay>()
                    .Any(entry =>
                        entry.State == EntityState.Added &&
                        entry.Entity.Name == "Novo Dia"))
            {
                throw new InvalidOperationException(
                    "Falha simulada durante a gravação.");
            }

            return base.SavingChangesAsync(
                eventData,
                result,
                cancellationToken);
        }
    }
}