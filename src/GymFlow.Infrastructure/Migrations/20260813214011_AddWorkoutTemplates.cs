using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkoutTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkoutTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GymId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "citext", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkoutTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkoutTemplateDays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkoutTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkoutTemplateDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkoutTemplateDays_WorkoutTemplates_WorkoutTemplateId",
                        column: x => x.WorkoutTemplateId,
                        principalTable: "WorkoutTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkoutTemplateExercises",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkoutTemplateDayId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sets = table.Column<int>(type: "integer", nullable: false),
                    Repetitions = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RestSeconds = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkoutTemplateExercises", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkoutTemplateExercises_Exercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalTable: "Exercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkoutTemplateExercises_WorkoutTemplateDays_WorkoutTemplat~",
                        column: x => x.WorkoutTemplateDayId,
                        principalTable: "WorkoutTemplateDays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutTemplateDays_WorkoutTemplateId",
                table: "WorkoutTemplateDays",
                column: "WorkoutTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutTemplateDays_WorkoutTemplateId_Order",
                table: "WorkoutTemplateDays",
                columns: new[] { "WorkoutTemplateId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutTemplateExercises_ExerciseId",
                table: "WorkoutTemplateExercises",
                column: "ExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutTemplateExercises_WorkoutTemplateDayId",
                table: "WorkoutTemplateExercises",
                column: "WorkoutTemplateDayId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutTemplateExercises_WorkoutTemplateDayId_Order",
                table: "WorkoutTemplateExercises",
                columns: new[] { "WorkoutTemplateDayId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutTemplates_GymId",
                table: "WorkoutTemplates",
                column: "GymId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutTemplates_GymId_Name",
                table: "WorkoutTemplates",
                columns: new[] { "GymId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkoutTemplateExercises");

            migrationBuilder.DropTable(
                name: "WorkoutTemplateDays");

            migrationBuilder.DropTable(
                name: "WorkoutTemplates");
        }
    }
}
