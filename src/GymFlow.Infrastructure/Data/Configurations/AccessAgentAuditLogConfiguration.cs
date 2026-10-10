using GymFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymFlow.Infrastructure.Data.Configurations;

public class AccessAgentAuditLogConfiguration
    : IEntityTypeConfiguration<AccessAgentAuditLog>
{
    public void Configure(
        EntityTypeBuilder<AccessAgentAuditLog> builder)
    {
        builder.ToTable("AccessAgentAuditLogs");

        builder.HasKey(audit => audit.Id);

        builder.Property(audit => audit.GymId)
            .IsRequired();

        builder.Property(audit => audit.ActorUserId)
            .IsRequired();

        builder.Property(audit => audit.AccessAgentId)
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

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(audit => audit.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AccessAgent>()
            .WithMany()
            .HasForeignKey(audit => audit.AccessAgentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(audit => new
        {
            audit.GymId,
            audit.OccurredAt
        });

        builder.HasIndex(audit => new
        {
            audit.AccessAgentId,
            audit.OccurredAt
        });
    }
}
