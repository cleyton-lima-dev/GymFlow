using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessAgentControlPlane : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ConfigurationVersion",
                table: "AccessAgents",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReleaseEnabled",
                table: "AccessAgents",
                type: "boolean",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "AccessAgents"
                SET
                    "ConfigurationVersion" = 1,
                    "ReleaseEnabled" = FALSE
                WHERE
                    "ConfigurationVersion" IS NULL
                    OR "ReleaseEnabled" IS NULL;
                """);

            migrationBuilder.AlterColumn<long>(
                name: "ConfigurationVersion",
                table: "AccessAgents",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "ReleaseEnabled",
                table: "AccessAgents",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "AccessAgentAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GymId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccessAgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    PreviousValues = table.Column<string>(type: "jsonb", nullable: false),
                    NewValues = table.Column<string>(type: "jsonb", nullable: false),
                    OccurredAt = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_AccessAgentAuditLogs",
                        x => x.Id);

                    table.ForeignKey(
                        name: "FK_AccessAgentAuditLogs_AccessAgents_AccessAgentId",
                        column: x => x.AccessAgentId,
                        principalTable: "AccessAgents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);

                    table.ForeignKey(
                        name: "FK_AccessAgentAuditLogs_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessAgentAuditLogs_AccessAgentId_OccurredAt",
                table: "AccessAgentAuditLogs",
                columns: new[]
                {
                    "AccessAgentId",
                    "OccurredAt"
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessAgentAuditLogs_ActorUserId",
                table: "AccessAgentAuditLogs",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessAgentAuditLogs_GymId_OccurredAt",
                table: "AccessAgentAuditLogs",
                columns: new[]
                {
                    "GymId",
                    "OccurredAt"
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessAgentAuditLogs");

            migrationBuilder.DropColumn(
                name: "ConfigurationVersion",
                table: "AccessAgents");

            migrationBuilder.DropColumn(
                name: "ReleaseEnabled",
                table: "AccessAgents");
        }
    }
}
