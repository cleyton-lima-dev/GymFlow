using GymFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymFlow.Infrastructure.Data.Configurations;

public class PhysicalAccessCredentialConfiguration
    : IEntityTypeConfiguration<PhysicalAccessCredential>
{
    public void Configure(
        EntityTypeBuilder<PhysicalAccessCredential> builder)
    {
        builder.ToTable("PhysicalAccessCredentials");

        builder.HasKey(credential => credential.Id);

        builder.Property(credential => credential.GymId)
            .IsRequired();

        builder.Property(credential => credential.StudentId)
            .IsRequired();

        builder.Property(credential => credential.Type)
            .IsRequired();

        builder.Property(credential => credential.ProviderKey)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnType("citext");

        builder.Property(credential => credential.ExternalIdentifier)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(credential => credential.IsActive)
            .IsRequired();

        builder.Property(credential => credential.CreatedAt)
            .IsRequired();

        builder.HasOne(credential => credential.Student)
            .WithMany()
            .HasForeignKey(credential => credential.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(credential => credential.StudentId);

        builder.HasIndex(credential => credential.GymId);

        builder.HasIndex(credential => new
        {
            credential.GymId,
            credential.ProviderKey,
            credential.Type,
            credential.ExternalIdentifier
        })
        .IsUnique();
    }
}