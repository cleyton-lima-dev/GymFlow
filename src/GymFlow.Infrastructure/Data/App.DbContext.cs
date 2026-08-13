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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}