using GymFlow.AccessAgent.Security;
using System.Net;
using System.Net.Http.Json;
using GymFlow.AccessAgent.Abstractions;
using System.Net.Http.Headers;
using GymFlow.AccessAgent.Offline;

namespace GymFlow.AccessAgent.Api;

public class AvelriAccessApiClient : IAvelriAccessApiClient
{
    private readonly HttpClient _httpClient;

    public AvelriAccessApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<AgentLoginResult?> LoginAsync(
        AgentCredentials credentials,
        CancellationToken cancellationToken)
    {
        var baseUrl =
            credentials.ApiBaseUrl.TrimEnd('/');

        var request = new AgentLoginRequest(
            credentials.AgentId,
            credentials.GymId,
            credentials.Secret);

        using var response =
            await _httpClient.PostAsJsonAsync(
                $"{baseUrl}/api/access-agents/login",
                request,
                cancellationToken);

        if (response.StatusCode ==
            HttpStatusCode.Unauthorized)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<AgentLoginResult>(
                cancellationToken);
    }

    public async Task<AgentAccessDecisionResult> DecideAsync(
    AgentCredentials credentials,
    string token,
    string providerKey,
    DeviceAccessAttempt attempt,
    CancellationToken cancellationToken)
    {
        var baseUrl =
            credentials.ApiBaseUrl.TrimEnd('/');

        var requestBody =
            new AccessDecisionRequest(
                attempt.RequestId,
                providerKey,
                (int)attempt.CredentialType,
                attempt.ExternalIdentifier,
                attempt.OccurredAt);

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                $"{baseUrl}/api/access-agent/physical-access/decisions");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        request.Content =
            JsonContent.Create(requestBody);

        using var response =
            await _httpClient.SendAsync(
                request,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        var result =
            await response.Content
                .ReadFromJsonAsync<AccessDecisionResponse>(
                    cancellationToken);

        if (result is null)
        {
            throw new InvalidOperationException(
                "A API retornou uma resposta de decisão de acesso inválida.");
        }

        return new AgentAccessDecisionResult(
            result.RequestId,
            result.Decision == 1,
            MapReason(result.Reason),
            result.ProcessedAt);
    }

    public async Task SyncOfflineEventAsync(
    AgentCredentials credentials,
    string token,
    PendingAccessEvent accessEvent,
    CancellationToken cancellationToken)
    {
        var baseUrl =
            credentials.ApiBaseUrl.TrimEnd('/');

        var requestBody =
            new SyncOfflineAccessEventRequest(
                accessEvent.RequestId,
                accessEvent.ProviderKey,
                (int)accessEvent.CredentialType,
                accessEvent.ExternalIdentifier,
                accessEvent.OccurredAt,
                accessEvent.Allowed ? 1 : 2,
                MapReasonCode(accessEvent.Reason),
                (int)accessEvent.Source,
                accessEvent.CreatedAt);

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                $"{baseUrl}/api/access-agent/physical-access/offline-events");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        request.Content =
            JsonContent.Create(requestBody);

        using var response =
            await _httpClient.SendAsync(
                request,
                cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    private static string MapReason(int reason)
    {
        return reason switch
        {
            1 => "Eligible",
            2 => "ManualOverride",
            100 => "CredentialNotFound",
            101 => "CredentialInactive",
            102 => "StudentInactive",
            103 => "StudentArchived",
            104 => "NoValidEnrollment",
            105 => "FinancialRestriction",
            106 => "ManualBlock",
            107 => "PlanRestriction",
            _ => $"Unknown:{reason}"
        };
    }

    private static int MapReasonCode(string reason)
    {
        return reason switch
        {
            "Eligible" => 1,
            "ManualOverride" => 2,
            "CredentialNotFound" => 100,
            "CredentialInactive" => 101,
            "StudentInactive" => 102,
            "StudentArchived" => 103,
            "NoValidEnrollment" => 104,
            "FinancialRestriction" => 105,
            "ManualBlock" => 106,
            "PlanRestriction" => 107,
            "OfflineNoCachedPermission" => 108,
            _ => throw new InvalidOperationException(
                $"Motivo de acesso offline desconhecido: {reason}")
        };
    }

    private sealed record AccessDecisionRequest(
        Guid RequestId,
        string ProviderKey,
        int CredentialType,
        string ExternalIdentifier,
        DateTime OccurredAt);

    private sealed record AccessDecisionResponse(
        Guid RequestId,
        int Decision,
        int Reason,
        Guid? StudentId,
        Guid? CredentialId,
        DateTime ProcessedAt);

    private sealed record AgentLoginRequest(
        Guid AgentId,
        Guid GymId,
        string Secret);

    private sealed record SyncOfflineAccessEventRequest(
    Guid RequestId,
    string ProviderKey,
    int CredentialType,
    string ExternalIdentifier,
    DateTime OccurredAt,
    int Decision,
    int Reason,
    int Source,
    DateTime ProcessedAt);
}