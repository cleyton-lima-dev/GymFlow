using GymFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymFlow.Infrastructure.Data.Configurations;

public class AccessAgentConfiguration
    : IEntityTypeConfiguration<AccessAgent>
{
    public void Configure(
        EntityTypeBuilder<AccessAgent> builder)
    {
        builder.ToTable("AccessAgents");

        builder.HasKey(agent => agent.Id);

        builder.Property(agent => agent.GymId)
            .IsRequired();

        builder.Property(agent => agent.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(agent => agent.MachineName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(agent => agent.SecretHash)
            .IsRequired();

        builder.Property(agent => agent.IsActive)
            .IsRequired();

        builder.Property(agent => agent.ReleaseEnabled)
            .IsRequired();

        builder.Property(agent => agent.ConfigurationVersion)
            .IsRequired();

        builder.Property(agent => agent.CreatedAt)
            .IsRequired();

        builder.HasIndex(agent => agent.GymId);

        builder.HasIndex(agent => new
        {
            agent.GymId,
            agent.MachineName
        })
        .IsUnique();
    }
}
