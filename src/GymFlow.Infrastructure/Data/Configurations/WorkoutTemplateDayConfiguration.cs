using GymFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymFlow.Infrastructure.Data.Configurations;

public class WorkoutTemplateDayConfiguration : IEntityTypeConfiguration<WorkoutTemplateDay>
{
    public void Configure(EntityTypeBuilder<WorkoutTemplateDay> builder)
    {
        builder.ToTable("WorkoutTemplateDays");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Order)
            .IsRequired();

        builder.HasIndex(x => x.WorkoutTemplateId);

        builder.HasIndex(x => new { x.WorkoutTemplateId, x.Order })
            .IsUnique();

        builder.HasMany(x => x.Exercises)
            .WithOne(x => x.WorkoutTemplateDay)
            .HasForeignKey(x => x.WorkoutTemplateDayId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}