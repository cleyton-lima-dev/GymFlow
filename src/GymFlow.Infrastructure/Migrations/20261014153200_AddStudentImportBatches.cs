using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymFlow.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261014153200_AddStudentImportBatches")]
public partial class AddStudentImportBatches : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "StudentImportBatches",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                GymId = table.Column<Guid>(type: "uuid", nullable: false),
                Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                State = table.Column<int>(type: "integer", nullable: false),
                PayloadJson = table.Column<string>(type: "jsonb", nullable: true),
                PreviewJson = table.Column<string>(type: "jsonb", nullable: true),
                ResultJson = table.Column<string>(type: "jsonb", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_StudentImportBatches", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_StudentImportBatches_ExpiresAt",
            table: "StudentImportBatches",
            column: "ExpiresAt");

        migrationBuilder.CreateIndex(
            name: "IX_StudentImportBatches_GymId_Fingerprint",
            table: "StudentImportBatches",
            columns: new[] { "GymId", "Fingerprint" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "StudentImportBatches");
}
