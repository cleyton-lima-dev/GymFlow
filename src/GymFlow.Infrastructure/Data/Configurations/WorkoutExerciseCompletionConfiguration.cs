using GymFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymFlow.Infrastructure.Data.Configurations;

public class WorkoutExerciseCompletionConfiguration
    : IEntityTypeConfiguration<WorkoutExerciseCompletion>
{
    public void Configure(EntityTypeBuilder<WorkoutExerciseCompletion> builder)
    {
        builder.ToTable("WorkoutExerciseCompletions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CompletedAt)
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.WorkoutDayProgressId,
            x.WorkoutExerciseId
        })
        .IsUnique();

        builder.HasOne(x => x.WorkoutDayProgress)
            .WithMany(x => x.CompletedExercises)
            .HasForeignKey(x => x.WorkoutDayProgressId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.WorkoutExercise)
            .WithMany()
            .HasForeignKey(x => x.WorkoutExerciseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}