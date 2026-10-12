using GymFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace GymFlow.Infrastructure.Data.Configurations;

public sealed class GymSettingsConfiguration : IEntityTypeConfiguration<GymSettings>
{
    public void Configure(EntityTypeBuilder<GymSettings> builder)
    {
        builder.ToTable("GymSettings");
        builder.HasKey(x => x.GymId);
        builder.Property(x => x.GymId).ValueGeneratedNever();
        builder.Property(x => x.AdministrativeName).IsRequired().HasMaxLength(150);
        builder.Property(x => x.TimeZoneId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Version).IsConcurrencyToken();
    }
}
