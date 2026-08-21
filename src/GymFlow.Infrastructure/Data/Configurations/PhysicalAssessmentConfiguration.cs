using GymFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymFlow.Infrastructure.Data.Configurations;

public class PhysicalAssessmentConfiguration
    : IEntityTypeConfiguration<PhysicalAssessment>
{
    public void Configure(EntityTypeBuilder<PhysicalAssessment> builder)
    {
        builder.ToTable("PhysicalAssessments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.AssessmentDate)
            .IsRequired();

        builder.Property(x => x.WeightKg)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(x => x.HeightCm)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(x => x.BodyFatPercentage)
            .HasPrecision(5, 2);

        builder.Property(x => x.ChestCm).HasPrecision(5, 2);
        builder.Property(x => x.WaistCm).HasPrecision(5, 2);
        builder.Property(x => x.AbdomenCm).HasPrecision(5, 2);
        builder.Property(x => x.HipCm).HasPrecision(5, 2);

        builder.Property(x => x.RightArmCm).HasPrecision(5, 2);
        builder.Property(x => x.LeftArmCm).HasPrecision(5, 2);

        builder.Property(x => x.RightThighCm).HasPrecision(5, 2);
        builder.Property(x => x.LeftThighCm).HasPrecision(5, 2);

        builder.Property(x => x.RightCalfCm).HasPrecision(5, 2);
        builder.Property(x => x.LeftCalfCm).HasPrecision(5, 2);

        builder.Property(x => x.Notes)
            .HasMaxLength(500);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        builder.HasOne(x => x.Student)
            .WithMany()
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.StudentId);

        builder.HasIndex(x => new { x.StudentId, x.AssessmentDate })
            .IsUnique();
    }
}