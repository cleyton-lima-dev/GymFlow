using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkoutExecutionDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Cria temporariamente como nullable para preservar os dados existentes.
            migrationBuilder.AddColumn<DateOnly>(
                name: "ExecutionDate",
                table: "WorkoutExecutions",
                type: "date",
                nullable: true);

            // 2. Preenche os registros históricos com a data real da execução.
            migrationBuilder.Sql(
                """
        UPDATE "WorkoutExecutions"
        SET "ExecutionDate" = "CompletedAt"::date
        WHERE "ExecutionDate" IS NULL;
        """);

            // 3. Depois do backfill, torna a coluna obrigatória.
            migrationBuilder.AlterColumn<DateOnly>(
                name: "ExecutionDate",
                table: "WorkoutExecutions",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            // 4. Garante uma execução por WorkoutDay por dia.
            migrationBuilder.CreateIndex(
                name: "IX_WorkoutExecutions_WorkoutDayId_ExecutionDate",
                table: "WorkoutExecutions",
                columns: new[] { "WorkoutDayId", "ExecutionDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkoutExecutions_WorkoutDayId_ExecutionDate",
                table: "WorkoutExecutions");

            migrationBuilder.DropColumn(
                name: "ExecutionDate",
                table: "WorkoutExecutions");
        }
    }
}
