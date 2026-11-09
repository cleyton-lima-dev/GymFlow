import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:avelri_gestao/core/update/installer_download_service.dart';
import 'package:avelri_gestao/core/update/signed_manifest_reader.dart';
import 'package:avelri_gestao/core/update/update_distribution_config.dart';
import 'package:avelri_gestao/core/update/windows_installer_service.dart';
import 'package:avelri_gestao/features/updates/presentation/update_controller.dart';
import 'package:avelri_gestao/features/updates/presentation/update_dialog.dart';
import 'package:crypto/crypto.dart';
import 'package:cryptography/cryptography.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:package_info_plus/package_info_plus.dart';

void main() {
  test(
    'missing distribution settings cause no manifest or download request',
    () async {
      final reader = _Reader();
      var downloaderCreated = false;
      final controller = UpdateController(
        config: null,
        versionReader: _VersionReader(),
        manifestReader: reader,
        downloadServiceFactory: (_) {
          downloaderCreated = true;
          throw StateError('must not create downloader');
        },
        installerServiceFactory: (_) => throw StateError('must not install'),
      );
      await controller.initialize();
      expect(controller.state, UpdateViewState.notConfigured);
      expect(controller.installedVersion?.version, '1.0.0');
      expect(controller.installedVersion?.build, 1);
      expect(reader.calls, 0);
      expect(downloaderCreated, isFalse);
      controller.dispose();
    },
  );

  test('installed version comes from package_info_plus', () async {
    PackageInfo.setMockInitialValues(
      appName: 'Avelri Gestão',
      packageName: 'com.avelri.gestao',
      version: '2.3.4',
      buildNumber: '57',
      buildSignature: '',
    );
    final result = await PackageInfoVersionReader().read();
    expect(result.version, '2.3.4');
    expect(result.build, 57);
  });

  testWidgets(
    'unconfigured dialog shows installed version and no download action',
    (tester) async {
      final controller = UpdateController(
        config: null,
        versionReader: _VersionReader(),
        manifestReader: _Reader(),
        downloadServiceFactory: (_) => throw StateError('unexpected'),
        installerServiceFactory: (_) => throw StateError('unexpected'),
      );
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(body: UpdateDialog(controller: controller)),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.text('1.0.0'), findsOneWidget);
      expect(find.text('Build 1'), findsOneWidget);
      expect(find.textContaining('ainda não está configurada'), findsOneWidget);
      expect(find.text('Baixar atualização'), findsNothing);
      await tester.pumpWidget(const SizedBox());
      controller.dispose();
    },
  );

  group('configured updater with disposable synthetic keys and files', () {
    late _Harness harness;

    setUp(() async => harness = await _Harness.create());
    tearDown(() async => harness.close());

    test(
      'a signed current build reports updated; a newer build is offered',
      () async {
        harness.reader.payload = await harness.signedPayload(
          version: '1.0.0',
          build: 1,
        );
        final controller = harness.controller();
        await controller.initialize();
        expect(controller.state, UpdateViewState.current);

        harness.reader.payload = await harness.signedPayload();
        await controller.check();
        expect(controller.state, UpdateViewState.available);
        expect(controller.availableRelease?.version.toString(), '1.1.0');
        controller.dispose();
      },
    );

    test(
      'an invalid detached signature enters error and retry verifies again',
      () async {
        final valid = await harness.signedPayload();
        harness.reader.payload = SignedManifestPayload(
          bytes: valid.bytes,
          signatureBase64: base64.encode(List<int>.filled(64, 0)),
        );
        final controller = harness.controller();
        await controller.initialize();
        expect(controller.state, UpdateViewState.error);
        expect(controller.availableRelease, isNull);
        harness.reader.payload = valid;
        await controller.retry();
        expect(controller.state, UpdateViewState.available);
        controller.dispose();
      },
    );

    testWidgets(
      'dialog shows checking, available, download progress and ready',
      (tester) async {
        harness.reader.payload = await harness.signedPayload();
        harness.reader.blocker = Completer<void>();
        final controller = harness.controller();
        await tester.pumpWidget(
          MaterialApp(
            home: Scaffold(body: UpdateDialog(controller: controller)),
          ),
        );
        await tester.pump();
        expect(find.text('Verificando atualizações…'), findsOneWidget);

        harness.reader.blocker!.complete();
        await tester.pumpAndSettle();
        expect(find.textContaining('Atualização disponível:'), findsOneWidget);
        controller.state = UpdateViewState.downloading;
        controller.progress = 0.5;
        controller.notifyListeners();
        await tester.pump();
        expect(find.textContaining('50%'), findsOneWidget);
        expect(find.text('Cancelar download'), findsOneWidget);

        controller.state = UpdateViewState.ready;
        controller.notifyListeners();
        await tester.pump();
        expect(find.textContaining('pronto para instalação'), findsOneWidget);
        expect(find.text('Instalar atualização'), findsOneWidget);
        await tester.pumpWidget(const SizedBox());
        controller.dispose();
      },
    );

    testWidgets('confirmation dialog returns explicit denial or approval', (
      tester,
    ) async {
      bool? decision;
      await tester.pumpWidget(
        MaterialApp(
          home: Builder(
            builder: (context) => TextButton(
              onPressed: () async {
                decision = await showDialog<bool>(
                  context: context,
                  builder: (_) => const InstallUpdateConfirmationDialog(
                    version: '1.1.0',
                  buildNumber: 2,
                  ),
                );
              },
              child: const Text('Abrir confirmação'),
            ),
          ),
        ),
      );
      await tester.tap(find.text('Abrir confirmação'));
      await tester.pumpAndSettle();
      expect(find.text('Instalar atualização?'), findsOneWidget);
      expect(find.text('1.1.0'), findsOneWidget);
      expect(find.text('Build 2'), findsOneWidget);
      await tester.tap(find.text('Cancelar'));
      await tester.pumpAndSettle();
      expect(decision, isFalse);
      await tester.tap(find.text('Abrir confirmação'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Instalar e fechar'));
      await tester.pumpAndSettle();
      expect(decision, isTrue);
    });

    testWidgets('dialog shows current and invalid manifest states', (
      tester,
    ) async {
      harness.reader.payload = await harness.signedPayload(
        version: '1.0.0',
        build: 1,
      );
      final controller = harness.controller();
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(body: UpdateDialog(controller: controller)),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.text('O Avelri Gestão está atualizado.'), findsOneWidget);

      harness.reader.payload = SignedManifestPayload(
        bytes: (await harness.signedPayload()).bytes,
        signatureBase64: base64.encode(List<int>.filled(64, 0)),
      );
      await controller.check();
      await tester.pumpAndSettle();
      expect(find.text('Tentar novamente'), findsOneWidget);
      expect(find.textContaining('com segurança'), findsOneWidget);
      await tester.pumpWidget(const SizedBox());
      controller.dispose();
    });

    test('download reports real byte progress and cancellation removes partial file', () async {
      harness.reader.payload = await harness.signedPayload();
      final firstChunkSent = Completer<void>();
      final finishStream = Completer<void>();
      harness.responseStream = () async* {
        yield harness.installerBytes.sublist(0, 8);
        firstChunkSent.complete();
        await finishStream.future;
        yield harness.installerBytes.sublist(8);
      };
      final controller = harness.controller();
      await controller.initialize();
      final download = controller.download();
      await firstChunkSent.future;
      await Future<void>.delayed(const Duration(milliseconds: 20));
      expect(controller.state, UpdateViewState.downloading);
      expect(controller.progress, greaterThan(0));
      expect(controller.progress, lessThan(1));
      controller.cancelDownload();
      finishStream.complete();
      await download;
      expect(controller.state, UpdateViewState.available);
      expect(await harness.directory.list().toList(), isEmpty);
      controller.dispose();
    });

    test('installation requires approval and closes only after a successful launch', () async {
      harness.reader.payload = await harness.signedPayload();
      final controller = harness.controller();
      await controller.initialize();
      await controller.download();
      expect(controller.state, UpdateViewState.ready);
      expect(controller.progress, 1);

      await controller.install((_) async => false);
      expect(controller.state, UpdateViewState.ready);
      expect(harness.launcher.starts, 0);
      expect(harness.shutdowns, 0);

      harness.launcher.fail = true;
      await controller.install((_) async => true);
      expect(controller.state, UpdateViewState.error);
      expect(harness.launcher.starts, 1);
      expect(harness.shutdowns, 0);
      controller.dispose();

      final successful = harness.controller();
      await successful.initialize();
      await successful.download();
      harness.launcher.fail = false;
      await successful.install((_) async => true);
      expect(successful.state, UpdateViewState.started);
      expect(harness.launcher.starts, 2);
      expect(harness.shutdowns, 1);
      successful.dispose();
    });
  });
}

final class _VersionReader implements InstalledVersionReader {
  @override
  Future<InstalledAppVersion> read() async =>
      const InstalledAppVersion('1.0.0', 1);
}

final class _Reader implements SignedManifestReader {
  SignedManifestPayload? payload;
  Completer<void>? blocker;
  int calls = 0;

  @override
  Future<SignedManifestPayload> read(UpdateDistributionConfig config) async {
    calls++;
    await blocker?.future;
    return payload!;
  }
}

final class _Launcher implements InstallerProcessLauncher {
  int starts = 0;
  bool fail = false;

  @override
  Future<void> start(File installer) async {
    starts++;
    if (fail) throw const ProcessException('synthetic', []);
  }
}

final class _Harness {
  _Harness._(this.keyPair, this.config, this.directory);

  static Future<_Harness> create() async {
    final keyPair = await Ed25519().newKeyPair();
    final publicKey = await keyPair.extractPublicKey();
    final config = UpdateDistributionConfig.tryCreate(
      enabled: true,
      appEnvironment: 'production',
      appId: UpdateDistributionConfig.productionAppId,
      channel: 'production',
      manifestUrl: 'https://updates.example.invalid/release.json',
      signatureUrl: 'https://updates.example.invalid/release.sig',
      downloadOrigin: 'https://updates.example.invalid',
      keyId: 'synthetic-key',
      publicKeyBase64: base64.encode(publicKey.bytes),
    )!;
    final directory = await Directory.systemTemp.createTemp('avelri-stage11-');
    return _Harness._(keyPair, config, directory);
  }

  final SimpleKeyPair keyPair;
  final UpdateDistributionConfig config;
  final Directory directory;
  final reader = _Reader();
  final launcher = _Launcher();
  final installerBytes = utf8.encode('synthetic installer bytes only');
  Stream<List<int>> Function()? responseStream;
  int shutdowns = 0;

  Future<SignedManifestPayload> signedPayload({
    String version = '1.1.0',
    int build = 2,
  }) async {
    final now = DateTime.now().toUtc();
    final bytes = utf8.encode(
      jsonEncode({
        'schemaVersion': 1,
        'appId': config.appId,
        'channel': config.channel,
        'platform': 'windows-x64',
        'version': version,
        'build': build,
        'minimumInstalledVersion': '1.0.0',
        'minimumInstalledBuild': 1,
        'issuedAt': now.subtract(const Duration(hours: 1)).toIso8601String(),
        'expiresAt': now.add(const Duration(hours: 1)).toIso8601String(),
        'installer': {
          'url': 'https://updates.example.invalid/installer.exe',
          'sizeBytes': installerBytes.length,
          'sha256': sha256.convert(installerBytes).toString(),
        },
      }),
    );
    final signature = await Ed25519().sign(bytes, keyPair: keyPair);
    return SignedManifestPayload(
      bytes: bytes,
      signatureBase64: base64.encode(signature.bytes),
    );
  }

  UpdateController controller() => UpdateController(
    config: config,
    versionReader: _VersionReader(),
    manifestReader: reader,
    downloadServiceFactory: (_) => InstallerDownloadService(
      allowedOrigins: {config.downloadOrigin},
      clientFactory: () => _Client(
        (request) async => http.StreamedResponse(
          responseStream?.call() ?? Stream.value(installerBytes),
          HttpStatus.ok,
          request: request,
        ),
      ),
      temporaryDirectoryProvider: () async => directory,
    ),
    installerServiceFactory: (installed) => WindowsInstallerService(
      installed: installed,
      launcher: launcher,
      requestDesktopShutdown: () async => shutdowns++,
    ),
  );

  Future<void> close() => directory.delete(recursive: true);
}

final class _Client extends http.BaseClient {
  _Client(this.handler);
  final Future<http.StreamedResponse> Function(http.BaseRequest) handler;

  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) =>
      handler(request);
}
