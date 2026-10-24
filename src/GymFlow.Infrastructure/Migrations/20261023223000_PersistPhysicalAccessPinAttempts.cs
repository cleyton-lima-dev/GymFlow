using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymFlow.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261023223000_PersistPhysicalAccessPinAttempts")]
public partial class PersistPhysicalAccessPinAttempts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PhysicalAccessPinAttemptStates",
            columns: table => new
            {
                GymId = table.Column<Guid>(type: "uuid", nullable: false),
                WindowStart = table.Column<long>(type: "bigint", nullable: false),
                Failures = table.Column<int>(type: "integer", nullable: false),
                Pending = table.Column<int>(type: "integer", nullable: false),
                LockedUntil = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_PhysicalAccessPinAttemptStates", x => x.GymId));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "PhysicalAccessPinAttemptStates");
    }
}
