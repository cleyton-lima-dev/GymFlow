import 'dart:convert';

import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/access_control/data/access_agents_service.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

void main() {
  test('configureToletus sends communication enabled separately from physical release', () async {
    final client = MockClient((request) async {
      expect(request.method, 'PATCH');
      expect(request.url.path, '/api/access-agents/agent-1/devices');

      final body = jsonDecode(request.body) as Map<String, dynamic>;

      final devices = body['devices'] as List<dynamic>;

      final device = devices.single as Map<String, dynamic>;

      expect(device['deviceKey'], 'primary');

      expect(device['providerKey'], 'toletus-litenet2');

      expect(device['enabled'], isTrue);

      expect(device['host'], '192.168.0.250');

      expect(device['port'], 7878);

      return http.Response(
        jsonEncode({
          'id': 'agent-1',
          'gymId': 'gym-1',
          'name': 'Avelri Access Agent',
          'machineName': 'PC-01',
          'isActive': true,
          'releaseEnabled': false,
          'configurationVersion': 2,
          'appliedConfigurationVersion': 1,
          'configurationApplied': false,
          'isOnline': true,
          'pendingOfflineEvents': 0,
          'desiredDevices': [
            {
              'deviceKey': 'primary',
              'providerKey': 'toletus-litenet2',
              'enabled': true,
              'host': '192.168.0.250',
              'port': 7878,
            },
          ],
          'devices': [],
          'lastOfflineSyncAt': null,
          'lastFailureAt': null,
          'lastFailureCode': null,
          'lastSeenAt': null,
          'createdAt': '2026-10-10T00:00:00Z',
          'updatedAt': null,
        }),
        200,
        headers: {'content-type': 'application/json'},
      );
    });

    final apiClient = ApiClient(
      baseUrl: 'http://localhost:5137/',
      client: client,
    );

    final service = AccessAgentsService(apiClient);

    final result = await service.configureToletus(
      agentId: 'agent-1',
      host: '192.168.0.250',
      port: 7878,
      enabled: true,
    );

    expect(result.releaseEnabled, isFalse);

    expect(result.desiredDevices.single.enabled, isTrue);

    apiClient.close();
  });
}
