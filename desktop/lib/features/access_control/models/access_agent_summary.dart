class AccessAgentSummary {
  const AccessAgentSummary({
    required this.id,
    required this.gymId,
    required this.name,
    required this.machineName,
    required this.isActive,
    required this.releaseEnabled,
    required this.configurationVersion,
    required this.appliedConfigurationVersion,
    required this.configurationApplied,
    required this.isOnline,
    required this.pendingOfflineEvents,
    required this.desiredDevices,
    required this.devices,
    required this.lastOfflineSyncAt,
    required this.lastFailureAt,
    required this.lastFailureCode,
    required this.lastSeenAt,
    required this.createdAt,
    required this.updatedAt,
  });

  final String id;
  final String gymId;
  final String name;
  final String machineName;
  final bool isActive;
  final bool releaseEnabled;
  final int configurationVersion;
  final int? appliedConfigurationVersion;
  final bool configurationApplied;
  final bool isOnline;
  final int? pendingOfflineEvents;
  final List<AccessDeviceConfiguration> desiredDevices;
  final List<AccessDeviceStatus> devices;
  final DateTime? lastOfflineSyncAt;
  final DateTime? lastFailureAt;
  final String? lastFailureCode;
  final DateTime? lastSeenAt;
  final DateTime createdAt;
  final DateTime? updatedAt;

  bool get hasConnectedDevice =>
      devices.any((device) => device.enabled && device.connected);

  factory AccessAgentSummary.fromJson(Map<String, dynamic> json) {
    final rawDesiredDevices = json['desiredDevices'];
    final rawDevices = json['devices'];

    return AccessAgentSummary(
      id: json['id'] as String,
      gymId: json['gymId'] as String,
      name: json['name'] as String,
      machineName: json['machineName'] as String,
      isActive: json['isActive'] as bool,
      releaseEnabled: json['releaseEnabled'] as bool,
      configurationVersion: (json['configurationVersion'] as num).toInt(),
      appliedConfigurationVersion: (json['appliedConfigurationVersion'] as num?)
          ?.toInt(),
      configurationApplied: json['configurationApplied'] as bool,
      isOnline: json['isOnline'] as bool,
      pendingOfflineEvents: (json['pendingOfflineEvents'] as num?)?.toInt(),
      desiredDevices: rawDesiredDevices is List
          ? rawDesiredDevices
                .map(
                  (item) => AccessDeviceConfiguration.fromJson(
                    Map<String, dynamic>.from(item as Map),
                  ),
                )
                .toList()
          : const [],
      devices: rawDevices is List
          ? rawDevices
                .map(
                  (item) => AccessDeviceStatus.fromJson(
                    Map<String, dynamic>.from(item as Map),
                  ),
                )
                .toList()
          : const [],
      lastOfflineSyncAt: _parseNullableDateTime(json['lastOfflineSyncAt']),
      lastFailureAt: _parseNullableDateTime(json['lastFailureAt']),
      lastFailureCode: json['lastFailureCode'] as String?,
      lastSeenAt: _parseNullableDateTime(json['lastSeenAt']),
      createdAt: DateTime.parse(json['createdAt'] as String),
      updatedAt: _parseNullableDateTime(json['updatedAt']),
    );
  }

  static DateTime? _parseNullableDateTime(Object? value) {
    if (value is! String || value.isEmpty) {
      return null;
    }

    return DateTime.parse(value);
  }
}

class AccessDeviceStatus {
  const AccessDeviceStatus({
    required this.deviceKey,
    required this.providerKey,
    required this.enabled,
    required this.connected,
    required this.endpoint,
  });

  final String deviceKey;
  final String providerKey;
  final bool enabled;
  final bool connected;
  final String? endpoint;

  factory AccessDeviceStatus.fromJson(Map<String, dynamic> json) {
    return AccessDeviceStatus(
      deviceKey: json['deviceKey'] as String,
      providerKey: json['providerKey'] as String,
      enabled: json['enabled'] as bool,
      connected: json['connected'] as bool,
      endpoint: json['endpoint'] as String?,
    );
  }
}

class AccessDeviceConfiguration {
  const AccessDeviceConfiguration({
    required this.deviceKey,
    required this.providerKey,
    required this.enabled,
    required this.host,
    required this.port,
  });

  final String deviceKey;
  final String providerKey;
  final bool enabled;
  final String? host;
  final int port;

  factory AccessDeviceConfiguration.fromJson(Map<String, dynamic> json) {
    return AccessDeviceConfiguration(
      deviceKey: json['deviceKey'] as String,
      providerKey: json['providerKey'] as String,
      enabled: json['enabled'] as bool,
      host: json['host'] as String?,
      port: (json['port'] as num).toInt(),
    );
  }
}
