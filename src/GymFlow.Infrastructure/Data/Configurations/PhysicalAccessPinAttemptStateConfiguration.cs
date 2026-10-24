using GymFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymFlow.Infrastructure.Data.Configurations;

public sealed class PhysicalAccessPinAttemptStateConfiguration
    : IEntityTypeConfiguration<PhysicalAccessPinAttemptState>
{
    public void Configure(EntityTypeBuilder<PhysicalAccessPinAttemptState> builder)
    {
        builder.ToTable("PhysicalAccessPinAttemptStates");
        builder.HasKey(state => state.GymId);
        builder.Property(state => state.GymId).ValueGeneratedNever();
    }
}
