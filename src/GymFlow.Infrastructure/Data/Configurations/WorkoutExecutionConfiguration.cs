using GymFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymFlow.Infrastructure.Data.Configurations;

public class WorkoutExecutionConfiguration
    : IEntityTypeConfiguration<WorkoutExecution>
{
    public void Configure(EntityTypeBuilder<WorkoutExecution> builder)
    {
        builder.ToTable("WorkoutExecutions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CompletedAt)
            .IsRequired();

        builder.HasIndex(x => x.WorkoutDayId);

        builder.HasIndex(x => x.CompletedAt);
    }
}