using GymFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymFlow.Infrastructure.Data.Configurations;

public sealed class MigrationSessionConfiguration : IEntityTypeConfiguration<MigrationSession>
{
    public void Configure(EntityTypeBuilder<MigrationSession> builder)
    {
        builder.ToTable("MigrationSessions");
        builder.HasKey(session => session.Id);
        builder.Property(session => session.Name).HasMaxLength(150).IsRequired();
        builder.Property(session => session.SourceNamespace).HasMaxLength(120).IsRequired();
        builder.Property(session => session.Status).IsRequired();
        builder.Property(session => session.CreatedAt).IsRequired();
        builder.Property(session => session.ExpiresAt).IsRequired();
        builder.HasOne<User>().WithMany().HasForeignKey(session => session.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(session => session.ReleasedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(session => new { session.GymId, session.Status });
        builder.HasIndex(session => session.ExpiresAt);
    }
}
