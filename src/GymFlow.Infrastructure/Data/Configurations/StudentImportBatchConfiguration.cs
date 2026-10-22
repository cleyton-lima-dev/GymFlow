using GymFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymFlow.Infrastructure.Data.Configurations;

public sealed class StudentImportBatchConfiguration : IEntityTypeConfiguration<StudentImportBatch>
{
    public void Configure(EntityTypeBuilder<StudentImportBatch> builder)
    {
        builder.ToTable("StudentImportBatches");
        builder.HasKey(batch => batch.Id);
        builder.Property(batch => batch.Fingerprint).HasMaxLength(64).IsRequired();
        builder.Property(batch => batch.PayloadJson).HasColumnType("jsonb");
        builder.Property(batch => batch.PreviewJson).HasColumnType("jsonb");
        builder.Property(batch => batch.ResultJson).HasColumnType("jsonb");
        builder.Property(batch => batch.State).IsConcurrencyToken();
        builder.HasIndex(batch => new { batch.GymId, batch.Fingerprint }).IsUnique();
        builder.HasIndex(batch => batch.ExpiresAt);
    }
}
