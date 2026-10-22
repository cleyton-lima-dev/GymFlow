using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeparateStudentIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Students_Users_UserId",
                table: "Students");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "Students",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<bool>(
                name: "AppAccessEnabled",
                table: "Students",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "AppInviteExpiresAt",
                table: "Students",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AppInviteTokenHash",
                table: "Students",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Students",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GymId",
                table: "Students",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Students",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Students",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            // Copy identity and legacy access state without changing User authentication state.
            migrationBuilder.Sql("""
                UPDATE "Students" AS s
                SET "GymId" = u."GymId",
                    "Name" = u."Name",
                    "Email" = u."Email",
                    "IsActive" = CASE
                        WHEN s."InactivationReason" = 1 THEN FALSE
                        WHEN s."InactivationReason" = 2 THEN TRUE
                        ELSE u."IsActive" END,
                    "AppAccessEnabled" = u."IsActive"
                        AND s."InactivationReason" IS DISTINCT FROM 1
                        AND s."ArchivedAt" IS NULL
                FROM "Users" AS u
                WHERE s."UserId" = u."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Students_AppInviteTokenHash",
                table: "Students",
                column: "AppInviteTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_GymId",
                table: "Students",
                column: "GymId");

            migrationBuilder.AddForeignKey(
                name: "FK_Students_Users_UserId",
                table: "Students",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "Students" WHERE "UserId" IS NULL) THEN
                        RAISE EXCEPTION 'Cannot restore mandatory Student.UserId while administrative-only students exist';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_Students_Users_UserId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_AppInviteTokenHash",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_GymId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "AppAccessEnabled",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "AppInviteExpiresAt",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "AppInviteTokenHash",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "GymId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Students");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "Students",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Students_Users_UserId",
                table: "Students",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
