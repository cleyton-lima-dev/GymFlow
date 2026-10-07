using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPhysicalAccessDecisionSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Source",
                table: "PhysicalAccessEvents",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(
                """
        UPDATE "PhysicalAccessEvents"
        SET "Source" = 1
        WHERE "Source" IS NULL;
        """);

            migrationBuilder.AlterColumn<int>(
                name: "Source",
                table: "PhysicalAccessEvents",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Source",
                table: "PhysicalAccessEvents");
        }
    }
}
