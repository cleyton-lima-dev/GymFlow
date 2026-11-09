import 'dart:async';
import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:package_info_plus/package_info_plus.dart';

import 'package:avelri_gestao/core/update/installer_download_service.dart';
import 'package:avelri_gestao/core/update/release_manifest_validator.dart';
import 'package:avelri_gestao/core/update/signed_manifest_reader.dart';
import 'package:avelri_gestao/core/update/update_distribution_config.dart';
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
  });

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
  );

  final UpdateDistributionConfig? config;
  final InstalledVersionReader versionReader;
  final SignedManifestReader manifestReader;
  final DownloadServiceFactory downloadServiceFactory;
  final InstallerServiceFactory installerServiceFactory;

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

  Future<void> check() async {
    final distribution = config;
    if (distribution == null || _disposed || _busy) return;
    _setState(UpdateViewState.checking);
    availableRelease = null;
    errorMessage = null;
    _retryDownload = false;
    try {
      final local = installedVersion ??= await versionReader.read();
      final installed = InstalledRelease(
        appId: distribution.appId,
        channel: distribution.channel,
        platform: 'windows-x64',
        version: local.version,
        build: local.build,
      );
      _installer ??= installerServiceFactory(installed);
      final payload = await manifestReader.read(distribution);
      if (_disposed) return;
      final result =
          await ReleaseManifestValidator(
            installed: installed,
            trustedPublicKeys: {distribution.keyId: distribution.publicKey},
          ).inspect(
            manifestBytes: payload.bytes,
            signatureBase64: payload.signatureBase64,
            keyId: distribution.keyId,
          );
      if (_disposed) return;
      if (result is CurrentRelease) {
        _setState(UpdateViewState.current);
      } else if (result is AvailableRelease) {
        availableRelease = result.manifest;
        _setState(UpdateViewState.available);
      }
    } on Object {
      _fail('Não foi possível verificar a atualização com segurança.');
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
