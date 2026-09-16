using GymFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymFlow.Infrastructure.Data.Configurations;

public class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.ToTable("Plans");

        builder.HasKey(plan => plan.Id);

        builder.Property(plan => plan.GymId)
            .IsRequired();

        builder.Property(plan => plan.Name)
            .IsRequired()
            .HasMaxLength(150)
            .HasColumnType("citext");

        builder.Property(plan => plan.Price)
            .IsRequired()
            .HasPrecision(10, 2);

        builder.Property(plan => plan.DurationMonths)
            .IsRequired();

        builder.Property(plan => plan.BillingCycle)
            .IsRequired();

        builder.Property(plan => plan.IsActive)
            .IsRequired();

        builder.Property(plan => plan.CreatedAt)
            .IsRequired();

        builder.HasIndex(plan => plan.GymId);

        builder.HasIndex(plan => new
        {
            plan.GymId,
            plan.Name
        })
        .IsUnique();
    }
}