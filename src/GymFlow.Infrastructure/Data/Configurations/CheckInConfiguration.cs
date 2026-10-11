using GymFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymFlow.Infrastructure.Data.Configurations;

public class CheckInConfiguration :
    IEntityTypeConfiguration<CheckIn>
{
    public void Configure(
        EntityTypeBuilder<CheckIn> builder)
    {
        builder.ToTable("CheckIns");

        builder.HasKey(checkIn => checkIn.Id);

        builder.Property(checkIn => checkIn.GymId)
            .IsRequired();

        builder.Property(checkIn => checkIn.StudentId)
            .IsRequired();

        builder.Property(checkIn => checkIn.RequestId)
            .IsRequired();

        builder.Property(checkIn => checkIn.Source)
            .IsRequired();

        builder.Property(checkIn => checkIn.OccurredAt)
            .IsRequired();

        builder.Property(checkIn => checkIn.CreatedAt)
            .IsRequired();

        builder.HasOne(checkIn => checkIn.Student)
            .WithMany()
            .HasForeignKey(checkIn => checkIn.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(checkIn => checkIn.PhysicalAccessEvent)
            .WithMany()
            .HasForeignKey(checkIn => checkIn.PhysicalAccessEventId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(checkIn => checkIn.RecordedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(checkIn => new
        {
            checkIn.GymId,
            checkIn.RequestId
        })
        .IsUnique();

        builder.HasIndex(checkIn =>
                checkIn.PhysicalAccessEventId)
            .IsUnique();

        builder.HasIndex(checkIn => new
        {
            checkIn.GymId,
            checkIn.OccurredAt
        });

        builder.HasIndex(checkIn => new
        {
            checkIn.GymId,
            checkIn.StudentId,
            checkIn.OccurredAt
        });
    }
}