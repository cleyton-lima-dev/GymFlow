import 'package:avelri_gestao/features/access_control/models/access_agent_summary.dart';
import 'package:avelri_gestao/features/access_control/presentation/credential_read_probe.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('returns a credential read newer than the baseline', () async {
    final baseline = DateTime.utc(2026, 10, 10, 12);

    var calls = 0;

    final probe = CredentialReadProbe(() async {
      calls++;

      return [
        _agent(
          readAt: calls == 1
              ? baseline
              : baseline.add(const Duration(seconds: 5)),
          credentialType: 5,
        ),
      ];
    });

    final result = await probe.waitForNextRead(
      agentId: 'agent-1',
      baseline: baseline,
      timeout: const Duration(milliseconds: 100),
      pollInterval: const Duration(milliseconds: 1),
    );

    expect(result, isNotNull);
    expect(result!.credentialType, 5);
    expect(result.readAt, baseline.add(const Duration(seconds: 5)));
  });

  test('ignores a credential read equal to the baseline', () async {
    final baseline = DateTime.utc(2026, 10, 10, 12);

    final probe = CredentialReadProbe(
      () async => [_agent(readAt: baseline, credentialType: 1)],
    );

    final result = await probe.waitForNextRead(
      agentId: 'agent-1',
      baseline: baseline,
      timeout: const Duration(milliseconds: 10),
      pollInterval: const Duration(milliseconds: 1),
    );

    expect(result, isNull);
  });

  test('latestReadAt returns the newest device telemetry', () {
    final older = DateTime.utc(2026, 10, 10, 12);
    final newer = older.add(const Duration(minutes: 1));

    final agent = _agentWithDevices([
      _device(readAt: older, credentialType: 1),
      _device(readAt: newer, credentialType: 3),
    ]);

    expect(CredentialReadProbe.latestReadAt(agent), newer);
  });
}

AccessAgentSummary _agent({
  required DateTime readAt,
  required int credentialType,
}) {
  return _agentWithDevices([
    _device(readAt: readAt, credentialType: credentialType),
  ]);
}

AccessAgentSummary _agentWithDevices(List<AccessDeviceStatus> devices) {
  return AccessAgentSummary(
    id: 'agent-1',
    gymId: 'gym-1',
    name: 'Avelri Access Agent',
    machineName: 'PC-01',
    isActive: true,
    releaseEnabled: false,
    configurationVersion: 1,
    appliedConfigurationVersion: 1,
    configurationApplied: true,
    isOnline: true,
    pendingOfflineEvents: 0,
    desiredDevices: const [],
    devices: devices,
    lastOfflineSyncAt: null,
    lastFailureAt: null,
    lastFailureCode: null,
    lastSeenAt: DateTime.utc(2026, 10, 10, 12),
    createdAt: DateTime.utc(2026, 10, 10),
    updatedAt: null,
  );
}

AccessDeviceStatus _device({
  required DateTime readAt,
  required int credentialType,
}) {
  return AccessDeviceStatus(
    deviceKey: 'primary',
    providerKey: 'toletus-litenet2',
    enabled: true,
    connected: true,
    endpoint: '192.168.0.250:7878',
    lastCredentialReadAt: readAt,
    lastCredentialType: credentialType,
  );
}
