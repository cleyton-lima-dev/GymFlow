import 'dart:async';
import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:package_info_plus/package_info_plus.dart';

import 'package:avelri_gestao/core/update/installer_download_service.dart';
import 'package:avelri_gestao/core/update/release_manifest_validator.dart';
import 'package:avelri_gestao/core/update/signed_manifest_reader.dart';
import 'package:avelri_gestao/core/update/update_distribution_config.dart';
import 'package:avelri_gestao/core/update/update_state_store.dart';
import 'package:avelri_gestao/core/update/windows_installer_service.dart';

enum UpdateViewState {
  notConfigured,
  checking,
  current,
  available,
  downloading,
  ready,
  installing,
  started,
  error,
}

final class InstalledAppVersion {
  const InstalledAppVersion(this.version, this.build);

  final String version;
  final int build;
}

abstract interface class InstalledVersionReader {
  Future<InstalledAppVersion> read();
}

final class PackageInfoVersionReader implements InstalledVersionReader {
  @override
  Future<InstalledAppVersion> read() async {
    final info = await PackageInfo.fromPlatform();
    final build = int.tryParse(info.buildNumber);
    if (build == null || build < 1) {
      throw const FormatException('Invalid installed build.');
    }
    ReleaseVersion.parse(info.version);
    return InstalledAppVersion(info.version, build);
  }
}

typedef DownloadServiceFactory = InstallerDownloadService Function(
  UpdateDistributionConfig config,
);
typedef InstallerServiceFactory = WindowsInstallerService Function(
  InstalledRelease installed,
);

final class UpdateController extends ChangeNotifier {
  UpdateController({
    required this.config,
    required this.versionReader,
    required this.manifestReader,
    required this.downloadServiceFactory,
    required this.installerServiceFactory,
    UpdateStateStore? stateStore,
    DateTime Function()? clock,
  }) : stateStore = stateStore ?? InMemoryUpdateStateStore(),
       _clock = clock ?? DateTime.now;

  factory UpdateController.forDesktop({
    required Future<void> Function() requestDesktopShutdown,
  }) => UpdateController(
    config: UpdateDistributionConfig.fromEnvironment(),
    versionReader: PackageInfoVersionReader(),
    manifestReader: HttpsSignedManifestReader(),
    downloadServiceFactory: (config) =>
        InstallerDownloadService(allowedOrigins: {config.downloadOrigin}),
    installerServiceFactory: (installed) => WindowsInstallerService.forWindows(
      installed: installed,
      requestDesktopShutdown: requestDesktopShutdown,
    ),
    stateStore: FileUpdateStateStore.applicationSupport(),
  );

  final UpdateDistributionConfig? config;
  final InstalledVersionReader versionReader;
  final SignedManifestReader manifestReader;
  final DownloadServiceFactory downloadServiceFactory;
  final InstallerServiceFactory installerServiceFactory;
  final UpdateStateStore stateStore;
  final DateTime Function() _clock;

  UpdateViewState state = UpdateViewState.notConfigured;
  InstalledAppVersion? installedVersion;
  ReleaseManifest? availableRelease;
  InstallerDownloadSuccess? verifiedDownload;
  String? errorMessage;
  double progress = 0;

  WindowsInstallerService? _installer;
  DownloadCancellation? _cancellation;
  bool _disposed = false;
  bool _retryDownload = false;
  bool _installerStarted = false;
  bool _automaticChecksUnavailable = false;
  DateTime? _lastCheckAtUtc;
  Future<void>? _pendingRestore;

  bool get isBusy => _busy;

  bool get shouldDeferAutomaticCheck =>
      _busy ||
      state == UpdateViewState.ready ||
      state == UpdateViewState.started;

  bool get hasPendingUpdate {
    final release = availableRelease;
    if (release == null || !release.expiresAt.isAfter(_clock().toUtc())) {
      return false;
    }
    final installed = installedVersion;
    if (installed == null) return true;
    final comparison = ReleaseVersion.parse(installed.version)
        .compareTo(release.version);
    return comparison < 0 ||
        (comparison == 0 && installed.build < release.build);
  }

  Future<void> initialize() async {
    try {
      installedVersion ??= await versionReader.read();
      _notify();
      if (config == null || _disposed) return;
      await check();
    } on Object {
      if (config != null) _fail('Não foi possível ler a versão instalada.');
    }
  }

  Future<void> check() => _check();

  Future<void> _check({bool attemptAlreadyPersisted = false}) async {
    final distribution = config;
    if (distribution == null || _disposed || _busy) return;
    final pendingRestore = _pendingRestore;
    if (pendingRestore != null) await pendingRestore;
    if (_disposed || _busy) return;
    _setState(UpdateViewState.checking);
    errorMessage = null;
    _retryDownload = false;
    if (!attemptAlreadyPersisted) {
      _lastCheckAtUtc = _clock().toUtc();
      try {
        await stateStore.writeLastCheckAt(_lastCheckAtUtc!);
        _automaticChecksUnavailable = false;
      } on Object {
        // Manual checks still work, but auto checks stop if their schedule
        // cannot be persisted safely.
        _automaticChecksUnavailable = true;
      }
    }
    try {
      final local = installedVersion ??= await versionReader.read();
      final installed = _installedRelease(distribution, local);
      _installer ??= installerServiceFactory(installed);
      final payload = await manifestReader.read(distribution);
      if (_disposed) return;
      final result = await _inspect(distribution, installed, payload);
      if (_disposed) return;
      if (result is CurrentRelease) {
        availableRelease = null;
        await _clearCachedManifest();
        _setState(UpdateViewState.current);
      } else if (result is AvailableRelease) {
        availableRelease = result.manifest;
        try {
          await stateStore.writePendingManifest(payload);
        } on Object {
          // The verified result remains usable in memory if local storage fails.
        }
        _setState(UpdateViewState.available);
      }
    } on Object {
      _fail('Não foi possível verificar a atualização com segurança.');
    }
  }

  /// Restores only a cached payload that passes the same signature, identity,
  /// compatibility, installed-version, and expiry checks as a network response.
  Future<void> restoreCachedAvailability() async {
    if (config == null || _disposed) return;
    final existing = _pendingRestore;
    if (existing != null) return existing;
    final restore = _restoreCachedAvailability();
    _pendingRestore = restore;
    try {
      await restore;
    } finally {
      if (identical(_pendingRestore, restore)) _pendingRestore = null;
    }
  }

  Future<void> _restoreCachedAvailability() async {
    final distribution = config;
    if (distribution == null || _disposed) return;
    try {
      _lastCheckAtUtc = (await stateStore.readLastCheckAt())?.toUtc();
      final payload = await stateStore.readPendingManifest();
      if (payload == null || _disposed) return;
      final local = installedVersion ??= await versionReader.read();
      final installed = _installedRelease(distribution, local);
      final result = await _inspect(distribution, installed, payload);
      if (_disposed) return;
      if (result is AvailableRelease) {
        availableRelease = result.manifest;
        _setState(UpdateViewState.available);
      } else {
        availableRelease = null;
        await _clearCachedManifest();
        _notify();
      }
    } on Object {
      availableRelease = null;
      await _clearCachedManifest();
      _notify();
    }
  }

  Future<bool> checkIfDue({
    Duration interval = const Duration(hours: 12),
  }) async {
    if (config == null ||
        _disposed ||
        shouldDeferAutomaticCheck ||
        _automaticChecksUnavailable) {
      return false;
    }
    DateTime? lastCheck = _lastCheckAtUtc;
    if (lastCheck == null) {
      try {
        lastCheck = (await stateStore.readLastCheckAt())?.toUtc();
        _lastCheckAtUtc = lastCheck;
      } on Object {
        _automaticChecksUnavailable = true;
        return false;
      }
    }
    if (_disposed || shouldDeferAutomaticCheck) return false;
    final now = _clock().toUtc();
    if (lastCheck != null &&
        !lastCheck.isAfter(now) &&
        now.isBefore(lastCheck.add(interval))) {
      return false;
    }
    final attemptAt = now;
    try {
      await stateStore.writeLastCheckAt(attemptAt);
      _lastCheckAtUtc = attemptAt;
      _automaticChecksUnavailable = false;
    } on Object {
      _automaticChecksUnavailable = true;
      return false;
    }
    await _check(attemptAlreadyPersisted: true);
    return true;
  }

  Future<Duration?> timeUntilCheckDue({
    Duration interval = const Duration(hours: 12),
  }) async {
    if (config == null || _disposed || _automaticChecksUnavailable) return null;
    DateTime? lastCheck = _lastCheckAtUtc;
    if (lastCheck == null) {
      try {
        lastCheck = (await stateStore.readLastCheckAt())?.toUtc();
        _lastCheckAtUtc = lastCheck;
      } on Object {
        _automaticChecksUnavailable = true;
        return null;
      }
    }
    if (lastCheck == null || lastCheck.isAfter(_clock().toUtc())) {
      return Duration.zero;
    }
    final remaining = lastCheck.add(interval).difference(_clock().toUtc());
    return remaining.isNegative ? Duration.zero : remaining;
  }

  Duration? get timeUntilPendingReleaseExpires {
    if (state == UpdateViewState.downloading ||
        state == UpdateViewState.ready ||
        state == UpdateViewState.installing ||
        state == UpdateViewState.started) {
      return null;
    }
    final release = availableRelease;
    if (release == null) return null;
    final remaining = release.expiresAt.difference(_clock().toUtc());
    return remaining.isNegative ? Duration.zero : remaining;
  }

  Future<void> expirePendingAvailability() async {
    final release = availableRelease;
    if (release == null || release.expiresAt.isAfter(_clock().toUtc())) return;
    if (state == UpdateViewState.downloading ||
        state == UpdateViewState.ready ||
        state == UpdateViewState.installing ||
        state == UpdateViewState.started) {
      return;
    }
    availableRelease = null;
    errorMessage = 'A atualização disponível expirou. Verifique novamente.';
    if (state == UpdateViewState.available) state = UpdateViewState.error;
    await _clearCachedManifest();
    _notify();
  }

  Future<void> handleDialogClosed() async {
    if (_installerStarted || state == UpdateViewState.installing) return;
    cancelDownload();
    final download = verifiedDownload;
    if (download == null) return;
    verifiedDownload = null;
    await _removeVerifiedFile(download);
    if (state == UpdateViewState.ready || state == UpdateViewState.error) {
      state = hasPendingUpdate
          ? UpdateViewState.available
          : UpdateViewState.notConfigured;
      errorMessage = null;
      _notify();
    }
  }

  Future<void> download() async {
    final distribution = config;
    final release = availableRelease;
    if (distribution == null || release == null || _disposed || _busy) return;
    _cancellation = DownloadCancellation();
    progress = 0;
    errorMessage = null;
    _setState(UpdateViewState.downloading);
    try {
      final result = await downloadServiceFactory(distribution).download(
        release,
        cancellation: _cancellation,
        onProgress: (received, total) {
          if (_disposed) return;
          progress = received / total;
          _notify();
        },
      );
      if (_disposed) {
        if (result is InstallerDownloadSuccess) {
          await _removeVerifiedFile(result);
        }
        return;
      }
      if (result is InstallerDownloadSuccess) {
        verifiedDownload = result;
        _setState(UpdateViewState.ready);
      } else if (result is InstallerDownloadFailure &&
          result.reason == InstallerDownloadFailureReason.cancelled) {
        _setState(UpdateViewState.available);
      } else {
        _retryDownload = true;
        _fail('Não foi possível baixar e verificar o instalador.');
      }
    } on Object {
      _retryDownload = true;
      _fail('Não foi possível baixar e verificar o instalador.');
    } finally {
      _cancellation = null;
    }
  }

  void cancelDownload() => _cancellation?.cancel();

  Future<void> install(
    Future<bool> Function(ReleaseManifest release) confirmUser,
  ) async {
    final download = verifiedDownload;
    final installer = _installer;
    if (download == null || installer == null || _disposed || _busy) return;
    _setState(UpdateViewState.installing);
    final result = await installer.startUpdate(
      download,
      confirmUser: confirmUser,
    );
    if (_disposed) return;
    if (result is InstallerStarted) {
      _installerStarted = true;
      errorMessage = result.desktopCloseRequested
          ? null
          : 'Instalador iniciado. Feche o Avelri Gestão manualmente.';
      _setState(UpdateViewState.started);
    } else if (result is InstallerStartFailed &&
        result.reason == InstallerStartFailureReason.authorizationMissing) {
      _setState(UpdateViewState.ready);
    } else {
      _fail(
        'Não foi possível iniciar o instalador. O aplicativo permanece aberto.',
      );
    }
  }

  Future<void> retry() => _retryDownload ? download() : check();

  InstalledRelease _installedRelease(
    UpdateDistributionConfig distribution,
    InstalledAppVersion local,
  ) => InstalledRelease(
    appId: distribution.appId,
    channel: distribution.channel,
    platform: 'windows-x64',
    version: local.version,
    build: local.build,
  );

  Future<ReleaseInspection> _inspect(
    UpdateDistributionConfig distribution,
    InstalledRelease installed,
    SignedManifestPayload payload,
  ) =>
      ReleaseManifestValidator(
        installed: installed,
        trustedPublicKeys: {distribution.keyId: distribution.publicKey},
        clock: _clock,
      ).inspect(
        manifestBytes: payload.bytes,
        signatureBase64: payload.signatureBase64,
        keyId: distribution.keyId,
      );

  Future<void> _clearCachedManifest() async {
    try {
      await stateStore.clearPendingManifest();
    } on Object {
      // An unreadable cache is revalidated before it can ever be shown again.
    }
  }

  bool get _busy =>
      state == UpdateViewState.checking ||
      state == UpdateViewState.downloading ||
      state == UpdateViewState.installing;

  void _setState(UpdateViewState next) {
    if (_disposed) return;
    state = next;
    _notify();
  }

  void _fail(String message) {
    errorMessage = message;
    _setState(UpdateViewState.error);
  }

  void _notify() {
    if (!_disposed) notifyListeners();
  }

  Future<void> _removeVerifiedFile(InstallerDownloadSuccess download) async {
    try {
      await download.file.parent.delete(recursive: true);
    } on FileSystemException {
      // A failed cleanup must not affect another user's file or directory.
    }
  }

  @override
  void dispose() {
    _cancellation?.cancel();
    if (!_installerStarted && verifiedDownload != null) {
      unawaited(_removeVerifiedFile(verifiedDownload!));
    }
    _disposed = true;
    super.dispose();
  }
}
