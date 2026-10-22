using GymFlow.Domain.Entities;
using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.IntegrationTests.Security;

// The former one-to-one relationship remains unique for existing identities,
// while new administrative students can exist without an app account.
public sealed class LegacyStudentIdentityModelTests
{
    [Fact]
    public void Student_KeepsUniqueOptionalUserAndOwnsNameAndTenant()
    {
        // Metadata only; this deliberately unusable destination is never opened.
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=unused.invalid;Database=unused").Options);
        var student = db.Model.FindEntityType(typeof(Student))!;
        var relationship = Assert.Single(student.GetForeignKeys(), f => f.PrincipalEntityType.ClrType == typeof(User));
        Assert.False(relationship.IsRequired);
        Assert.True(relationship.IsUnique);
        Assert.Equal(DeleteBehavior.SetNull, relationship.DeleteBehavior);
        Assert.True(student.FindProperty(nameof(Student.UserId))!.IsNullable);
        Assert.False(student.FindProperty(nameof(Student.GymId))!.IsNullable);
        Assert.False(student.FindProperty(nameof(Student.Name))!.IsNullable);
        Assert.True(student.FindProperty(nameof(Student.Email))!.IsNullable);
        Assert.Equal(false, student.FindProperty(nameof(Student.AppAccessEnabled))!.GetDefaultValue());
    }

    [Fact]
    public void LegacyCredentials_AreRequiredAndEmailIsGloballyUnique()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=unused.invalid;Database=unused").Options);
        var user = db.Model.FindEntityType(typeof(User))!;
        Assert.False(user.FindProperty(nameof(User.Email))!.IsNullable);
        Assert.False(user.FindProperty(nameof(User.PasswordHash))!.IsNullable);
        Assert.Contains(user.GetIndexes(), index => index.IsUnique &&
            index.Properties.Count == 1 && index.Properties[0].Name == nameof(User.Email));
    }
}
