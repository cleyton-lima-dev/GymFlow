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
        services.AddScoped<PlanService>();
        services.AddScoped<EnrollmentService>();
        services.AddScoped<ChargeService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<ReportService>();
        services.AddScoped<GymSettingsService>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<StudentAccessService>();
        services.AddScoped<PhysicalAccessService>();
        services.AddScoped<CheckInService>();
        services.AddScoped<PhysicalAccessCredentialService>();
        services.AddScoped<PhysicalAccessOverrideService>();
        services.AddScoped<AccessAgentService>();
        services.AddScoped<AccessAgentPairingService>();
        services.AddScoped<AccessAgentControlService>();
        services.AddScoped<StudentLifecycleService>();
        services.AddScoped<WorkoutTemplateService>();
        services.AddScoped<WorkoutService>();
        services.AddScoped<PhysicalAssessmentService>();

        return services;
    }
}
