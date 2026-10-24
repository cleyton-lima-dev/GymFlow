using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymFlow.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261023215000_OnePhysicalAccessPinPerStudent")]
public partial class OnePhysicalAccessPinPerStudent : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Existing duplicate PIN rows must be reviewed, never deleted silently.
        migrationBuilder.CreateIndex(
            name: "IX_PhysicalAccessCredentials_OnePinPerStudent",
            table: "PhysicalAccessCredentials",
            columns: new[] { "GymId", "StudentId" },
            unique: true,
            filter: "\"Type\" = 5");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_PhysicalAccessCredentials_OnePinPerStudent",
            table: "PhysicalAccessCredentials");
    }
}
