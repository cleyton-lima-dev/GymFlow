using GymFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymFlow.Infrastructure.Data.Configurations;

public class WorkoutDayProgressConfiguration
    : IEntityTypeConfiguration<WorkoutDayProgress>
{
    public void Configure(EntityTypeBuilder<WorkoutDayProgress> builder)
    {
        builder.ToTable("WorkoutDayProgresses");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.StartedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        // Um aluno só pode possuir um dia em andamento.
        builder.HasIndex(x => x.StudentId)
            .IsUnique();

        builder.HasIndex(x => x.WorkoutDayId);

        builder.HasOne(x => x.Student)
            .WithMany()
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.WorkoutDay)
            .WithMany()
            .HasForeignKey(x => x.WorkoutDayId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}