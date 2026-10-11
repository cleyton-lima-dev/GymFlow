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
      'desiredDevices': [
        {
          'deviceKey': 'primary',
          'providerKey': 'toletus-litenet2',
          'enabled': false,
          'host': '192.168.0.50',
          'port': 7878,
        },
      ],
      'devices': [
        {
          'deviceKey': 'primary',
          'providerKey': 'toletus-litenet2',
          'enabled': true,
          'connected': true,
          'endpoint': '192.168.0.50:7878',
          'lastCredentialReadAt': '2026-10-10T15:00:30Z',
          'lastCredentialType': 1,
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

    expect(agent.desiredDevices, hasLength(1));
    expect(agent.desiredDevices.single.host, '192.168.0.50');
    expect(agent.desiredDevices.single.port, 7878);
    expect(agent.desiredDevices.single.enabled, isFalse);

    expect(agent.devices, hasLength(1));
    expect(agent.hasConnectedDevice, isTrue);
    expect(agent.devices.single.providerKey, 'toletus-litenet2');
    expect(agent.devices.single.lastCredentialType, 1);
    expect(
      agent.devices.single.lastCredentialReadAt,
      DateTime.parse('2026-10-10T15:00:30Z'),
    );
  });
}
