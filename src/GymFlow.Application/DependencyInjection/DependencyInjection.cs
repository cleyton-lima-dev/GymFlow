using GymFlow.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GymFlow.Application.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        services.AddScoped<AuthenticationService>();
        services.AddScoped<StudentService>();
        services.AddScoped<ExerciseService>();
        services.AddScoped<WorkoutTemplateService>();
        services.AddScoped<WorkoutService>();
        services.AddScoped<PhysicalAssessmentService>();


        return services;
    }
}