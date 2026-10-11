import 'package:avelri_gestao/features/access_control/models/access_agent_pairing_code.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('parses access agent pairing code response', () {
    final result = AccessAgentPairingCode.fromJson({
      'code': '7K4M-N9Q2',
      'expiresAt': '2026-10-10T23:30:00Z',
    });

    expect(result.code, '7K4M-N9Q2');
    expect(result.expiresAt, DateTime.utc(2026, 10, 10, 23, 30));
  });
}
