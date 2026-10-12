using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGymSettingsAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GymSettings",
                columns: table => new
                {
                    GymId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdministrativeName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GymSettings", x => x.GymId);
                });

            migrationBuilder.CreateTable(
                name: "GymSettingsAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GymId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousValues = table.Column<string>(type: "jsonb", nullable: false),
                    NewValues = table.Column<string>(type: "jsonb", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GymSettingsAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GymSettingsAuditLogs_GymSettings_GymId",
                        column: x => x.GymId,
                        principalTable: "GymSettings",
                        principalColumn: "GymId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GymSettingsAuditLogs_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GymSettingsAuditLogs_ActorUserId",
                table: "GymSettingsAuditLogs",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GymSettingsAuditLogs_GymId_OccurredAt",
                table: "GymSettingsAuditLogs",
                columns: new[] { "GymId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GymSettingsAuditLogs");

            migrationBuilder.DropTable(
                name: "GymSettings");
        }
    }
}
