using GymFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymFlow.Infrastructure.Data.Configurations;

public class WorkoutDayConfiguration : IEntityTypeConfiguration<WorkoutDay>
{
    public void Configure(EntityTypeBuilder<WorkoutDay> builder)
    {
        builder.ToTable("WorkoutDays");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Order)
            .IsRequired();

        builder.HasIndex(x => x.WorkoutId);

        builder.HasIndex(x => new { x.WorkoutId, x.Order })
            .IsUnique();

        builder.HasMany(x => x.Executions)
            .WithOne(x => x.WorkoutDay)
            .HasForeignKey(x => x.WorkoutDayId)
            .OnDelete(DeleteBehavior.Restrict);

    }
}