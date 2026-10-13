using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlatformOperators",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    ProtectedTotpSecret = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CredentialVersion = table.Column<long>(type: "bigint", nullable: false),
                    LastTotpStep = table.Column<long>(type: "bigint", nullable: false),
                    FailedAttempts = table.Column<int>(type: "integer", nullable: false),
                    LockedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformOperators", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GymOnboardings",
                columns: table => new
                {
                    GymId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PhysicalAccessContracted = table.Column<bool>(type: "boolean", nullable: false),
                    ConfigurationConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PhysicalValidationEvidence = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AdminActivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GymOnboardings", x => x.GymId);
                    table.ForeignKey(
                        name: "FK_GymOnboardings_GymSettings_GymId",
                        column: x => x.GymId,
                        principalTable: "GymSettings",
                        principalColumn: "GymId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GymOnboardings_PlatformOperators_OperatorId",
                        column: x => x.OperatorId,
                        principalTable: "PlatformOperators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GymOnboardings_Users_AdminId",
                        column: x => x.AdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AdminActivationInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GymId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminActivationInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminActivationInvitations_GymOnboardings_GymId",
                        column: x => x.GymId,
                        principalTable: "GymOnboardings",
                        principalColumn: "GymId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdminActivationInvitations_Users_AdminId",
                        column: x => x.AdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GymOnboardingAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GymId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Action = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Changes = table.Column<string>(type: "jsonb", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GymOnboardingAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GymOnboardingAuditLogs_GymOnboardings_GymId",
                        column: x => x.GymId,
                        principalTable: "GymOnboardings",
                        principalColumn: "GymId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminActivationInvitations_AdminId",
                table: "AdminActivationInvitations",
                column: "AdminId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminActivationInvitations_GymId",
                table: "AdminActivationInvitations",
                column: "GymId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminActivationInvitations_TokenHash",
                table: "AdminActivationInvitations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GymOnboardingAuditLogs_GymId_OccurredAt",
                table: "GymOnboardingAuditLogs",
                columns: new[] { "GymId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GymOnboardings_AdminId",
                table: "GymOnboardings",
                column: "AdminId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GymOnboardings_CreatedAt",
                table: "GymOnboardings",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_GymOnboardings_OperatorId_IdempotencyKey",
                table: "GymOnboardings",
                columns: new[] { "OperatorId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformOperators_Email",
                table: "PlatformOperators",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminActivationInvitations");

            migrationBuilder.DropTable(
                name: "GymOnboardingAuditLogs");

            migrationBuilder.DropTable(
                name: "GymOnboardings");

            migrationBuilder.DropTable(
                name: "PlatformOperators");
        }
    }
}
