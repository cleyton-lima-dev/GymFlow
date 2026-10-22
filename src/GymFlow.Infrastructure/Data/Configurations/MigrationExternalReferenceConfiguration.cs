using GymFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymFlow.Infrastructure.Data.Configurations;

public sealed class MigrationExternalReferenceConfiguration : IEntityTypeConfiguration<MigrationExternalReference>
{
    public void Configure(EntityTypeBuilder<MigrationExternalReference> builder)
    {
        builder.ToTable("MigrationExternalReferences");
        builder.HasKey(reference => reference.Id);
        builder.Property(reference => reference.SourceNamespace).HasMaxLength(120).IsRequired();
        builder.Property(reference => reference.ExternalId).HasMaxLength(200).IsRequired();
        builder.HasOne<MigrationSession>().WithMany().HasForeignKey(reference => reference.MigrationSessionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(reference => new
        {
            reference.GymId, reference.SourceNamespace, reference.EntityType, reference.ExternalId
        }).IsUnique();
        builder.HasIndex(reference => new { reference.GymId, reference.TargetEntityId });
    }
}
