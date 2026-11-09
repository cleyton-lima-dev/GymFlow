import 'dart:async';
import 'dart:io';

import 'package:crypto/crypto.dart';
import 'package:http/http.dart' as http;
import 'package:path_provider/path_provider.dart';

import 'release_manifest_validator.dart';

enum InstallerDownloadFailureReason {
  unauthorizedOrigin,
  expiredManifest,
  invalidHttpResponse,
  sizeMismatch,
  hashMismatch,
  cancelled,
  networkError,
  fileError,
  cleanupFailed,
}

sealed class InstallerDownloadResult {
  const InstallerDownloadResult();
}

final class InstallerDownloadSuccess extends InstallerDownloadResult {
  const InstallerDownloadSuccess._verified(this.file, this.release);

  /// The caller owns this verified temporary file and must remove it when done.
  final File file;
  final ReleaseManifest release;
}

final class InstallerDownloadFailure extends InstallerDownloadResult {
  const InstallerDownloadFailure(this.reason);

  final InstallerDownloadFailureReason reason;
}

class DownloadCancellation {
  final Completer<void> _cancelled = Completer<void>();

  bool get isCancelled => _cancelled.isCompleted;
  Future<void> get whenCancelled => _cancelled.future;

  void cancel() {
    if (!_cancelled.isCompleted) _cancelled.complete();
  }
}

/// Downloads only a release minted by ReleaseManifestValidator. Every redirect
/// is rejected, including redirects that would otherwise preserve HTTPS.
class InstallerDownloadService {
  InstallerDownloadService({
    required Set<String> allowedOrigins,
    http.Client Function()? clientFactory,
    Future<Directory> Function()? temporaryDirectoryProvider,
    DateTime Function()? clock,
  }) : _allowedOrigins = Set.unmodifiable(allowedOrigins),
       _clientFactory = clientFactory ?? http.Client.new,
       _temporaryDirectoryProvider =
           temporaryDirectoryProvider ?? getTemporaryDirectory,
       _clock = clock ?? DateTime.now {
    if (_allowedOrigins.isEmpty) {
      throw ArgumentError('At least one trusted download origin is required.');
    }
    for (final origin in _allowedOrigins) {
      final uri = Uri.tryParse(origin);
      if (uri == null ||
          uri.scheme != 'https' ||
          uri.host.isEmpty ||
          uri.origin != origin) {
        throw ArgumentError('Invalid trusted download origin.');
      }
    }
  }

  final Set<String> _allowedOrigins;
  final http.Client Function() _clientFactory;
  final Future<Directory> Function() _temporaryDirectoryProvider;
  final DateTime Function() _clock;

  Future<InstallerDownloadResult> download(
    ReleaseManifest release, {
    DownloadCancellation? cancellation,
    void Function(int receivedBytes, int totalBytes)? onProgress,
  }) async {
    final url = release.installerUrl;
    if (url.scheme != 'https' || !_allowedOrigins.contains(url.origin)) {
      return const InstallerDownloadFailure(
        InstallerDownloadFailureReason.unauthorizedOrigin,
      );
    }
    if (!_manifestIsCurrent(release)) {
      return const InstallerDownloadFailure(
        InstallerDownloadFailureReason.expiredManifest,
      );
    }
    if (cancellation?.isCancelled ?? false) {
      return const InstallerDownloadFailure(
        InstallerDownloadFailureReason.cancelled,
      );
    }

    final client = _clientFactory();
    Directory? stagingDirectory;
    RandomAccessFile? output;
    InstallerDownloadSuccess? success;
    InstallerDownloadFailure? failure;

    try {
      final request = http.AbortableRequest(
        'GET',
        url,
        abortTrigger: cancellation?.whenCancelled,
      )..followRedirects = false;
      final response = await Future.any<http.StreamedResponse>([
        client.send(request),
        if (cancellation != null)
          cancellation.whenCancelled.then<http.StreamedResponse>(
            (_) => throw const _DownloadRejected(
              InstallerDownloadFailureReason.cancelled,
            ),
          ),
      ]);
      if (cancellation?.isCancelled ?? false) {
        throw const _DownloadRejected(InstallerDownloadFailureReason.cancelled);
      }
      if (response.request?.url != null && response.request!.url != url) {
        throw const _DownloadRejected(
          InstallerDownloadFailureReason.unauthorizedOrigin,
        );
      }
      if (response.statusCode != HttpStatus.ok) {
        throw const _DownloadRejected(
          InstallerDownloadFailureReason.invalidHttpResponse,
        );
      }
      if (response.contentLength != null &&
          response.contentLength != release.installerSizeBytes) {
        throw const _DownloadRejected(
          InstallerDownloadFailureReason.sizeMismatch,
        );
      }

      final temporaryDirectory = await _temporaryDirectoryProvider();
      stagingDirectory = await temporaryDirectory.createTemp('avelri-update-');
      if (cancellation?.isCancelled ?? false) {
        throw const _DownloadRejected(InstallerDownloadFailureReason.cancelled);
      }
      final file = File(
        '${stagingDirectory.path}${Platform.pathSeparator}installer.exe',
      );
      output = await file.open(mode: FileMode.writeOnly);

      final digestReceiver = _DigestReceiver();
      final digestInput = sha256.startChunkedConversion(digestReceiver);
      var receivedBytes = 0;
      await for (final chunk in response.stream) {
        if (cancellation?.isCancelled ?? false) {
          throw const _DownloadRejected(
            InstallerDownloadFailureReason.cancelled,
          );
        }
        receivedBytes += chunk.length;
        if (receivedBytes > release.installerSizeBytes) {
          throw const _DownloadRejected(
            InstallerDownloadFailureReason.sizeMismatch,
          );
        }
        await output.writeFrom(chunk);
        digestInput.add(chunk);
        onProgress?.call(receivedBytes, release.installerSizeBytes);
      }
      digestInput.close();
      await output.flush();
      await output.close();
      output = null;

      if (cancellation?.isCancelled ?? false) {
        throw const _DownloadRejected(InstallerDownloadFailureReason.cancelled);
      }
      if (receivedBytes != release.installerSizeBytes) {
        throw const _DownloadRejected(
          InstallerDownloadFailureReason.sizeMismatch,
        );
      }
      if (digestReceiver.value.toString() != release.installerSha256) {
        throw const _DownloadRejected(
          InstallerDownloadFailureReason.hashMismatch,
        );
      }
      if (!_manifestIsCurrent(release)) {
        throw const _DownloadRejected(
          InstallerDownloadFailureReason.expiredManifest,
        );
      }
      success = InstallerDownloadSuccess._verified(file, release);
    } on _DownloadRejected catch (error) {
      failure = InstallerDownloadFailure(error.reason);
    } on http.RequestAbortedException {
      failure = InstallerDownloadFailure(
        cancellation?.isCancelled ?? false
            ? InstallerDownloadFailureReason.cancelled
            : InstallerDownloadFailureReason.networkError,
      );
    } on FileSystemException {
      failure = const InstallerDownloadFailure(
        InstallerDownloadFailureReason.fileError,
      );
    } on Object {
      failure = const InstallerDownloadFailure(
        InstallerDownloadFailureReason.networkError,
      );
    } finally {
      try {
        await output?.close();
      } on FileSystemException {
        success = null;
        failure = const InstallerDownloadFailure(
          InstallerDownloadFailureReason.fileError,
        );
      }
      client.close();
    }

    if (failure != null && stagingDirectory != null) {
      try {
        await stagingDirectory.delete(recursive: true);
      } on FileSystemException {
        return const InstallerDownloadFailure(
          InstallerDownloadFailureReason.cleanupFailed,
        );
      }
    }
    return failure ?? success!;
  }

  bool _manifestIsCurrent(ReleaseManifest release) {
    final now = _clock().toUtc();
    return !release.issuedAt.isAfter(now) && release.expiresAt.isAfter(now);
  }
}

class _DownloadRejected implements Exception {
  const _DownloadRejected(this.reason);

  final InstallerDownloadFailureReason reason;
}

class _DigestReceiver implements Sink<Digest> {
  Digest? _value;

  Digest get value => _value!;

  @override
  void add(Digest data) => _value = data;

  @override
  void close() {}
}
