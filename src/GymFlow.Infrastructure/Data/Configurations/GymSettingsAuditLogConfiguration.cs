using GymFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace GymFlow.Infrastructure.Data.Configurations;

public sealed class GymSettingsAuditLogConfiguration : IEntityTypeConfiguration<GymSettingsAuditLog>
{
    public void Configure(EntityTypeBuilder<GymSettingsAuditLog> builder)
    {
        builder.ToTable("GymSettingsAuditLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PreviousValues).IsRequired().HasColumnType("jsonb");
        builder.Property(x => x.NewValues).IsRequired().HasColumnType("jsonb");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GymSettings>().WithMany().HasForeignKey(x => x.GymId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.GymId, x.OccurredAt });
    }
}
