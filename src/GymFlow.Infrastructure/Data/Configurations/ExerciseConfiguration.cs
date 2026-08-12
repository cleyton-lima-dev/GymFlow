using GymFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymFlow.Infrastructure.Data.Configurations;

public class ExerciseConfiguration : IEntityTypeConfiguration<Exercise>
{
    public void Configure(EntityTypeBuilder<Exercise> builder)
    {
        builder.ToTable("Exercises");

        builder.HasKey(exercise => exercise.Id);

        builder.Property(exercise => exercise.GymId)
            .IsRequired();

        builder.Property(exercise => exercise.Name)
            .IsRequired()
            .HasMaxLength(150)
            .HasColumnType("citext");

        builder.Property(exercise => exercise.MuscleGroup)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(exercise => exercise.Description)
            .HasMaxLength(500);

        builder.Property(exercise => exercise.IsActive)
            .IsRequired();

        builder.Property(exercise => exercise.CreatedAt)
            .IsRequired();

        builder.HasIndex(exercise => exercise.GymId);

        builder.HasIndex(exercise => new
        {
            exercise.GymId,
            exercise.Name
        })
        .IsUnique();
    }
}