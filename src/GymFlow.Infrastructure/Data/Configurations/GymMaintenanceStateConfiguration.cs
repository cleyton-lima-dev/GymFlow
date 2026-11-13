using GymFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymFlow.Infrastructure.Data.Configurations;

public sealed class GymMaintenanceStateConfiguration : IEntityTypeConfiguration<GymMaintenanceState>
{
    public void Configure(EntityTypeBuilder<GymMaintenanceState> builder)
    {
        builder.ToTable("GymMaintenanceStates", table => table.HasCheckConstraint(
            "CK_GymMaintenanceStates_AllowedAdmin", "(\"IsActive\" AND \"AllowedAdminUserId\" IS NOT NULL) OR (NOT \"IsActive\" AND \"AllowedAdminUserId\" IS NULL)"));
        builder.HasKey(x => x.GymId);
        builder.Property(x => x.GymId).ValueGeneratedNever();
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.AllowedAdminUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
