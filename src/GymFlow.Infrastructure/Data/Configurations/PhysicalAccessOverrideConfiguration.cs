using GymFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymFlow.Infrastructure.Data.Configurations;

public class PhysicalAccessOverrideConfiguration
    : IEntityTypeConfiguration<PhysicalAccessOverride>
{
    public void Configure(
        EntityTypeBuilder<PhysicalAccessOverride> builder)
    {
        builder.ToTable("PhysicalAccessOverrides");

        builder.HasKey(accessOverride => accessOverride.Id);

        builder.Property(accessOverride => accessOverride.GymId)
            .IsRequired();

        builder.Property(accessOverride => accessOverride.StudentId)
            .IsRequired();

        builder.Property(accessOverride => accessOverride.Type)
            .IsRequired();

        builder.Property(accessOverride => accessOverride.Reason)
            .HasMaxLength(500);

        builder.Property(accessOverride => accessOverride.ActorUserId)
            .IsRequired();

        builder.Property(accessOverride => accessOverride.CreatedAt)
            .IsRequired();

        builder.HasOne(accessOverride => accessOverride.Student)
            .WithMany()
            .HasForeignKey(accessOverride => accessOverride.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(accessOverride => accessOverride.ActorUser)
            .WithMany()
            .HasForeignKey(accessOverride => accessOverride.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(accessOverride => accessOverride.GymId);

        builder.HasIndex(accessOverride => new
        {
            accessOverride.GymId,
            accessOverride.StudentId
        })
        .IsUnique();
    }
}