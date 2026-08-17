using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using GymFlow.Application.Interfaces.Security;
using GymFlow.Infrastructure.Security;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Infrastructure.Persistence.Repositories;


namespace GymFlow.Infrastructure.DependencyInjection;

public static class DependencyInjection

{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IStudentRepository, StudentRepository>();
        services.AddScoped<IExerciseRepository, ExerciseRepository>();
        services.AddScoped<IWorkoutTemplateRepository, WorkoutTemplateRepository>();
        services.AddScoped<IWorkoutRepository, WorkoutRepository>();
        services.AddScoped<
            IWorkoutExecutionRepository,
            WorkoutExecutionRepository>();
        services.AddScoped<ITokenService, JwtTokenService>();

        return services;

    }

}