using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckIns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CheckIns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GymId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    PhysicalAccessEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecordedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckIns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CheckIns_PhysicalAccessEvents_PhysicalAccessEventId",
                        column: x => x.PhysicalAccessEventId,
                        principalTable: "PhysicalAccessEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CheckIns_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CheckIns_Users_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CheckIns_GymId_OccurredAt",
                table: "CheckIns",
                columns: new[] { "GymId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CheckIns_GymId_RequestId",
                table: "CheckIns",
                columns: new[] { "GymId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CheckIns_GymId_StudentId_OccurredAt",
                table: "CheckIns",
                columns: new[] { "GymId", "StudentId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CheckIns_PhysicalAccessEventId",
                table: "CheckIns",
                column: "PhysicalAccessEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CheckIns_RecordedByUserId",
                table: "CheckIns",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckIns_StudentId",
                table: "CheckIns",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CheckIns");
        }
    }
}
