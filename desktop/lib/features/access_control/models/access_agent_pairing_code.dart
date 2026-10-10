class AccessAgentPairingCode {
  const AccessAgentPairingCode({required this.code, required this.expiresAt});

  final String code;
  final DateTime expiresAt;

  factory AccessAgentPairingCode.fromJson(Map<String, dynamic> json) {
    return AccessAgentPairingCode(
      code: json['code'] as String,
      expiresAt: DateTime.parse(json['expiresAt'] as String),
    );
  }
}
