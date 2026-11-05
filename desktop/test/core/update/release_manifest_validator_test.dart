import 'dart:convert';

import 'package:avelri_gestao/core/update/release_manifest_validator.dart';
import 'package:cryptography/cryptography.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  final algorithm = Ed25519();
  late SimpleKeyPair keyPair;
  late List<int> publicKey;
  late ReleaseManifestValidator validator;

  Map<String, dynamic> manifest({
    String appId = 'B46B7D1A-6E54-4C0A-A1E2-5F41C6D92837',
    String channel = 'production',
    String platform = 'windows-x64',
    String version = '1.1.0',
    int build = 2,
    String issuedAt = '2026-11-01T00:00:00Z',
    String expiresAt = '2026-11-07T00:00:00Z',
  }) => {
    'schemaVersion': 1,
    'appId': appId,
    'channel': channel,
    'platform': platform,
    'version': version,
    'build': build,
    'minimumInstalledVersion': '1.0.0',
    'minimumInstalledBuild': 1,
    'issuedAt': issuedAt,
    'expiresAt': expiresAt,
    'installer': {
      'url': 'https://updates.example.invalid/avelri-gestao.exe',
      'sizeBytes': 123456,
      'sha256': 'a' * 64,
    },
  };

  Future<ReleaseManifest> validateSigned(Map<String, dynamic> data) async {
    final bytes = utf8.encode(jsonEncode(data));
    final signature = await algorithm.sign(bytes, keyPair: keyPair);
    return validator.validate(
      manifestBytes: bytes,
      signatureBase64: base64.encode(signature.bytes),
      keyId: 'synthetic-test-key',
    );
  }

  Matcher rejects(ManifestRejection reason) => throwsA(
    isA<ManifestValidationException>().having(
      (error) => error.reason,
      'reason',
      reason,
    ),
  );

  setUp(() async {
    keyPair = await algorithm.newKeyPair();
    publicKey = (await keyPair.extractPublicKey()).bytes;
    validator = ReleaseManifestValidator(
      installed: InstalledRelease(
        appId: 'B46B7D1A-6E54-4C0A-A1E2-5F41C6D92837',
        channel: 'production',
        platform: 'windows-x64',
        version: '1.0.0',
        build: 1,
      ),
      trustedPublicKeys: {'synthetic-test-key': publicKey},
      clock: () => DateTime.utc(2026, 11, 5),
    );
  });

  test('accepts an exact signed manifest with a newer build', () async {
    final release = await validateSigned(manifest());
    expect(release.version.toString(), '1.1.0');
    expect(release.build, 2);
    expect(release.installerSizeBytes, 123456);
    expect(release.installerSha256, 'a' * 64);
  });

  test('rejects an altered signature', () async {
    final bytes = utf8.encode(jsonEncode(manifest()));
    final signature = await algorithm.sign(bytes, keyPair: keyPair);
    final changed = [...signature.bytes]..[0] ^= 1;
    expect(
      validator.validate(
        manifestBytes: bytes,
        signatureBase64: base64.encode(changed),
        keyId: 'synthetic-test-key',
      ),
      rejects(ManifestRejection.invalidSignature),
    );
  });

  test('rejects a byte change after signing', () async {
    final bytes = utf8.encode(jsonEncode(manifest()));
    final signature = await algorithm.sign(bytes, keyPair: keyPair);
    expect(
      validator.validate(
        manifestBytes: [...bytes, 0x20],
        signatureBase64: base64.encode(signature.bytes),
        keyId: 'synthetic-test-key',
      ),
      rejects(ManifestRejection.invalidSignature),
    );
  });

  test('rejects an expired signed manifest', () async {
    expect(
      validateSigned(manifest(expiresAt: '2026-11-05T00:00:00Z')),
      rejects(ManifestRejection.expired),
    );
  });

  test('rejects a signed manifest issued in the future', () async {
    expect(
      validateSigned(manifest(issuedAt: '2026-11-06T00:00:00Z')),
      rejects(ManifestRejection.notYetValid),
    );
  });

  test('rejects old version and non-increasing build', () async {
    expect(
      validateSigned(
        manifest(version: '0.9.9', build: 2)
          ..['minimumInstalledVersion'] = '0.9.0',
      ),
      rejects(ManifestRejection.downgrade),
    );
    expect(
      validateSigned(manifest(version: '1.1.0', build: 1)),
      rejects(ManifestRejection.downgrade),
    );
  });

  test(
    'rejects another app, channel, platform, or unsupported client',
    () async {
      for (final data in [
        manifest(appId: 'D68C6C5D-2D87-4CBA-9918-3AD08C0D1C69'),
        manifest(channel: 'homologation'),
        manifest(platform: 'windows-arm64'),
        manifest()..['minimumInstalledBuild'] = 2,
      ]) {
        expect(
          validateSigned(data),
          rejects(ManifestRejection.incompatibleRelease),
        );
      }
    },
  );

  test('rejects malformed signed size, SHA-256, and version', () async {
    for (final data in [
      manifest()..['installer'] = {...manifest()['installer'], 'sizeBytes': 0},
      manifest()..['installer'] = {...manifest()['installer'], 'sha256': 'bad'},
      manifest(version: '1.1.0-beta'),
      manifest(expiresAt: '2026-11-31T00:00:00Z'),
    ]) {
      expect(
        validateSigned(data),
        rejects(ManifestRejection.malformedManifest),
      );
    }
  });

  test('rejects an unknown key identifier', () async {
    final bytes = utf8.encode(jsonEncode(manifest()));
    final signature = await algorithm.sign(bytes, keyPair: keyPair);
    expect(
      validator.validate(
        manifestBytes: bytes,
        signatureBase64: base64.encode(signature.bytes),
        keyId: 'untrusted-key',
      ),
      rejects(ManifestRejection.untrustedKey),
    );
  });
}
