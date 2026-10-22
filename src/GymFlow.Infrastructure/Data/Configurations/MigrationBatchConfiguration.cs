using GymFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymFlow.Infrastructure.Data.Configurations;

public sealed class MigrationBatchConfiguration : IEntityTypeConfiguration<MigrationBatch>
{
    public void Configure(EntityTypeBuilder<MigrationBatch> builder)
    {
        builder.ToTable("MigrationBatches");
        builder.HasKey(batch => batch.Id);
        builder.Property(batch => batch.Fingerprint).HasMaxLength(64).IsRequired();
        builder.Property(batch => batch.PayloadJson).HasColumnType("jsonb").IsRequired();
        builder.Property(batch => batch.PreviewJson).HasColumnType("jsonb").IsRequired();
        builder.Property(batch => batch.DecisionsJson).HasColumnType("jsonb").IsRequired();
        builder.Property(batch => batch.ResultJson).HasColumnType("jsonb").IsRequired();
        builder.Property(batch => batch.Status).IsConcurrencyToken();
        builder.HasOne<MigrationSession>().WithMany().HasForeignKey(batch => batch.MigrationSessionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(batch => new { batch.MigrationSessionId, batch.EntityType, batch.Fingerprint }).IsUnique();
        builder.HasIndex(batch => new { batch.GymId, batch.MigrationSessionId });
    }
}
