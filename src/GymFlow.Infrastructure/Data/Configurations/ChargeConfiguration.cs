using GymFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using GymFlow.Domain.Enums;

namespace GymFlow.Infrastructure.Data.Configurations;

public class ChargeConfiguration : IEntityTypeConfiguration<Charge>
{
    // Charge origin and verification enums are persisted as integers.
    public void Configure(EntityTypeBuilder<Charge> builder)
    {
        builder.ToTable("Charges");

        builder.HasKey(charge => charge.Id);

        builder.Property(charge => charge.EnrollmentId)
            .IsRequired();

        builder.Property(charge => charge.Amount)
            .IsRequired()
            .HasPrecision(10, 2);

        builder.Property(charge => charge.DiscountAmount)
            .IsRequired()
            .HasPrecision(10, 2);

        builder.Property(charge => charge.PaidAmount)
            .HasPrecision(10, 2);

        builder.Property(charge => charge.PaymentMethod);

        builder.Property(charge => charge.DueDate)
            .IsRequired();

        builder.Property(charge => charge.Status)
            .IsRequired();

        builder.Property(charge => charge.Origin).IsRequired().HasDefaultValue(ChargeOrigin.Avelri);
        builder.Property(charge => charge.PaymentOrigin).IsRequired().HasDefaultValue(PaymentOrigin.Avelri);
        builder.Property(charge => charge.VerificationStatus).IsRequired().HasDefaultValue(ChargeVerificationStatus.Verified);

        builder.Property(charge => charge.CreatedAt)
            .IsRequired();

        builder.HasOne(charge => charge.Enrollment)
            .WithOne(enrollment => enrollment.Charge)
            .HasForeignKey<Charge>(charge => charge.EnrollmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(charge => charge.EnrollmentId)
            .IsUnique();

        builder.HasIndex(charge => new
        {
            charge.Status,
            charge.DueDate
        });
    }
}
