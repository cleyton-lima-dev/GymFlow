import 'package:avelri_gestao/features/access_control/models/access_agent_summary.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('parses access agent operational status', () {
    final agent = AccessAgentSummary.fromJson({
      'id': 'agent-1',
      'gymId': 'gym-1',
      'name': 'Catraca principal',
      'machineName': 'RECEPCAO-01',
      'isActive': true,
      'releaseEnabled': false,
      'configurationVersion': 3,
      'appliedConfigurationVersion': 3,
      'configurationApplied': true,
      'isOnline': true,
      'pendingOfflineEvents': 2,
      'devices': [
        {
          'deviceKey': 'primary',
          'providerKey': 'toletus-litenet2',
          'enabled': true,
          'connected': true,
          'endpoint': '192.168.0.50:7878',
        },
      ],
      'lastOfflineSyncAt': '2026-10-10T15:00:00Z',
      'lastFailureAt': '2026-10-10T14:00:00Z',
      'lastFailureCode': 'OfflineSync.ApiUnavailable',
      'lastSeenAt': '2026-10-10T15:01:00Z',
      'createdAt': '2026-10-01T12:00:00Z',
      'updatedAt': '2026-10-10T15:01:00Z',
    });

    expect(agent.isOnline, isTrue);
    expect(agent.pendingOfflineEvents, 2);
    expect(agent.configurationApplied, isTrue);
    expect(agent.devices, hasLength(1));
    expect(agent.hasConnectedDevice, isTrue);
    expect(agent.devices.single.providerKey, 'toletus-litenet2');
  });
}
