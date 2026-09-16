using GymFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymFlow.Infrastructure.Data.Configurations;

public class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.ToTable("Enrollments");

        builder.HasKey(enrollment => enrollment.Id);

        builder.Property(enrollment => enrollment.StudentId)
            .IsRequired();

        builder.Property(enrollment => enrollment.PlanId)
            .IsRequired();

        builder.Property(enrollment => enrollment.StartDate)
            .IsRequired();

        builder.Property(enrollment => enrollment.EndDate)
            .IsRequired();

        builder.Property(enrollment => enrollment.Status)
            .IsRequired();

        builder.Property(enrollment => enrollment.PlanName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(enrollment => enrollment.PlanPrice)
            .IsRequired()
            .HasPrecision(10, 2);

        builder.Property(enrollment => enrollment.PlanDurationMonths)
            .IsRequired();

        builder.Property(enrollment => enrollment.PlanBillingCycle)
            .IsRequired();

        builder.Property(enrollment => enrollment.CreatedAt)
            .IsRequired();

        builder.HasOne(enrollment => enrollment.Student)
            .WithMany()
            .HasForeignKey(enrollment => enrollment.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(enrollment => enrollment.Plan)
            .WithMany()
            .HasForeignKey(enrollment => enrollment.PlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(enrollment => enrollment.StudentId);

        builder.HasIndex(enrollment => enrollment.PlanId);

        builder.HasIndex(enrollment => new
        {
            enrollment.Status,
            enrollment.EndDate
        });
    }
}