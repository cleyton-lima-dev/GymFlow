using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkoutDayProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkoutDayProgresses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkoutDayId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkoutDayProgresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkoutDayProgresses_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkoutDayProgresses_WorkoutDays_WorkoutDayId",
                        column: x => x.WorkoutDayId,
                        principalTable: "WorkoutDays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkoutExerciseCompletions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkoutDayProgressId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkoutExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkoutExerciseCompletions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkoutExerciseCompletions_WorkoutDayProgresses_WorkoutDayP~",
                        column: x => x.WorkoutDayProgressId,
                        principalTable: "WorkoutDayProgresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkoutExerciseCompletions_WorkoutExercises_WorkoutExercise~",
                        column: x => x.WorkoutExerciseId,
                        principalTable: "WorkoutExercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutDayProgresses_StudentId",
                table: "WorkoutDayProgresses",
                column: "StudentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutDayProgresses_WorkoutDayId",
                table: "WorkoutDayProgresses",
                column: "WorkoutDayId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutExerciseCompletions_WorkoutDayProgressId_WorkoutExer~",
                table: "WorkoutExerciseCompletions",
                columns: new[] { "WorkoutDayProgressId", "WorkoutExerciseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutExerciseCompletions_WorkoutExerciseId",
                table: "WorkoutExerciseCompletions",
                column: "WorkoutExerciseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkoutExerciseCompletions");

            migrationBuilder.DropTable(
                name: "WorkoutDayProgresses");
        }
    }
}
