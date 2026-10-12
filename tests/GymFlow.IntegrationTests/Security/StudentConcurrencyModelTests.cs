using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GymFlow.IntegrationTests.Security;

public class StudentConcurrencyModelTests
{
    [Fact]
    public void ExistingActivityAndReasonColumnsRequireNoSchemaOperations()
    {
        // Compare PostgreSQL metadata only. No connection is opened and no
        // migrations, SQL commands or schema changes are executed.
        using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=unused.invalid;Database=unused")
            .Options);
        var snapshot = context.GetService<IMigrationsAssembly>().ModelSnapshot!;
        var previous = context.GetService<IModelRuntimeInitializer>().Initialize(snapshot.Model, designTime: true);
        var current = context.GetService<IDesignTimeModel>().Model;
        var differences = context.GetService<IMigrationsModelDiffer>().GetDifferences(
            previous.GetRelationalModel(), current.GetRelationalModel());

        Assert.Empty(differences);
        Assert.True(current.FindEntityType(typeof(GymFlow.Domain.Entities.User))!
            .FindProperty("IsActive")!.IsConcurrencyToken);
        Assert.True(current.FindEntityType(typeof(GymFlow.Domain.Entities.Student))!
            .FindProperty("InactivationReason")!.IsConcurrencyToken);
    }
}
