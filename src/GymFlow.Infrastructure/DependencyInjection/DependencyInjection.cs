using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using GymFlow.Application.Interfaces.Security;
using GymFlow.Infrastructure.Security;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Persistence;
using GymFlow.Infrastructure.Persistence.Repositories;
using GymFlow.Infrastructure.Persistence;
using GymFlow.Application.Interfaces.Time;
using GymFlow.Infrastructure.Time;

namespace GymFlow.Infrastructure.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IGymErpPolicy>(new ConfigurationGymErpPolicy(configuration));

        var connectionString =
            configuration.GetConnectionString(
                "DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection não configurada.");
        }

        services.AddDbContext<AppDbContext>(
            options =>
                options.UseNpgsql(
                    connectionString));

        services.AddScoped<
            IApplicationTransaction,
            EfApplicationTransaction>();

        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IStudentRepository, StudentRepository>();
        services.AddScoped<IExerciseRepository, ExerciseRepository>();
        services.AddScoped<IPlanRepository, PlanRepository>();
        services.AddScoped<IEnrollmentRepository, EnrollmentRepository>();
        services.AddScoped<IChargeRepository, ChargeRepository>();
        services.AddScoped<IDashboardRepository, DashboardRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IFinancialAuditRepository, FinancialAuditRepository>();
        services.AddScoped<IPhysicalAccessCredentialRepository, PhysicalAccessCredentialRepository>();
        services.AddScoped<IPhysicalAccessOverrideRepository, PhysicalAccessOverrideRepository>();
        services.AddScoped<IPhysicalAccessEventRepository, PhysicalAccessEventRepository>();
        services.AddScoped<ICheckInRepository, CheckInRepository>();
        services.AddScoped<IAccessAgentRepository, AccessAgentRepository>();
        services.AddScoped<IAccessAgentAuditRepository, AccessAgentAuditRepository>();
        services.AddScoped<IAccessAgentPairingCodeRepository, AccessAgentPairingCodeRepository>();
        services.AddScoped<IWorkoutTemplateRepository, WorkoutTemplateRepository>();
        services.AddScoped<IWorkoutRepository, WorkoutRepository>();
        services.AddScoped<IPhysicalAssessmentRepository, PhysicalAssessmentRepository>();
        services.AddScoped<IWorkoutExecutionRepository, WorkoutExecutionRepository>();
        services.AddScoped<IWorkoutDayProgressRepository, WorkoutDayProgressRepository>();

        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IAccessAgentTokenService, AccessAgentJwtTokenService>();
        services.AddScoped<IAccessAgentSecretService, AccessAgentSecretService>();
        services.AddScoped<IAccessAgentPairingCodeService, AccessAgentPairingCodeService>();

        services.AddSingleton<IGymTimeZoneProvider, ConfigurationGymTimeZoneProvider>();

        return services;
    }
}
