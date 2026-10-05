using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPhysicalAccessControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PhysicalAccessCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GymId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    ProviderKey = table.Column<string>(type: "citext", maxLength: 100, nullable: false),
                    ExternalIdentifier = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhysicalAccessCredentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhysicalAccessCredentials_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PhysicalAccessOverrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GymId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhysicalAccessOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhysicalAccessOverrides_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhysicalAccessOverrides_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PhysicalAccessEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GymId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CredentialId = table.Column<Guid>(type: "uuid", nullable: true),
                    Decision = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<int>(type: "integer", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhysicalAccessEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhysicalAccessEvents_PhysicalAccessCredentials_CredentialId",
                        column: x => x.CredentialId,
                        principalTable: "PhysicalAccessCredentials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhysicalAccessEvents_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalAccessCredentials_GymId",
                table: "PhysicalAccessCredentials",
                column: "GymId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalAccessCredentials_GymId_ProviderKey_Type_ExternalId~",
                table: "PhysicalAccessCredentials",
                columns: new[] { "GymId", "ProviderKey", "Type", "ExternalIdentifier" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalAccessCredentials_StudentId",
                table: "PhysicalAccessCredentials",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalAccessEvents_CredentialId",
                table: "PhysicalAccessEvents",
                column: "CredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalAccessEvents_GymId_OccurredAt",
                table: "PhysicalAccessEvents",
                columns: new[] { "GymId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalAccessEvents_GymId_RequestId",
                table: "PhysicalAccessEvents",
                columns: new[] { "GymId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalAccessEvents_StudentId_OccurredAt",
                table: "PhysicalAccessEvents",
                columns: new[] { "StudentId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalAccessOverrides_ActorUserId",
                table: "PhysicalAccessOverrides",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalAccessOverrides_GymId",
                table: "PhysicalAccessOverrides",
                column: "GymId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalAccessOverrides_GymId_StudentId",
                table: "PhysicalAccessOverrides",
                columns: new[] { "GymId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalAccessOverrides_StudentId",
                table: "PhysicalAccessOverrides",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PhysicalAccessEvents");

            migrationBuilder.DropTable(
                name: "PhysicalAccessOverrides");

            migrationBuilder.DropTable(
                name: "PhysicalAccessCredentials");
        }
    }
}
