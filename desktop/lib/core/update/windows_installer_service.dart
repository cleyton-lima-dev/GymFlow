import 'dart:async';
import 'dart:io';

import 'package:crypto/crypto.dart';

import 'installer_download_service.dart';
import 'release_manifest_validator.dart';

enum InstallerStartFailureReason {
  busy,
  authorizationMissing,
  authorizationFailed,
  manifestExpired,
  incompatibleRelease,
  downgrade,
  fileMissing,
  sizeMismatch,
  hashMismatch,
  fileError,
  processStartFailed,
}

sealed class InstallerStartResult {
  const InstallerStartResult();
}

final class InstallerStarted extends InstallerStartResult {
  const InstallerStarted({required this.desktopCloseRequested});

  /// False means the installer started, but the Desktop shutdown request failed.
  final bool desktopCloseRequested;
}

final class InstallerStartFailed extends InstallerStartResult {
  const InstallerStartFailed(this.reason);

  final InstallerStartFailureReason reason;
}

abstract interface class InstallerProcessLauncher {
  /// Completes only after the OS confirms process creation.
  Future<void> start(File installer);
}

/// The Inno Setup installer is per-user and needs no shell or silent flags.
final class _WindowsInstallerProcessLauncher
    implements InstallerProcessLauncher {
  @override
  Future<void> start(File installer) async {
    if (!Platform.isWindows) {
      throw UnsupportedError('Windows installer requires Windows.');
    }
    await Process.start(
      installer.absolute.path,
      const [],
      workingDirectory: installer.parent.absolute.path,
      runInShell: false,
      mode: ProcessStartMode.detached,
    );
  }
}

/// Accepts only a successful Stage 7 download, which carries its signed release.
/// Confirmation and Desktop shutdown are supplied by the future UI layer.
final class WindowsInstallerService {
  factory WindowsInstallerService({
    required InstalledRelease installed,
    required InstallerProcessLauncher launcher,
    required Future<void> Function() requestDesktopShutdown,
    DateTime Function()? clock,
  }) => WindowsInstallerService._(
    installed,
    launcher,
    requestDesktopShutdown,
    clock ?? DateTime.now,
  );

  factory WindowsInstallerService.forWindows({
    required InstalledRelease installed,
    required Future<void> Function() requestDesktopShutdown,
    DateTime Function()? clock,
  }) => WindowsInstallerService._(
    installed,
    _WindowsInstallerProcessLauncher(),
    requestDesktopShutdown,
    clock ?? DateTime.now,
  );

  WindowsInstallerService._(
    this.installed,
    this._launcher,
    this._requestDesktopShutdown,
    this._clock,
  );

  final InstalledRelease installed;
  final InstallerProcessLauncher _launcher;
  final Future<void> Function() _requestDesktopShutdown;
  final DateTime Function() _clock;

  static bool _launchInProgress = false;
  bool _launchedFromThisService = false;

  Future<InstallerStartResult> startUpdate(
    InstallerDownloadSuccess download, {
    Future<bool> Function(ReleaseManifest release)? confirmUser,
  }) async {
    if (_launchInProgress || _launchedFromThisService) {
      return const InstallerStartFailed(InstallerStartFailureReason.busy);
    }
    _launchInProgress = true;
    try {
      // A missing confirmation callback must never imply consent.
      if (confirmUser == null) {
        return const InstallerStartFailed(
          InstallerStartFailureReason.authorizationMissing,
        );
      }

      var rejection = await _verify(download);
      if (rejection != null) return InstallerStartFailed(rejection);

      late final bool approved;
      try {
        approved = await confirmUser(download.release);
      } on Object {
        return const InstallerStartFailed(
          InstallerStartFailureReason.authorizationFailed,
        );
      }
      if (!approved) {
        return const InstallerStartFailed(
          InstallerStartFailureReason.authorizationMissing,
        );
      }

      // A file or the manifest can change while the confirmation is visible.
      rejection = await _verify(download);
      if (rejection != null) return InstallerStartFailed(rejection);

      try {
        await _launcher.start(download.file);
      } on Object {
        return const InstallerStartFailed(
          InstallerStartFailureReason.processStartFailed,
        );
      }
      _launchedFromThisService = true;

      try {
        await _requestDesktopShutdown();
        return const InstallerStarted(desktopCloseRequested: true);
      } on Object {
        // The installer is already running; report that the Desktop remains open.
        return const InstallerStarted(desktopCloseRequested: false);
      }
    } finally {
      _launchInProgress = false;
    }
  }

  Future<InstallerStartFailureReason?> _verify(
    InstallerDownloadSuccess download,
  ) async {
    final release = download.release;
    final now = _clock().toUtc();
    if (release.issuedAt.isAfter(now) || !release.expiresAt.isAfter(now)) {
      return InstallerStartFailureReason.manifestExpired;
    }
    if (release.appId != installed.appId ||
        release.channel != installed.channel ||
        release.platform != installed.platform ||
        installed.version.compareTo(release.minimumInstalledVersion) < 0 ||
        installed.build < release.minimumInstalledBuild) {
      return InstallerStartFailureReason.incompatibleRelease;
    }
    if (release.version.compareTo(installed.version) < 0 ||
        release.build <= installed.build) {
      return InstallerStartFailureReason.downgrade;
    }

    final file = download.file;
    try {
      if (await FileSystemEntity.type(file.path, followLinks: false) !=
          FileSystemEntityType.file) {
        return InstallerStartFailureReason.fileMissing;
      }
      if (await file.length() != release.installerSizeBytes) {
        return InstallerStartFailureReason.sizeMismatch;
      }

      final receiver = _DigestReceiver();
      final hashInput = sha256.startChunkedConversion(receiver);
      var bytesRead = 0;
      await for (final chunk in file.openRead()) {
        bytesRead += chunk.length;
        if (bytesRead > release.installerSizeBytes) {
          return InstallerStartFailureReason.sizeMismatch;
        }
        hashInput.add(chunk);
      }
      hashInput.close();
      if (bytesRead != release.installerSizeBytes) {
        return InstallerStartFailureReason.sizeMismatch;
      }
      if (receiver.value.toString() != release.installerSha256) {
        return InstallerStartFailureReason.hashMismatch;
      }
      if (!release.expiresAt.isAfter(_clock().toUtc())) {
        return InstallerStartFailureReason.manifestExpired;
      }
      return null;
    } on FileSystemException {
      return InstallerStartFailureReason.fileError;
    }
  }
}

class _DigestReceiver implements Sink<Digest> {
  Digest? _value;

  Digest get value => _value!;

  @override
  void add(Digest data) => _value = data;

  @override
  void close() {}
}
