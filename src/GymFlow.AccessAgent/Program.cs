using GymFlow.AccessAgent;
using GymFlow.AccessAgent.Abstractions;
using GymFlow.AccessAgent.Adapters.Toletus;
using GymFlow.AccessAgent.Api;
using GymFlow.AccessAgent.ControlPlane;
using GymFlow.AccessAgent.Offline;
using GymFlow.AccessAgent.Security;
using GymFlow.AccessAgent.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting.WindowsServices;

var pairingMode =
    AgentPairingBootstrap
        .IsPairingRequested(args);

var hostArgs =
    pairingMode
        ? args
            .Where(
                argument =>
                    !string.Equals(
                        argument,
                        "--pair",
                        StringComparison.OrdinalIgnoreCase))
            .ToArray()
        : args;

var builder =
    Host.CreateApplicationBuilder(
        hostArgs);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName =
        "Avelri Access Agent";
});

builder.Services
    .AddHttpClient<
        IAvelriAccessApiClient,
        AvelriAccessApiClient>();

builder.Services
    .AddSingleton<
        IAgentCredentialStore,
        WindowsAgentCredentialStore>();

builder.Services
    .AddSingleton<
        ILocalSensitiveDataProtector,
        WindowsLocalSensitiveDataProtector>();

builder.Services
    .AddSingleton<
        IAccessDecisionService,
        AccessDecisionService>();

builder.Services
    .AddSingleton<
        IAgentSessionService,
        AgentSessionService>();

builder.Services
    .AddSingleton<
        IAccessOperationalHealthStore,
        InMemoryAccessOperationalHealthStore>();

builder.Services.Configure<ToletusLiteNet2Options>(
    builder.Configuration.GetSection(
        ToletusLiteNet2Options.SectionName));

builder.Services
    .AddSingleton<
        IAccessDeviceAdapter,
        ToletusLiteNet2Adapter>();

builder.Services.Configure<AccessOfflineStoreOptions>(
    builder.Configuration.GetSection(
        AccessOfflineStoreOptions.SectionName));

builder.Services
    .AddSingleton<
        IAccessOfflineStore,
        SqliteAccessOfflineStore>();

builder.Services
    .AddSingleton<
        IAccessReleaseControl,
        SqliteAccessReleaseControl>();

builder.Services.Configure<AccessControlPlaneOptions>(
    builder.Configuration.GetSection(
        AccessControlPlaneOptions.SectionName));

builder.Services
    .AddSingleton<
        IAccessControlPlaneSyncService,
        AccessControlPlaneSyncService>();

builder.Services
    .AddSingleton<
        IOfflineEventSyncService,
        OfflineEventSyncService>();

builder.Services
    .AddHostedService<Worker>();

builder.Services
    .AddHostedService<OfflineSyncWorker>();

builder.Services
    .AddHostedService<
        AccessControlPlaneSyncWorker>();

var host =
    builder.Build();

if (pairingMode)
{
    var credentialStore =
        host.Services
            .GetRequiredService<
                IAgentCredentialStore>();

    Environment.ExitCode =
        await AgentPairingBootstrap
            .RunAsync(
                credentialStore,
                CancellationToken.None);

    return;
}

host.Run();
