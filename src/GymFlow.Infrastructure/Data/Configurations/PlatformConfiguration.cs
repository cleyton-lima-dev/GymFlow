using GymFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymFlow.Infrastructure.Data.Configurations;

public sealed class PlatformOperatorConfiguration : IEntityTypeConfiguration<PlatformOperator>
{
    public void Configure(EntityTypeBuilder<PlatformOperator> b)
    {
        b.ToTable("PlatformOperators"); b.HasKey(x => x.Id);
        b.Property(x => x.Email).HasMaxLength(200).IsRequired(); b.HasIndex(x => x.Email).IsUnique();
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.PasswordHash).IsRequired(); b.Property(x => x.ProtectedTotpSecret).IsRequired();
        b.Property(x => x.LastTotpStep).IsConcurrencyToken(); b.Property(x => x.FailedAttempts).IsConcurrencyToken();
        b.Property(x => x.LockedUntil).IsConcurrencyToken(); b.Property(x => x.IsActive).IsConcurrencyToken();
        b.Property(x => x.CredentialVersion).IsConcurrencyToken();
    }
}

public sealed class GymOnboardingConfiguration : IEntityTypeConfiguration<GymOnboarding>
{
    public void Configure(EntityTypeBuilder<GymOnboarding> b)
    {
        b.ToTable("GymOnboardings"); b.HasKey(x => x.GymId);
        b.Property(x => x.IdempotencyKey).HasMaxLength(36).IsRequired();
        b.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.PhysicalValidationEvidence).HasMaxLength(1000);
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => new { x.OperatorId, x.IdempotencyKey }).IsUnique(); b.HasIndex(x => x.AdminId).IsUnique();
        b.HasIndex(x => x.CreatedAt);
        b.HasOne<PlatformOperator>().WithMany().HasForeignKey(x => x.OperatorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.AdminId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<GymSettings>().WithOne().HasForeignKey<GymOnboarding>(x => x.GymId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AdminActivationInvitationConfiguration : IEntityTypeConfiguration<AdminActivationInvitation>
{
    public void Configure(EntityTypeBuilder<AdminActivationInvitation> b)
    {
        b.ToTable("AdminActivationInvitations"); b.HasKey(x => x.Id);
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired(); b.HasIndex(x => x.TokenHash).IsUnique();
        b.Property(x => x.ConsumedAt).IsConcurrencyToken(); b.Property(x => x.RevokedAt).IsConcurrencyToken();
        b.HasIndex(x => x.GymId);
        b.HasOne<GymOnboarding>().WithMany().HasForeignKey(x => x.GymId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.AdminId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class GymOnboardingAuditConfiguration : IEntityTypeConfiguration<GymOnboardingAuditLog>
{
    public void Configure(EntityTypeBuilder<GymOnboardingAuditLog> b)
    {
        b.ToTable("GymOnboardingAuditLogs"); b.HasKey(x => x.Id);
        b.Property(x => x.ActorType).HasMaxLength(20).IsRequired(); b.Property(x => x.Action).HasMaxLength(60).IsRequired();
        b.Property(x => x.Changes).HasColumnType("jsonb").IsRequired();
        b.HasIndex(x => new { x.GymId, x.OccurredAt });
        b.HasOne<GymOnboarding>().WithMany().HasForeignKey(x => x.GymId).OnDelete(DeleteBehavior.Restrict);
    }
}
