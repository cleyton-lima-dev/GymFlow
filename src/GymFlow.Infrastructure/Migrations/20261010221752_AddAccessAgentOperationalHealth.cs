using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessAgentOperationalHealth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastFailureAt",
                table: "AccessAgents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastFailureCode",
                table: "AccessAgents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastOfflineSyncAt",
                table: "AccessAgents",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastFailureAt",
                table: "AccessAgents");

            migrationBuilder.DropColumn(
                name: "LastFailureCode",
                table: "AccessAgents");

            migrationBuilder.DropColumn(
                name: "LastOfflineSyncAt",
                table: "AccessAgents");
        }
    }
}
