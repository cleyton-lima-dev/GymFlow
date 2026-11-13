using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGymMaintenanceStateAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GymMaintenanceStates",
                columns: table => new
                {
                    GymId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    AllowedAdminUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GymMaintenanceStates", x => x.GymId);
                    table.CheckConstraint("CK_GymMaintenanceStates_AllowedAdmin", "(\"IsActive\" AND \"AllowedAdminUserId\" IS NOT NULL) OR (NOT \"IsActive\" AND \"AllowedAdminUserId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_GymMaintenanceStates_Users_AllowedAdminUserId",
                        column: x => x.AllowedAdminUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GymMaintenanceAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GymId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorPlatformOperatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    AllowedAdminUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GymMaintenanceAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GymMaintenanceAuditLogs_GymMaintenanceStates_GymId",
                        column: x => x.GymId,
                        principalTable: "GymMaintenanceStates",
                        principalColumn: "GymId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GymMaintenanceAuditLogs_PlatformOperators_ActorPlatformOper~",
                        column: x => x.ActorPlatformOperatorId,
                        principalTable: "PlatformOperators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GymMaintenanceAuditLogs_ActorPlatformOperatorId",
                table: "GymMaintenanceAuditLogs",
                column: "ActorPlatformOperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_GymMaintenanceAuditLogs_GymId_OccurredAt",
                table: "GymMaintenanceAuditLogs",
                columns: new[] { "GymId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GymMaintenanceStates_AllowedAdminUserId",
                table: "GymMaintenanceStates",
                column: "AllowedAdminUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GymMaintenanceAuditLogs");

            migrationBuilder.DropTable(
                name: "GymMaintenanceStates");
        }
    }
}
