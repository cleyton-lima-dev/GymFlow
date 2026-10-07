using GymFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymFlow.Infrastructure.Data.Configurations;

public class PhysicalAccessEventConfiguration
    : IEntityTypeConfiguration<PhysicalAccessEvent>
{
    public void Configure(
        EntityTypeBuilder<PhysicalAccessEvent> builder)
    {
        builder.ToTable("PhysicalAccessEvents");

        builder.HasKey(accessEvent => accessEvent.Id);

        builder.Property(accessEvent => accessEvent.GymId)
            .IsRequired();

        builder.Property(accessEvent => accessEvent.RequestId)
            .IsRequired();

        builder.Property(accessEvent => accessEvent.Decision)
            .IsRequired();

        builder.Property(accessEvent => accessEvent.Reason)
            .IsRequired();

        builder.Property(accessEvent => accessEvent.Source)
            .IsRequired();

        builder.Property(accessEvent => accessEvent.OccurredAt)
            .IsRequired();

        builder.Property(accessEvent => accessEvent.ProcessedAt)
            .IsRequired();

        builder.HasOne(accessEvent => accessEvent.Student)
            .WithMany()
            .HasForeignKey(accessEvent => accessEvent.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(accessEvent => accessEvent.Credential)
            .WithMany()
            .HasForeignKey(accessEvent => accessEvent.CredentialId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(accessEvent => new
        {
            accessEvent.GymId,
            accessEvent.RequestId
        })
        .IsUnique();

        builder.HasIndex(accessEvent => new
        {
            accessEvent.GymId,
            accessEvent.OccurredAt
        });

        builder.HasIndex(accessEvent => new
        {
            accessEvent.StudentId,
            accessEvent.OccurredAt
        });
    }
}