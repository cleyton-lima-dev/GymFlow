using GymFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymFlow.Infrastructure.Data.Configurations;

public class WorkoutExerciseConfiguration
    : IEntityTypeConfiguration<WorkoutExercise>
{
    public void Configure(EntityTypeBuilder<WorkoutExercise> builder)
    {
        builder.ToTable("WorkoutExercises");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Sets)
            .IsRequired();

        builder.Property(x => x.Repetitions)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Notes)
            .HasMaxLength(500);

        builder.Property(x => x.Order)
            .IsRequired();

        builder.HasIndex(x => x.WorkoutDayId);

        builder.HasIndex(x => x.ExerciseId);

        builder.HasIndex(x => new { x.WorkoutDayId, x.Order })
            .IsUnique();

        builder.HasOne(x => x.Exercise)
            .WithMany()
            .HasForeignKey(x => x.ExerciseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}