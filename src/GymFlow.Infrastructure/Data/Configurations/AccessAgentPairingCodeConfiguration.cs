using GymFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymFlow.Infrastructure.Data.Configurations;

public class AccessAgentPairingCodeConfiguration :
    IEntityTypeConfiguration<AccessAgentPairingCode>
{
    public void Configure(
        EntityTypeBuilder<AccessAgentPairingCode> builder)
    {
        builder.ToTable(
            "AccessAgentPairingCodes");

        builder.HasKey(
            pairingCode =>
                pairingCode.Id);

        builder.Property(
                pairingCode =>
                    pairingCode.GymId)
            .IsRequired();

        builder.Property(
                pairingCode =>
                    pairingCode.CreatedByUserId)
            .IsRequired();

        builder.Property(
                pairingCode =>
                    pairingCode.CodeHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(
                pairingCode =>
                    pairingCode.ExpiresAt)
            .IsRequired();

        builder.Property(
                pairingCode =>
                    pairingCode.ConsumedAt)
            .IsRequired(false);

        builder.Property(
                pairingCode =>
                    pairingCode.CreatedAt)
            .IsRequired();

        builder.HasIndex(
                pairingCode =>
                    pairingCode.CodeHash)
            .IsUnique();

        builder.HasIndex(
                pairingCode =>
                    pairingCode.GymId)
            .IsUnique()
            .HasFilter(
                "\"ConsumedAt\" IS NULL");

        builder.HasIndex(
            pairingCode => new
            {
                pairingCode.GymId,
                pairingCode.ExpiresAt
            });

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(
                pairingCode =>
                    pairingCode.CreatedByUserId)
            .OnDelete(
                DeleteBehavior.Restrict);
    }
}
