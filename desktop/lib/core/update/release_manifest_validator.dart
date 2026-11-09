import 'dart:convert';
import 'dart:typed_data';

import 'package:cryptography/cryptography.dart';

/// The signature and key identifier are transported separately from these bytes.
/// A publisher must sign the exact UTF-8 bytes that it serves to the client.
final class ReleaseManifest {
  const ReleaseManifest._verified({
    required this.appId,
    required this.channel,
    required this.platform,
    required this.version,
    required this.build,
    required this.minimumInstalledVersion,
    required this.minimumInstalledBuild,
    required this.issuedAt,
    required this.expiresAt,
    required this.installerUrl,
    required this.installerSizeBytes,
    required this.installerSha256,
  });

  final String appId;
  final String channel;
  final String platform;
  final ReleaseVersion version;
  final int build;
  final ReleaseVersion minimumInstalledVersion;
  final int minimumInstalledBuild;
  final DateTime issuedAt;
  final DateTime expiresAt;
  final Uri installerUrl;
  final int installerSizeBytes;
  final String installerSha256;
}

sealed class ReleaseInspection {
  const ReleaseInspection();
}

final class CurrentRelease extends ReleaseInspection {
  const CurrentRelease();
}

final class AvailableRelease extends ReleaseInspection {
  const AvailableRelease(this.manifest);

  final ReleaseManifest manifest;
}

class InstalledRelease {
  InstalledRelease({
    required this.appId,
    required this.channel,
    required this.platform,
    required String version,
    required this.build,
  }) : version = ReleaseVersion.parse(version) {
    if (appId.isEmpty || channel.isEmpty || platform.isEmpty || build < 1) {
      throw ArgumentError('Invalid installed release identity.');
    }
  }

  final String appId;
  final String channel;
  final String platform;
  final ReleaseVersion version;
  final int build;
}

class ReleaseVersion implements Comparable<ReleaseVersion> {
  const ReleaseVersion(this.major, this.minor, this.patch);

  factory ReleaseVersion.parse(String value) {
    final match = RegExp(r'^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$')
        .firstMatch(value);
    if (match == null) throw const FormatException('Invalid release version.');
    final numbers = [for (var i = 1; i <= 3; i++) int.tryParse(match[i]!)];
    if (numbers.any((number) => number == null)) {
      throw const FormatException('Invalid release version.');
    }
    return ReleaseVersion(numbers[0]!, numbers[1]!, numbers[2]!);
  }

  final int major;
  final int minor;
  final int patch;

  @override
  int compareTo(ReleaseVersion other) {
    for (final (left, right) in [
      (major, other.major),
      (minor, other.minor),
      (patch, other.patch),
    ]) {
      final comparison = left.compareTo(right);
      if (comparison != 0) return comparison;
    }
    return 0;
  }

  @override
  String toString() => '$major.$minor.$patch';
}

enum ManifestRejection {
  untrustedKey,
  malformedSignature,
  invalidSignature,
  malformedManifest,
  incompatibleRelease,
  notYetValid,
  expired,
  downgrade,
}

class ManifestValidationException implements Exception {
  const ManifestValidationException(this.reason);

  final ManifestRejection reason;

  @override
  String toString() => 'ManifestValidationException: ${reason.name}';
}

/// Verifies a detached Ed25519 signature before interpreting release metadata.
/// Trusted public keys must come from the application binary, never the response.
class ReleaseManifestValidator {
  ReleaseManifestValidator({
    required this.installed,
    required Map<String, List<int>> trustedPublicKeys,
    DateTime Function()? clock,
  }) : _clock = clock ?? DateTime.now,
       _trustedPublicKeys = Map.unmodifiable({
         for (final entry in trustedPublicKeys.entries)
           entry.key: List<int>.unmodifiable(entry.value),
       }) {
    for (final entry in _trustedPublicKeys.entries) {
      if (entry.key.isEmpty || entry.value.length != 32) {
        throw ArgumentError('Invalid trusted Ed25519 public key.');
      }
    }
  }

  static const int maximumManifestBytes = 16 * 1024;

  final InstalledRelease installed;
  final DateTime Function() _clock;
  final Map<String, List<int>> _trustedPublicKeys;
  final Ed25519 _ed25519 = Ed25519();

  Future<ReleaseManifest> validate({
    required List<int> manifestBytes,
    required String signatureBase64,
    required String keyId,
  }) async {
    final inspection = await inspect(
      manifestBytes: manifestBytes,
      signatureBase64: signatureBase64,
      keyId: keyId,
    );
    if (inspection is AvailableRelease) return inspection.manifest;
    throw const ManifestValidationException(ManifestRejection.downgrade);
  }

  /// A signed release equal to the installed build is current. Older signed
  /// releases remain rejected, so a replay cannot be presented as up to date.
  Future<ReleaseInspection> inspect({
    required List<int> manifestBytes,
    required String signatureBase64,
    required String keyId,
  }) async {
    final key = _trustedPublicKeys[keyId];
    if (key == null) {
      throw const ManifestValidationException(ManifestRejection.untrustedKey);
    }
    if (manifestBytes.isEmpty || manifestBytes.length > maximumManifestBytes) {
      throw const ManifestValidationException(
        ManifestRejection.malformedManifest,
      );
    }

    late final List<int> signatureBytes;
    try {
      signatureBytes = base64.decode(signatureBase64);
      if (signatureBytes.length != 64 ||
          base64.encode(signatureBytes) != signatureBase64) {
        throw const FormatException('Invalid signature encoding.');
      }
    } on FormatException {
      throw const ManifestValidationException(
        ManifestRejection.malformedSignature,
      );
    }

    // Copy before awaiting verification so a caller cannot change the input
    // between signature verification and JSON parsing.
    final signedBytes = Uint8List.fromList(manifestBytes);
    final valid = await _ed25519.verify(
      signedBytes,
      signature: Signature(
        signatureBytes,
        publicKey: SimplePublicKey(key, type: KeyPairType.ed25519),
      ),
    );
    if (!valid) {
      throw const ManifestValidationException(
        ManifestRejection.invalidSignature,
      );
    }

    final manifest = _parseManifest(signedBytes);
    if (manifest.appId != installed.appId ||
        manifest.channel != installed.channel ||
        manifest.platform != installed.platform ||
        installed.version.compareTo(manifest.minimumInstalledVersion) < 0 ||
        installed.build < manifest.minimumInstalledBuild) {
      throw const ManifestValidationException(
        ManifestRejection.incompatibleRelease,
      );
    }

    final now = _clock().toUtc();
    if (manifest.issuedAt.isAfter(now)) {
      throw const ManifestValidationException(ManifestRejection.notYetValid);
    }
    if (!manifest.expiresAt.isAfter(now)) {
      throw const ManifestValidationException(ManifestRejection.expired);
    }
    if (manifest.version.compareTo(installed.version) == 0 &&
        manifest.build == installed.build) {
      return const CurrentRelease();
    }
    if (manifest.version.compareTo(installed.version) < 0 ||
        manifest.build <= installed.build) {
      throw const ManifestValidationException(ManifestRejection.downgrade);
    }
    return AvailableRelease(manifest);
  }

  ReleaseManifest _parseManifest(List<int> bytes) {
    try {
      final decoded = jsonDecode(utf8.decode(bytes, allowMalformed: false));
      if (decoded is! Map<String, dynamic>) throw const FormatException();
      _requireFields(decoded, const {
        'schemaVersion',
        'appId',
        'channel',
        'platform',
        'version',
        'build',
        'minimumInstalledVersion',
        'minimumInstalledBuild',
        'issuedAt',
        'expiresAt',
        'installer',
      });
      if (decoded['schemaVersion'] != 1) throw const FormatException();
      final installer = decoded['installer'];
      if (installer is! Map<String, dynamic>) throw const FormatException();
      _requireFields(installer, const {'url', 'sizeBytes', 'sha256'});

      final appId = _string(decoded['appId']);
      final channel = _string(decoded['channel']);
      final platform = _string(decoded['platform']);
      final version = ReleaseVersion.parse(_string(decoded['version']));
      final build = _positiveInt(decoded['build']);
      final minimumVersion = ReleaseVersion.parse(
        _string(decoded['minimumInstalledVersion']),
      );
      final minimumBuild = _positiveInt(decoded['minimumInstalledBuild']);
      final issuedAt = _utcDate(decoded['issuedAt']);
      final expiresAt = _utcDate(decoded['expiresAt']);
      if (!expiresAt.isAfter(issuedAt)) throw const FormatException();

      final url = Uri.tryParse(_string(installer['url']));
      if (url == null ||
          url.scheme != 'https' ||
          !url.hasAuthority ||
          url.host.isEmpty ||
          url.userInfo.isNotEmpty ||
          url.hasFragment) {
        throw const FormatException();
      }
      final sizeBytes = _positiveInt(installer['sizeBytes']);
      final sha256 = _string(installer['sha256']);
      if (!RegExp(r'^[0-9a-f]{64}$').hasMatch(sha256)) {
        throw const FormatException();
      }
      if (minimumVersion.compareTo(version) > 0 || minimumBuild > build) {
        throw const FormatException();
      }

      return ReleaseManifest._verified(
        appId: appId,
        channel: channel,
        platform: platform,
        version: version,
        build: build,
        minimumInstalledVersion: minimumVersion,
        minimumInstalledBuild: minimumBuild,
        issuedAt: issuedAt,
        expiresAt: expiresAt,
        installerUrl: url,
        installerSizeBytes: sizeBytes,
        installerSha256: sha256,
      );
    } on FormatException {
      throw const ManifestValidationException(
        ManifestRejection.malformedManifest,
      );
    } on TypeError {
      throw const ManifestValidationException(
        ManifestRejection.malformedManifest,
      );
    }
  }

  void _requireFields(Map<String, dynamic> value, Set<String> fields) {
    if (value.length != fields.length ||
        !value.keys.toSet().containsAll(fields)) {
      throw const FormatException();
    }
  }

  String _string(Object? value) {
    if (value is! String || value.isEmpty) throw const FormatException();
    return value;
  }

  int _positiveInt(Object? value) {
    if (value is! int || value < 1) throw const FormatException();
    return value;
  }

  DateTime _utcDate(Object? value) {
    final text = _string(value);
    final match = RegExp(
      r'^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})(?:\.\d+)?Z$',
    ).firstMatch(text);
    if (match == null) throw const FormatException();
    final parsed = DateTime.tryParse(text);
    if (parsed == null || !parsed.isUtc) throw const FormatException();
    final parts = [
      parsed.year,
      parsed.month,
      parsed.day,
      parsed.hour,
      parsed.minute,
      parsed.second,
    ];
    for (var i = 0; i < parts.length; i++) {
      if (parts[i] != int.parse(match[i + 1]!)) throw const FormatException();
    }
    return parsed;
  }
}
