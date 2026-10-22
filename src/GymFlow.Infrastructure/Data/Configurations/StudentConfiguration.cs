using GymFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymFlow.Infrastructure.Data.Configurations;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("Students");

        builder.HasKey(student => student.Id);
        builder.Property(student => student.Name).IsRequired().HasMaxLength(150);
        // Email participates in invite redemption/update concurrency. This is
        // an optimistic concurrency token and does not change the database schema.
        builder.Property(student => student.Email).HasMaxLength(200).IsConcurrencyToken();
        builder.Property(student => student.IsActive).IsConcurrencyToken();
        builder.Property(student => student.AppAccessEnabled)
            .HasDefaultValue(false)
            .IsConcurrencyToken();
        builder.Property(student => student.AppInviteTokenHash).HasMaxLength(64).IsConcurrencyToken();
        builder.HasIndex(student => student.GymId);
        builder.HasIndex(student => student.AppInviteTokenHash).IsUnique();

        // Existing column: optimistic concurrency needs no schema change.
        builder.Property(student => student.InactivationReason).IsConcurrencyToken();
        builder.Property(student => student.ArchivedAt).IsConcurrencyToken();
        builder.Property(student => student.UpdatedAt).IsConcurrencyToken();

        builder.Property(student => student.Phone)
            .HasMaxLength(20);

        builder.HasIndex(student => student.NoValidEnrollmentSince);

        builder.HasIndex(student => student.ArchivedAt);

        builder.HasOne(student => student.User)
            .WithOne()
            .HasForeignKey<Student>(student => student.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(student => student.UserId)
            .IsUnique();
    }
}
