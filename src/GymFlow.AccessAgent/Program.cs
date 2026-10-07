using GymFlow.AccessAgent;
using Microsoft.Extensions.Hosting.WindowsServices;
using GymFlow.AccessAgent.Security;
using GymFlow.AccessAgent.Api;
using GymFlow.AccessAgent.Services;
using GymFlow.AccessAgent.Abstractions;
using GymFlow.AccessAgent.Adapters.Toletus;
using GymFlow.AccessAgent.Offline;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Avelri Access Agent";
});

builder.Services.AddHttpClient<IAvelriAccessApiClient, AvelriAccessApiClient>();
builder.Services.AddSingleton<IAgentCredentialStore, WindowsAgentCredentialStore>();
builder.Services.AddSingleton<ILocalSensitiveDataProtector,WindowsLocalSensitiveDataProtector>();
builder.Services.AddSingleton<IAccessDecisionService, AccessDecisionService>();
builder.Services.AddSingleton<IAgentSessionService, AgentSessionService>();
builder.Services.Configure<ToletusLiteNet2Options>(
    builder.Configuration.GetSection(ToletusLiteNet2Options.SectionName));
builder.Services.AddSingleton<IAccessDeviceAdapter,ToletusLiteNet2Adapter>();
builder.Services.Configure<AccessOfflineStoreOptions>(
    builder.Configuration.GetSection(AccessOfflineStoreOptions.SectionName));
builder.Services.AddSingleton<IAccessOfflineStore,SqliteAccessOfflineStore>();
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<OfflineSyncWorker>();
builder.Services.AddSingleton<IOfflineEventSyncService, OfflineEventSyncService>();

var host = builder.Build();
host.Run();