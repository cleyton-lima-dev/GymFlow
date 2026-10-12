import 'package:avelri_gestao/core/network/api_client.dart';

class GymSettings {
  const GymSettings({
    required this.timeZoneId,
    required this.timeZoneStatus,
    this.administrativeName,
    this.version,
    this.timeZoneSource = 'legacy',
  });
  final String? timeZoneId;
  final String timeZoneStatus;
  final String? administrativeName;
  final int? version;
  final String timeZoneSource;

  factory GymSettings.fromJson(Map<String, dynamic> json) {
    final status = json['timeZoneStatus'] as String;
    final id = json['timeZoneId'] as String?;
    if (!['configured', 'missing', 'invalid'].contains(status) ||
        (status == 'configured' && (id == null || id.trim().isEmpty))) {
      throw const FormatException('Invalid settings response');
    }
    return GymSettings(
      timeZoneId: id,
      timeZoneStatus: status,
      administrativeName: json['administrativeName'] as String?,
      version: (json['version'] as num?)?.toInt(),
      timeZoneSource: json['timeZoneSource'] as String? ?? 'legacy',
    );
  }
}

class GymSettingsService {
  const GymSettingsService(this.api);
  final ApiClient api;

  Future<GymSettings> get() async {
    final response = await api.get('api/gym-settings');
    return GymSettings.fromJson(Map<String, dynamic>.from(response as Map));
  }

  Future<GymSettings> save({
    required String name,
    required String timeZone,
    required int? version,
    required bool confirmed,
  }) async {
    final response = await api.put(
      'api/gym-settings',
      body: {
        'administrativeName': name,
        'timeZoneId': timeZone,
        'version': version,
        'confirmTimeZoneChange': confirmed,
      },
    );
    return GymSettings.fromJson(Map<String, dynamic>.from(response as Map));
  }
}
