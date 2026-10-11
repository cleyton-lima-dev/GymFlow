using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessAgentRuntimeStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeviceStatusesJson",
                table: "AccessAgents",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PendingOfflineEvents",
                table: "AccessAgents",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeviceStatusesJson",
                table: "AccessAgents");

            migrationBuilder.DropColumn(
                name: "PendingOfflineEvents",
                table: "AccessAgents");
        }
    }
}
