using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessAgentPairing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CredentialVersion",
                table: "AccessAgents",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.CreateTable(
                name: "AccessAgentPairingCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GymId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessAgentPairingCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccessAgentPairingCodes_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessAgentPairingCodes_CodeHash",
                table: "AccessAgentPairingCodes",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccessAgentPairingCodes_CreatedByUserId",
                table: "AccessAgentPairingCodes",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessAgentPairingCodes_GymId",
                table: "AccessAgentPairingCodes",
                column: "GymId",
                unique: true,
                filter: "\"ConsumedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AccessAgentPairingCodes_GymId_ExpiresAt",
                table: "AccessAgentPairingCodes",
                columns: new[] { "GymId", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessAgentPairingCodes");

            migrationBuilder.DropColumn(
                name: "CredentialVersion",
                table: "AccessAgents");
        }
    }
}
