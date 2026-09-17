using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "Students",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InactivatedAt",
                table: "Students",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InactivationReason",
                table: "Students",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "NoValidEnrollmentSince",
                table: "Students",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_ArchivedAt",
                table: "Students",
                column: "ArchivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Students_NoValidEnrollmentSince",
                table: "Students",
                column: "NoValidEnrollmentSince");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Students_ArchivedAt",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_NoValidEnrollmentSince",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "InactivatedAt",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "InactivationReason",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "NoValidEnrollmentSince",
                table: "Students");
        }
    }
}
