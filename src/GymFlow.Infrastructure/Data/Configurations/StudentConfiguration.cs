using GymFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymFlow.Infrastructure.Data.Configurations;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("Students");

        builder.HasKey(student => student.Id);

        builder.Property(student => student.Phone)
            .HasMaxLength(20);

        builder.HasOne(student => student.User)
            .WithOne()
            .HasForeignKey<Student>(student => student.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(student => student.UserId)
            .IsUnique();
    }
}