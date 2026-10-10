import 'package:avelri_gestao/core/network/api_client.dart';
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
}
