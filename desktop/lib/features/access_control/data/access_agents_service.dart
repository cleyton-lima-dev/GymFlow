import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/access_control/models/access_agent_pairing_code.dart';
import 'package:avelri_gestao/features/access_control/models/access_agent_summary.dart';

class AccessAgentsService {
  const AccessAgentsService(this._apiClient);

  final ApiClient _apiClient;

  Future<List<AccessAgentSummary>> getAgents() async {
    final response = await _apiClient.get('api/access-agents');

    if (response is! List) {
      throw const FormatException('Invalid access agents response.');
    }

    return response
        .map(
          (item) => AccessAgentSummary.fromJson(
            Map<String, dynamic>.from(item as Map),
          ),
        )
        .toList();
  }

  Future<AccessAgentPairingCode> createPairingCode() async {
    final response = await _apiClient.post(
      'api/access-agents/pairing-code',
      body: const {},
    );

    if (response is! Map) {
      throw const FormatException('Invalid pairing code response.');
    }

    return AccessAgentPairingCode.fromJson(Map<String, dynamic>.from(response));
  }
}
