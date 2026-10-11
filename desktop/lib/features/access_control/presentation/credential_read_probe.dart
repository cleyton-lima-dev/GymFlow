import 'dart:async';

import 'package:avelri_gestao/features/access_control/models/access_agent_summary.dart';

class CredentialReadObservation {
  const CredentialReadObservation({
    required this.deviceKey,
    required this.providerKey,
    required this.readAt,
    required this.credentialType,
  });

  final String deviceKey;
  final String providerKey;
  final DateTime readAt;
  final int credentialType;
}

typedef AccessAgentsLoader = Future<List<AccessAgentSummary>> Function();

class CredentialReadProbe {
  const CredentialReadProbe(this._loadAgents);

  final AccessAgentsLoader _loadAgents;

  Future<CredentialReadObservation?> waitForNextRead({
    required String agentId,
    required DateTime? baseline,
    Duration timeout = const Duration(seconds: 90),
    Duration pollInterval = const Duration(seconds: 3),
  }) async {
    final deadline = DateTime.now().add(timeout);

    while (DateTime.now().isBefore(deadline)) {
      final agents = await _loadAgents();

      AccessAgentSummary? agent;

      for (final candidate in agents) {
        if (candidate.id == agentId) {
          agent = candidate;
          break;
        }
      }

      if (agent != null) {
        CredentialReadObservation? newest;

        for (final device in agent.devices) {
          final readAt = device.lastCredentialReadAt;
          final credentialType = device.lastCredentialType;

          if (readAt == null || credentialType == null) {
            continue;
          }

          if (baseline != null && !readAt.isAfter(baseline)) {
            continue;
          }

          if (newest == null || readAt.isAfter(newest.readAt)) {
            newest = CredentialReadObservation(
              deviceKey: device.deviceKey,
              providerKey: device.providerKey,
              readAt: readAt,
              credentialType: credentialType,
            );
          }
        }

        if (newest != null) {
          return newest;
        }
      }

      final remaining = deadline.difference(DateTime.now());

      if (remaining <= Duration.zero) {
        break;
      }

      await Future<void>.delayed(
        remaining < pollInterval ? remaining : pollInterval,
      );
    }

    return null;
  }

  static DateTime? latestReadAt(AccessAgentSummary agent) {
    DateTime? latest;

    for (final device in agent.devices) {
      final readAt = device.lastCredentialReadAt;

      if (readAt == null) {
        continue;
      }

      if (latest == null || readAt.isAfter(latest)) {
        latest = readAt;
      }
    }

    return latest;
  }
}
