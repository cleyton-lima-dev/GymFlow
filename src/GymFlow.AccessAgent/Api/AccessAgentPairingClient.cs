using System.Net;
using System.Net.Http.Json;

namespace GymFlow.AccessAgent.Api;

public class AccessAgentPairingClient :
    IAccessAgentPairingClient
{
    private readonly HttpClient _httpClient;

    public AccessAgentPairingClient(
        HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PairAccessAgentResult?> PairAsync(
        string apiBaseUrl,
        string pairingCode,
        string machineName,
        CancellationToken cancellationToken)
    {
        var baseUrl =
            apiBaseUrl.TrimEnd('/');

        var request =
            new PairRequest(
                pairingCode,
                machineName);

        using var response =
            await _httpClient.PostAsJsonAsync(
                $"{baseUrl}/api/access-agents/pair",
                request,
                cancellationToken);

        if (response.StatusCode ==
            HttpStatusCode.Unauthorized)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<PairAccessAgentResult>(
                cancellationToken);
    }

    private sealed record PairRequest(
        string Code,
        string MachineName);
}
