using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymFlow.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261016170000_AddUniversalMigrationSessions")]
public partial class AddUniversalMigrationSessions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "Origin", table: "Charges", type: "integer", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<int>(name: "PaymentOrigin", table: "Charges", type: "integer", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<int>(name: "VerificationStatus", table: "Charges", type: "integer", nullable: false, defaultValue: 0);

        migrationBuilder.CreateTable(
            name: "MigrationSessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                GymId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                SourceNamespace = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ReleasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ReleasedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MigrationSessions", x => x.Id);
                table.ForeignKey("FK_MigrationSessions_Users_CreatedByUserId", x => x.CreatedByUserId, "Users", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_MigrationSessions_Users_ReleasedByUserId", x => x.ReleasedByUserId, "Users", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "MigrationBatches",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                MigrationSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                GymId = table.Column<Guid>(type: "uuid", nullable: false),
                EntityType = table.Column<int>(type: "integer", nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                PreviewJson = table.Column<string>(type: "jsonb", nullable: false),
                DecisionsJson = table.Column<string>(type: "jsonb", nullable: false),
                ResultJson = table.Column<string>(type: "jsonb", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ReleasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MigrationBatches", x => x.Id);
                table.ForeignKey("FK_MigrationBatches_MigrationSessions_MigrationSessionId", x => x.MigrationSessionId, "MigrationSessions", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "MigrationExternalReferences",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                GymId = table.Column<Guid>(type: "uuid", nullable: false),
                MigrationSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                SourceNamespace = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                EntityType = table.Column<int>(type: "integer", nullable: false),
                ExternalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                TargetEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MigrationExternalReferences", x => x.Id);
                table.ForeignKey("FK_MigrationExternalReferences_MigrationSessions_MigrationSessionId", x => x.MigrationSessionId, "MigrationSessions", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_MigrationSessions_CreatedByUserId", "MigrationSessions", "CreatedByUserId");
        migrationBuilder.CreateIndex("IX_MigrationSessions_ReleasedByUserId", "MigrationSessions", "ReleasedByUserId");
        migrationBuilder.CreateIndex("IX_MigrationSessions_GymId_Status", "MigrationSessions", new[] { "GymId", "Status" });
        migrationBuilder.CreateIndex("IX_MigrationSessions_ExpiresAt", "MigrationSessions", "ExpiresAt");
        migrationBuilder.CreateIndex("IX_MigrationBatches_MigrationSessionId_EntityType_Fingerprint", "MigrationBatches", new[] { "MigrationSessionId", "EntityType", "Fingerprint" }, unique: true);
        migrationBuilder.CreateIndex("IX_MigrationBatches_GymId_MigrationSessionId", "MigrationBatches", new[] { "GymId", "MigrationSessionId" });
        migrationBuilder.CreateIndex("IX_MigrationExternalReferences_MigrationSessionId", "MigrationExternalReferences", "MigrationSessionId");
        migrationBuilder.CreateIndex("IX_MigrationExternalReferences_GymId_SourceNamespace_EntityType_ExternalId", "MigrationExternalReferences", new[] { "GymId", "SourceNamespace", "EntityType", "ExternalId" }, unique: true);
        migrationBuilder.CreateIndex("IX_MigrationExternalReferences_GymId_TargetEntityId", "MigrationExternalReferences", new[] { "GymId", "TargetEntityId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "MigrationBatches");
        migrationBuilder.DropTable(name: "MigrationExternalReferences");
        migrationBuilder.DropTable(name: "MigrationSessions");
        migrationBuilder.DropColumn(name: "Origin", table: "Charges");
        migrationBuilder.DropColumn(name: "PaymentOrigin", table: "Charges");
        migrationBuilder.DropColumn(name: "VerificationStatus", table: "Charges");
    }
}
