import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:avelri_gestao/core/update/installer_download_service.dart';
import 'package:avelri_gestao/core/update/release_manifest_validator.dart';
import 'package:crypto/crypto.dart';
import 'package:cryptography/cryptography.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;

void main() {
  final signingAlgorithm = Ed25519();
  late SimpleKeyPair keyPair;
  late ReleaseManifestValidator validator;
  late Directory testDirectory;

  Future<ReleaseManifest> signedRelease(
    List<int> payload, {
    String? declaredHash,
    int? declaredSize,
    String url = 'https://updates.example.invalid/installer.exe',
  }) async {
    final manifestBytes = utf8.encode(
      jsonEncode({
        'schemaVersion': 1,
        'appId': 'B46B7D1A-6E54-4C0A-A1E2-5F41C6D92837',
        'channel': 'production',
        'platform': 'windows-x64',
        'version': '1.1.0',
        'build': 2,
        'minimumInstalledVersion': '1.0.0',
        'minimumInstalledBuild': 1,
        'issuedAt': '2026-11-01T00:00:00Z',
        'expiresAt': '2026-11-07T00:00:00Z',
        'installer': {
          'url': url,
          'sizeBytes': declaredSize ?? payload.length,
          'sha256': declaredHash ?? sha256.convert(payload).toString(),
        },
      }),
    );
    final signature = await signingAlgorithm.sign(
      manifestBytes,
      keyPair: keyPair,
    );
    return validator.validate(
      manifestBytes: manifestBytes,
      signatureBase64: base64.encode(signature.bytes),
      keyId: 'synthetic-test-key',
    );
  }

  InstallerDownloadService service(_SyntheticClient client) =>
      InstallerDownloadService(
        allowedOrigins: {'https://updates.example.invalid'},
        clientFactory: () => client,
        temporaryDirectoryProvider: () async => testDirectory,
        clock: () => DateTime.utc(2026, 11, 5),
      );

  Future<void> expectNoPartialFiles() async {
    expect(await testDirectory.list().toList(), isEmpty);
  }

  setUp(() async {
    testDirectory = await Directory.systemTemp.createTemp(
      'avelri-stage7-test-',
    );
    keyPair = await signingAlgorithm.newKeyPair();
    final publicKey = await keyPair.extractPublicKey();
    validator = ReleaseManifestValidator(
      installed: InstalledRelease(
        appId: 'B46B7D1A-6E54-4C0A-A1E2-5F41C6D92837',
        channel: 'production',
        platform: 'windows-x64',
        version: '1.0.0',
        build: 1,
      ),
      trustedPublicKeys: {'synthetic-test-key': publicKey.bytes},
      clock: () => DateTime.utc(2026, 11, 5),
    );
  });

  tearDown(() async {
    await testDirectory.delete(recursive: true);
  });

  test(
    'streams an intact synthetic file into a unique temporary directory',
    () async {
      final payload = utf8.encode('synthetic installer bytes only');
      final release = await signedRelease(payload);
      final client = _SyntheticClient((request) async {
        expect(request.url, release.installerUrl);
        expect(request.followRedirects, isFalse);
        return http.StreamedResponse(
          Stream.fromIterable([payload.sublist(0, 8), payload.sublist(8)]),
          HttpStatus.ok,
          request: request,
        );
      });

      final result = await service(client).download(release);
      expect(result, isA<InstallerDownloadSuccess>());
      final file = (result as InstallerDownloadSuccess).file;
      expect(await file.readAsBytes(), payload);
      expect(file.parent.path, startsWith(testDirectory.path));
      expect(client.closed, isTrue);
    },
  );

  test('rejects a wrong SHA-256 and removes the partial file', () async {
    final payload = utf8.encode('synthetic bytes');
    final release = await signedRelease(payload, declaredHash: 'b' * 64);
    final client = _SyntheticClient(
      (request) async => http.StreamedResponse(
        Stream.value(payload),
        HttpStatus.ok,
        request: request,
      ),
    );

    final result = await service(client).download(release);
    expect(
      (result as InstallerDownloadFailure).reason,
      InstallerDownloadFailureReason.hashMismatch,
    );
    await expectNoPartialFiles();
  });

  test('rejects a truncated response and removes the partial file', () async {
    final payload = utf8.encode('synthetic bytes');
    final release = await signedRelease(payload);
    final client = _SyntheticClient(
      (request) async => http.StreamedResponse(
        Stream.value(payload.sublist(0, 4)),
        HttpStatus.ok,
        request: request,
      ),
    );

    final result = await service(client).download(release);
    expect(
      (result as InstallerDownloadFailure).reason,
      InstallerDownloadFailureReason.sizeMismatch,
    );
    await expectNoPartialFiles();
  });

  test(
    'rejects an HTTP error and an unsafe redirect without following it',
    () async {
      final payload = utf8.encode('synthetic bytes');
      final release = await signedRelease(payload);
      for (final status in [HttpStatus.internalServerError, HttpStatus.found]) {
        final client = _SyntheticClient((request) async {
          expect(request.followRedirects, isFalse);
          return http.StreamedResponse(
            const Stream<List<int>>.empty(),
            status,
            headers: {'location': 'http://untrusted.example.invalid/file.exe'},
            request: request,
          );
        });
        final result = await service(client).download(release);
        expect(
          (result as InstallerDownloadFailure).reason,
          InstallerDownloadFailureReason.invalidHttpResponse,
        );
        await expectNoPartialFiles();
      }
    },
  );

  test('rejects an origin outside the independent allowlist', () async {
    final payload = utf8.encode('synthetic bytes');
    final release = await signedRelease(
      payload,
      url: 'https://untrusted.example.invalid/installer.exe',
    );
    var called = false;
    final result = await InstallerDownloadService(
      allowedOrigins: {'https://updates.example.invalid'},
      clientFactory: () {
        called = true;
        throw StateError('No HTTP call is permitted.');
      },
      temporaryDirectoryProvider: () async => testDirectory,
      clock: () => DateTime.utc(2026, 11, 5),
    ).download(release);
    expect(
      (result as InstallerDownloadFailure).reason,
      InstallerDownloadFailureReason.unauthorizedOrigin,
    );
    expect(called, isFalse);
    await expectNoPartialFiles();
  });

  test('cancels and removes a partially received synthetic file', () async {
    final payload = utf8.encode('synthetic bytes');
    final release = await signedRelease(payload);
    final cancellation = DownloadCancellation();
    late final StreamController<List<int>> controller;
    controller = StreamController<List<int>>(
      onListen: () {
        controller.add(payload.sublist(0, 4));
        Future<void>.delayed(const Duration(milliseconds: 20), () {
          cancellation.cancel();
          controller.close();
        });
      },
    );
    final client = _SyntheticClient(
      (request) async => http.StreamedResponse(
        controller.stream,
        HttpStatus.ok,
        request: request,
      ),
    );

    final result = await service(client)
        .download(release, cancellation: cancellation);
    expect(
      (result as InstallerDownloadFailure).reason,
      InstallerDownloadFailureReason.cancelled,
    );
    await expectNoPartialFiles();
  });

  test(
    'refuses a verified manifest after it expires without making HTTP calls',
    () async {
      final release = await signedRelease(utf8.encode('synthetic bytes'));
      var called = false;
      final result = await InstallerDownloadService(
        allowedOrigins: {'https://updates.example.invalid'},
        clientFactory: () {
          called = true;
          throw StateError('No HTTP call is permitted.');
        },
        temporaryDirectoryProvider: () async => testDirectory,
        clock: () => DateTime.utc(2026, 11, 8),
      ).download(release);
      expect(
        (result as InstallerDownloadFailure).reason,
        InstallerDownloadFailureReason.expiredManifest,
      );
      expect(called, isFalse);
      await expectNoPartialFiles();
    },
  );
}

class _SyntheticClient extends http.BaseClient {
  _SyntheticClient(this.handler);

  final Future<http.StreamedResponse> Function(http.BaseRequest) handler;
  bool closed = false;

  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) =>
      handler(request);

  @override
  void close() => closed = true;
}
