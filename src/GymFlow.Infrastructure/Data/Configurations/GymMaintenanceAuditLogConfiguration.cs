using GymFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymFlow.Infrastructure.Data.Configurations;

public sealed class GymMaintenanceAuditLogConfiguration : IEntityTypeConfiguration<GymMaintenanceAuditLog>
{
    public void Configure(EntityTypeBuilder<GymMaintenanceAuditLog> builder)
    {
        builder.ToTable("GymMaintenanceAuditLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).HasMaxLength(12).IsRequired();
        builder.HasOne<GymMaintenanceState>().WithMany().HasForeignKey(x => x.GymId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PlatformOperator>().WithMany().HasForeignKey(x => x.ActorPlatformOperatorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.GymId, x.OccurredAt });
    }
}
