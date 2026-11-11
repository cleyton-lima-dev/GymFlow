import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:avelri_gestao/app/router/management_shell_page.dart';
import 'package:avelri_gestao/app/session/session_status.dart';
import 'package:avelri_gestao/app/theme/app_theme.dart';
import 'package:avelri_gestao/app/theme/branding_controller.dart';
import 'package:avelri_gestao/app/theme/default_branding.dart';
import 'package:avelri_gestao/app/theme/local_branding_repository.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/core/update/signed_manifest_reader.dart';
import 'package:avelri_gestao/core/update/update_distribution_config.dart';
import 'package:avelri_gestao/core/update/update_state_store.dart';
import 'package:avelri_gestao/core/update/windows_installer_service.dart';
import 'package:avelri_gestao/features/dashboard/presentation/dashboard_page.dart';
import 'package:avelri_gestao/features/updates/presentation/update_check_scheduler.dart';
import 'package:avelri_gestao/features/updates/presentation/update_controller.dart';
import 'package:cryptography/cryptography.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart' as http_testing;
import 'package:provider/provider.dart';

import '../../support/management_test_scope.dart';

void main() {
  group('automatic update detection', () {
    late _UpdateFixture fixture;

    setUp(() async => fixture = await _UpdateFixture.create());
    tearDown(() async => fixture.close());

    test('checks only after an authenticated session starts', () async {
      fixture.reader.payload = await fixture.signedManifest();
      final controller = fixture.controller();
      final scheduler = UpdateCheckScheduler(controller: controller);

      await scheduler.handleSessionStatus(SessionStatus.unauthenticated);
      expect(fixture.reader.calls, 0);

      await scheduler.handleSessionStatus(SessionStatus.authenticated);
      expect(fixture.reader.calls, 1);
      expect(controller.hasPendingUpdate, isTrue);

      scheduler.dispose();
      controller.dispose();
    });

    test(
      'restores a signed cached release without an early network request',
      () async {
        fixture.reader.payload = await fixture.signedManifest();
        final first = fixture.controller();
        await first.check();
        first.dispose();

        fixture.reader.calls = 0;
        fixture.reader.fail = true;
        final restored = fixture.controller();
        final scheduler = UpdateCheckScheduler(controller: restored);
        await scheduler.handleSessionStatus(SessionStatus.authenticated);

        expect(fixture.reader.calls, 0);
        expect(restored.state, UpdateViewState.available);
        expect(restored.availableRelease?.version.toString(), '1.1.0');
        expect(restored.hasPendingUpdate, isTrue);

        scheduler.dispose();
        restored.dispose();
      },
    );

    test(
      'rechecks when the persisted 12-hour interval expires on resume',
      () async {
        fixture.reader.payload = await fixture.signedManifest();
        final controller = fixture.controller();
        final scheduler = UpdateCheckScheduler(controller: controller);
        await scheduler.handleSessionStatus(SessionStatus.authenticated);
        expect(fixture.reader.calls, 1);

        fixture.advance(const Duration(hours: 11, minutes: 59));
        await scheduler.onAppResumed();
        expect(fixture.reader.calls, 1);

        fixture.advance(const Duration(minutes: 1));
        await scheduler.onAppResumed();
        expect(fixture.reader.calls, 2);

        scheduler.dispose();
        controller.dispose();
      },
    );

    test(
      'runs the next check on its timer while the session stays open',
      () async {
        fixture.now = DateTime.now().toUtc();
        fixture.reader.payload = await fixture.signedManifest();
        final secondCall = Completer<void>();
        fixture.reader.onCall = (count) {
          if (count == 2) secondCall.complete();
        };
        final controller = fixture.controller(clock: DateTime.now);
        final scheduler = UpdateCheckScheduler(
          controller: controller,
          interval: const Duration(milliseconds: 25),
        );

        await scheduler.handleSessionStatus(SessionStatus.authenticated);
        await secondCall.future.timeout(const Duration(seconds: 2));

        expect(fixture.reader.calls, 2);
        scheduler.dispose();
        controller.dispose();
      },
    );

    test(
      'does not overlap a scheduled check with a request already running',
      () async {
        fixture.reader.payload = await fixture.signedManifest();
        fixture.reader.blocker = Completer<void>();
        final controller = fixture.controller();
        final scheduler = UpdateCheckScheduler(controller: controller);

        final firstCheck = scheduler.handleSessionStatus(
          SessionStatus.authenticated,
        );
        await Future<void>.delayed(Duration.zero);
        expect(fixture.reader.calls, 1);

        await scheduler.onAppResumed();
        expect(fixture.reader.calls, 1);

        fixture.reader.blocker!.complete();
        await firstCheck;
        scheduler.dispose();
        controller.dispose();
      },
    );

    test(
      'logout cancels scheduling and resume does not query anonymously',
      () async {
        fixture.reader.payload = await fixture.signedManifest();
        final controller = fixture.controller();
        final scheduler = UpdateCheckScheduler(controller: controller);
        await scheduler.handleSessionStatus(SessionStatus.authenticated);
        expect(fixture.reader.calls, 1);

        await scheduler.handleSessionStatus(SessionStatus.unauthenticated);
        fixture.advance(const Duration(days: 1));
        await scheduler.onAppResumed();
        expect(fixture.reader.calls, 1);

        scheduler.dispose();
        controller.dispose();
      },
    );

    test(
      'missing configuration makes no request and schedules nothing',
      () async {
        final controller = fixture.controller(configured: false);
        final scheduler = UpdateCheckScheduler(controller: controller);

        await scheduler.handleSessionStatus(SessionStatus.authenticated);

        expect(fixture.reader.calls, 0);
        expect(await fixture.store.readLastCheckAt(), isNull);
        expect(controller.state, UpdateViewState.notConfigured);

        scheduler.dispose();
        controller.dispose();
      },
    );

    test(
      'network failure preserves a still-valid previously verified release',
      () async {
        fixture.reader.payload = await fixture.signedManifest(
          expiresAt: fixture.now.add(const Duration(days: 2)),
        );
        final controller = fixture.controller();
        await controller.check();
        fixture.reader.fail = true;
        await controller.check();

        expect(controller.state, UpdateViewState.error);
        expect(controller.hasPendingUpdate, isTrue);
        expect(await fixture.store.readPendingManifest(), isNotNull);

        controller.dispose();
      },
    );

    test('expired cached manifests are removed and never advertised', () async {
      await fixture.store.writePendingManifest(
        await fixture.signedManifest(
          issuedAt: fixture.now.subtract(const Duration(days: 1)),
          expiresAt: fixture.now.subtract(const Duration(minutes: 1)),
        ),
      );
      final controller = fixture.controller();

      await controller.restoreCachedAvailability();

      expect(controller.availableRelease, isNull);
      expect(controller.hasPendingUpdate, isFalse);
      expect(await fixture.store.readPendingManifest(), isNull);

      controller.dispose();
    });

    test('cached signature is revalidated before display', () async {
      final valid = await fixture.signedManifest();
      await fixture.store.writePendingManifest(
        SignedManifestPayload(
          bytes: valid.bytes,
          signatureBase64: base64.encode(List<int>.filled(64, 0)),
        ),
      );
      final controller = fixture.controller();

      await controller.restoreCachedAvailability();

      expect(controller.availableRelease, isNull);
      expect(controller.hasPendingUpdate, isFalse);
      expect(await fixture.store.readPendingManifest(), isNull);

      controller.dispose();
    });

    test(
      'a valid current-version manifest clears a pending update cache',
      () async {
        await fixture.store.writePendingManifest(
          await fixture.signedManifest(),
        );
        final controller = fixture.controller(
          installedVersion: '1.1.0',
          installedBuild: 2,
        );

        await controller.restoreCachedAvailability();

        expect(controller.availableRelease, isNull);
        expect(controller.hasPendingUpdate, isFalse);
        expect(await fixture.store.readPendingManifest(), isNull);

        controller.dispose();
      },
    );

    testWidgets(
      'dashboard banner shows the version and opens the existing dialog',
      (tester) async {
        fixture.reader.payload = await fixture.signedManifest();
        final controller = fixture.controller();
        await controller.check();
        final api = _dashboardApi();
        final branding = BrandingController(
          const LocalBrandingRepository({}),
          defaultBranding: avelriDefaultBranding,
        );
        addTearDown(() {
          controller.dispose();
          branding.dispose();
          api.close();
        });

        await tester.pumpWidget(
          ManagementTestScope(
            api: api,
            child: MultiProvider(
              providers: [
                ChangeNotifierProvider<BrandingController>.value(
                  value: branding,
                ),
                ChangeNotifierProvider<UpdateController>.value(
                  value: controller,
                ),
              ],
              child: MaterialApp(
                theme: AppTheme.fromBranding(avelriDefaultBranding),
                home: ManagementShellPage(
                  location: '/dashboard',
                  child: const DashboardPage(),
                ),
              ),
            ),
          ),
        );
        await tester.pumpAndSettle();

        expect(find.text('Nova atualização disponível'), findsOneWidget);
        expect(find.text('Versão 1.1.0'), findsOneWidget);
        await tester.tap(find.text('Ver atualização'));
        await tester.pumpAndSettle();
        expect(find.text('Baixar atualização'), findsOneWidget);
        expect(fixture.reader.calls, 1);
      },
    );

    testWidgets('manual menu option remains and forces a fresh check', (
      tester,
    ) async {
      fixture.reader.payload = await fixture.signedManifest();
      final controller = fixture.controller();
      final api = _dashboardApi();
      final branding = BrandingController(
        const LocalBrandingRepository({}),
        defaultBranding: avelriDefaultBranding,
      );
      addTearDown(() {
        controller.dispose();
        branding.dispose();
        api.close();
      });

      await tester.pumpWidget(
        ManagementTestScope(
          api: api,
          child: MultiProvider(
            providers: [
              ChangeNotifierProvider<BrandingController>.value(value: branding),
              ChangeNotifierProvider<UpdateController>.value(value: controller),
            ],
            child: MaterialApp(
              theme: AppTheme.fromBranding(avelriDefaultBranding),
              home: ManagementShellPage(
                location: '/dashboard',
                child: const SizedBox.shrink(),
              ),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Verificar atualizações'), findsOneWidget);
      await tester.tap(find.text('Verificar atualizações'));
      await tester.pumpAndSettle();

      expect(fixture.reader.calls, 1);
      expect(find.text('Atualização disponível:'), findsOneWidget);
    });
  });

  test(
    'file state store persists exact signed bytes and the 12-hour marker',
    () async {
      final directory = await Directory.systemTemp.createTemp(
        'avelri-update-state-',
      );
      addTearDown(() => directory.delete(recursive: true));
      final store = FileUpdateStateStore.inDirectory(directory);
      const payload = SignedManifestPayload(
        bytes: [0, 1, 2, 255],
        signatureBase64: 'synthetic-signature-bytes',
      );
      final checkedAt = DateTime.utc(2026, 11, 10, 12);

      await store.writeLastCheckAt(checkedAt);
      await store.writePendingManifest(payload);

      expect(await store.readLastCheckAt(), checkedAt);
      final cached = await store.readPendingManifest();
      expect(cached?.bytes, payload.bytes);
      expect(cached?.signatureBase64, payload.signatureBase64);
    },
  );
}

ApiClient _dashboardApi() => ApiClient(
  baseUrl: 'https://synthetic.invalid',
  client: http_testing.MockClient(
    (_) async => http.Response(
      jsonEncode({
        'today': '2026-11-10',
        'periodStart': '2026-11-01',
        'expectedRevenue': 0,
        'receivedRevenue': 0,
        'overdueAmount': 0,
        'overdueCharges': 0,
        'activeEnrollments': 0,
        'expiringEnrollments': 0,
        'activeStudents': 0,
        'studentsWithoutAssessment': 0,
        'reassessmentsDue': 0,
        'expiringWithinDays': 15,
      }),
      200,
    ),
  ),
);

final class _UpdateFixture {
  _UpdateFixture._(this.keyPair, this.config);

  static Future<_UpdateFixture> create() async {
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
      keyId: 'synthetic-test-key',
      publicKeyBase64: base64.encode(publicKey.bytes),
    )!;
    return _UpdateFixture._(keyPair, config);
  }

  final SimpleKeyPair keyPair;
  final UpdateDistributionConfig config;
  final InMemoryUpdateStateStore store = InMemoryUpdateStateStore();
  final _ManifestReader reader = _ManifestReader();
  DateTime now = DateTime.utc(2026, 11, 10, 12);

  void advance(Duration duration) => now = now.add(duration);

  UpdateController controller({
    bool configured = true,
    String installedVersion = '1.0.0',
    int installedBuild = 1,
    DateTime Function()? clock,
  }) => UpdateController(
    config: configured ? config : null,
    versionReader: _VersionReader(installedVersion, installedBuild),
    manifestReader: reader,
    downloadServiceFactory: (_) => throw StateError('unexpected download'),
    installerServiceFactory: (installed) => WindowsInstallerService(
      installed: installed,
      launcher: _TestLauncher(),
      requestDesktopShutdown: () async {},
    ),
    stateStore: store,
    clock: clock ?? () => now,
  );

  Future<SignedManifestPayload> signedManifest({
    String version = '1.1.0',
    int build = 2,
    DateTime? issuedAt,
    DateTime? expiresAt,
  }) async {
    final issueTime = issuedAt ?? now.subtract(const Duration(minutes: 5));
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
        'issuedAt': issueTime.toIso8601String(),
        'expiresAt': (expiresAt ?? now.add(const Duration(days: 1)))
            .toIso8601String(),
        'installer': {
          'url': 'https://updates.example.invalid/installer.exe',
          'sizeBytes': 1,
          'sha256': List<String>.filled(64, '0').join(),
        },
      }),
    );
    final signature = await Ed25519().sign(bytes, keyPair: keyPair);
    return SignedManifestPayload(
      bytes: bytes,
      signatureBase64: base64.encode(signature.bytes),
    );
  }

  Future<void> close() async {}
}

final class _VersionReader implements InstalledVersionReader {
  const _VersionReader(this.version, this.build);
  final String version;
  final int build;

  @override
  Future<InstalledAppVersion> read() async =>
      InstalledAppVersion(version, build);
}

final class _ManifestReader implements SignedManifestReader {
  SignedManifestPayload? payload;
  Completer<void>? blocker;
  int calls = 0;
  bool fail = false;
  void Function(int)? onCall;

  @override
  Future<SignedManifestPayload> read(UpdateDistributionConfig config) async {
    calls++;
    onCall?.call(calls);
    await blocker?.future;
    if (fail) throw const SocketException('synthetic network failure');
    return payload!;
  }
}

final class _TestLauncher implements InstallerProcessLauncher {
  @override
  Future<void> start(File installer) async {}
}
