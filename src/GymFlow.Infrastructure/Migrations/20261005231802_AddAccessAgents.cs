using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessAgents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccessAgents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GymId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    MachineName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SecretHash = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessAgents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessAgents_GymId",
                table: "AccessAgents",
                column: "GymId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessAgents_GymId_MachineName",
                table: "AccessAgents",
                columns: new[] { "GymId", "MachineName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessAgents");
        }
    }
}
