using Microsoft.EntityFrameworkCore;
using GymFlow.Domain.Entities;

namespace GymFlow.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Student> Students { get; set; }
    public DbSet<Exercise> Exercises { get; set; }


    public DbSet<WorkoutTemplate> WorkoutTemplates { get; set; }
    public DbSet<WorkoutTemplateDay> WorkoutTemplateDays { get; set; }
    public DbSet<WorkoutTemplateExercise> WorkoutTemplateExercises { get; set; }


    public DbSet<Workout> Workouts { get; set; }
    public DbSet<WorkoutDay> WorkoutDays { get; set; }
    public DbSet<WorkoutExercise> WorkoutExercises { get; set; }
    public DbSet<WorkoutExecution> WorkoutExecutions { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}