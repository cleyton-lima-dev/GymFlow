using GymFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymFlow.Infrastructure.Data.Configurations;

public class FinancialAuditLogConfiguration
    : IEntityTypeConfiguration<FinancialAuditLog>
{
    public void Configure(
        EntityTypeBuilder<FinancialAuditLog> builder)
    {
        builder.ToTable("FinancialAuditLogs");

        builder.HasKey(audit => audit.Id);

        builder.Property(audit => audit.GymId)
            .IsRequired();

        builder.Property(audit => audit.ActorUserId)
            .IsRequired();

        builder.Property(audit => audit.EntityType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(audit => audit.EntityId)
            .IsRequired();

        builder.Property(audit => audit.Action)
            .IsRequired();

        builder.Property(audit => audit.PreviousValues)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(audit => audit.NewValues)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(audit => audit.OccurredAt)
            .IsRequired();

        builder.HasOne(audit => audit.ActorUser)
            .WithMany()
            .HasForeignKey(audit => audit.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(audit => new
        {
            audit.GymId,
            audit.OccurredAt
        });

        builder.HasIndex(audit => new
        {
            audit.EntityType,
            audit.EntityId
        });
    }
}