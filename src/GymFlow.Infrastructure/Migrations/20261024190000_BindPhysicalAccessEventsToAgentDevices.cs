using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BindPhysicalAccessEventsToAgentDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AccessAgentId",
                table: "PhysicalAccessEvents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceKey",
                table: "PhysicalAccessEvents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalAccessEvents_AccessAgentId",
                table: "PhysicalAccessEvents",
                column: "AccessAgentId");

            migrationBuilder.AddForeignKey(
                name: "FK_PhysicalAccessEvents_AccessAgents_AccessAgentId",
                table: "PhysicalAccessEvents",
                column: "AccessAgentId",
                principalTable: "AccessAgents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PhysicalAccessEvents_AccessAgents_AccessAgentId",
                table: "PhysicalAccessEvents");

            migrationBuilder.DropIndex(
                name: "IX_PhysicalAccessEvents_AccessAgentId",
                table: "PhysicalAccessEvents");

            migrationBuilder.DropColumn(
                name: "AccessAgentId",
                table: "PhysicalAccessEvents");

            migrationBuilder.DropColumn(
                name: "DeviceKey",
                table: "PhysicalAccessEvents");
        }
    }
}
