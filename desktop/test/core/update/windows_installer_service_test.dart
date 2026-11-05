import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:avelri_gestao/core/update/installer_download_service.dart';
import 'package:avelri_gestao/core/update/release_manifest_validator.dart';
import 'package:avelri_gestao/core/update/windows_installer_service.dart';
import 'package:crypto/crypto.dart';
import 'package:cryptography/cryptography.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;

void main() {
  const appId = 'B46B7D1A-6E54-4C0A-A1E2-5F41C6D92837';
  final payload = utf8.encode('synthetic data, never an executable');
  final signingAlgorithm = Ed25519();
  late Directory testDirectory;
  late InstallerDownloadSuccess download;
  late _SimulatedLauncher launcher;
  late List<String> events;

  InstalledRelease installed({
    String id = appId,
    String version = '1.0.0',
    int build = 1,
  }) => InstalledRelease(
    appId: id,
    channel: 'production',
    platform: 'windows-x64',
    version: version,
    build: build,
  );

  WindowsInstallerService service({
    InstalledRelease? current,
    DateTime Function()? clock,
  }) => WindowsInstallerService(
    installed: current ?? installed(),
    launcher: launcher,
    requestDesktopShutdown: () async => events.add('shutdown'),
    clock: clock ?? () => DateTime.utc(2026, 11, 5),
  );

  Future<bool> approve(ReleaseManifest _) async => true;

  setUp(() async {
    testDirectory = await Directory.systemTemp.createTemp(
      'avelri-stage8-test-',
    );
    events = [];
    launcher = _SimulatedLauncher(() async => events.add('start'));

    final keyPair = await signingAlgorithm.newKeyPair();
    final validator = ReleaseManifestValidator(
      installed: installed(),
      trustedPublicKeys: {
        'synthetic-test-key': (await keyPair.extractPublicKey()).bytes,
      },
      clock: () => DateTime.utc(2026, 11, 5),
    );
    final bytes = utf8.encode(
      jsonEncode({
        'schemaVersion': 1,
        'appId': appId,
        'channel': 'production',
        'platform': 'windows-x64',
        'version': '1.1.0',
        'build': 2,
        'minimumInstalledVersion': '1.0.0',
        'minimumInstalledBuild': 1,
        'issuedAt': '2026-11-01T00:00:00Z',
        'expiresAt': '2026-11-07T00:00:00Z',
        'installer': {
          'url': 'https://updates.example.invalid/installer.exe',
          'sizeBytes': payload.length,
          'sha256': sha256.convert(payload).toString(),
        },
      }),
    );
    final signature = await signingAlgorithm.sign(bytes, keyPair: keyPair);
    final release = await validator.validate(
      manifestBytes: bytes,
      signatureBase64: base64.encode(signature.bytes),
      keyId: 'synthetic-test-key',
    );
    final downloader = InstallerDownloadService(
      allowedOrigins: {'https://updates.example.invalid'},
      clientFactory: () => _SyntheticClient(
        (request) async => http.StreamedResponse(
          Stream.value(payload),
          HttpStatus.ok,
          request: request,
        ),
      ),
      temporaryDirectoryProvider: () async => testDirectory,
      clock: () => DateTime.utc(2026, 11, 5),
    );
    download = await downloader.download(release) as InstallerDownloadSuccess;
  });

  tearDown(() async {
    await testDirectory.delete(recursive: true);
  });

  test(
    'starts an intact authorized installer before requesting Desktop close',
    () async {
      final result = await service().startUpdate(
        download,
        confirmUser: approve,
      );
      expect(result, isA<InstallerStarted>());
      expect((result as InstallerStarted).desktopCloseRequested, isTrue);
      expect(events, ['start', 'shutdown']);
      expect(launcher.calls, 1);
    },
  );

  test('rejects a same-size file changed after download', () async {
    await download.file.writeAsBytes(List<int>.filled(payload.length, 0x58));
    final result = await service().startUpdate(download, confirmUser: approve);
    expect(
      (result as InstallerStartFailed).reason,
      InstallerStartFailureReason.hashMismatch,
    );
    expect(events, isEmpty);
  });

  test('rejects a missing or incomplete file', () async {
    await download.file.delete();
    final missing = await service().startUpdate(download, confirmUser: approve);
    expect(
      (missing as InstallerStartFailed).reason,
      InstallerStartFailureReason.fileMissing,
    );

    await download.file.writeAsBytes(payload.sublist(0, 4));
    final incomplete = await service().startUpdate(
      download,
      confirmUser: approve,
    );
    expect(
      (incomplete as InstallerStartFailed).reason,
      InstallerStartFailureReason.sizeMismatch,
    );
    expect(events, isEmpty);
  });

  test('never launches without explicit user approval', () async {
    final installer = service();
    final missing = await installer.startUpdate(download);
    expect(
      (missing as InstallerStartFailed).reason,
      InstallerStartFailureReason.authorizationMissing,
    );
    final denied = await installer.startUpdate(
      download,
      confirmUser: (_) async => false,
    );
    expect(
      (denied as InstallerStartFailed).reason,
      InstallerStartFailureReason.authorizationMissing,
    );
    expect(events, isEmpty);
  });

  test('does not launch when the confirmation callback fails', () async {
    final result = await service().startUpdate(
      download,
      confirmUser: (_) async =>
          throw StateError('synthetic confirmation error'),
    );
    expect(
      (result as InstallerStartFailed).reason,
      InstallerStartFailureReason.authorizationFailed,
    );
    expect(events, isEmpty);
  });

  test('rejects a second attempt while confirmation is pending', () async {
    final pending = Completer<bool>();
    final installer = service();
    final first = installer.startUpdate(
      download,
      confirmUser: (_) => pending.future,
    );
    await Future<void>.delayed(const Duration(milliseconds: 20));
    final second = await installer.startUpdate(download, confirmUser: approve);
    expect(
      (second as InstallerStartFailed).reason,
      InstallerStartFailureReason.busy,
    );
    pending.complete(false);
    expect(await first, isA<InstallerStartFailed>());
    expect(events, isEmpty);
  });

  test('keeps Desktop open when Windows process start fails', () async {
    launcher = _SimulatedLauncher(() async {
      throw const ProcessException('synthetic.exe', [], 'simulated failure');
    });
    final result = await service().startUpdate(download, confirmUser: approve);
    expect(
      (result as InstallerStartFailed).reason,
      InstallerStartFailureReason.processStartFailed,
    );
    expect(launcher.calls, 1);
    expect(events, isEmpty);
  });

  test('rechecks the file after approval before starting Windows', () async {
    final result = await service().startUpdate(
      download,
      confirmUser: (_) async {
        await download.file.writeAsBytes(
          List<int>.filled(payload.length, 0x58),
        );
        return true;
      },
    );
    expect(
      (result as InstallerStartFailed).reason,
      InstallerStartFailureReason.hashMismatch,
    );
    expect(events, isEmpty);
  });

  test('rejects an expired manifest, wrong identity, and downgrade', () async {
    final expired = await service(clock: () => DateTime.utc(2026, 11, 8))
        .startUpdate(download, confirmUser: approve);
    expect(
      (expired as InstallerStartFailed).reason,
      InstallerStartFailureReason.manifestExpired,
    );

    final wrongIdentity = await service(current: installed(id: 'another-app'))
        .startUpdate(download, confirmUser: approve);
    expect(
      (wrongIdentity as InstallerStartFailed).reason,
      InstallerStartFailureReason.incompatibleRelease,
    );

    final downgrade = await service(
      current: installed(version: '1.1.0', build: 2),
    ).startUpdate(download, confirmUser: approve);
    expect(
      (downgrade as InstallerStartFailed).reason,
      InstallerStartFailureReason.downgrade,
    );
    expect(events, isEmpty);
  });
}

class _SimulatedLauncher implements InstallerProcessLauncher {
  _SimulatedLauncher(this.onStart);

  final Future<void> Function() onStart;
  int calls = 0;

  @override
  Future<void> start(File installer) async {
    calls++;
    await onStart();
  }
}

class _SyntheticClient extends http.BaseClient {
  _SyntheticClient(this.handler);

  final Future<http.StreamedResponse> Function(http.BaseRequest) handler;

  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) =>
      handler(request);
}
