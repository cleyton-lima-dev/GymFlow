import 'dart:async';
import 'dart:convert';

import 'package:http/http.dart' as http;

import 'release_manifest_validator.dart';
import 'update_distribution_config.dart';

final class SignedManifestPayload {
  const SignedManifestPayload({
    required this.bytes,
    required this.signatureBase64,
  });

  final List<int> bytes;
  final String signatureBase64;
}

abstract interface class SignedManifestReader {
  Future<SignedManifestPayload> read(UpdateDistributionConfig config);
}

enum ManifestReadFailure {
  http,
  oversized,
  timedOut,
  network,
  malformedSignature,
}

final class ManifestReadException implements Exception {
  const ManifestReadException(this.reason);

  final ManifestReadFailure reason;
}

/// Only transports bounded bytes. The caller must verify Ed25519 before JSON.
final class HttpsSignedManifestReader implements SignedManifestReader {
  HttpsSignedManifestReader({
    http.Client Function()? clientFactory,
    this.timeout = const Duration(seconds: 10),
  }) : _clientFactory = clientFactory ?? http.Client.new;

  final http.Client Function() _clientFactory;
  final Duration timeout;

  @override
  Future<SignedManifestPayload> read(UpdateDistributionConfig config) async {
    final client = _clientFactory();
    try {
      final bytes = await _readBounded(
        client,
        config.manifestUrl,
        ReleaseManifestValidator.maximumManifestBytes,
      );
      final signatureBytes = await _readBounded(
        client,
        config.signatureUrl,
        128,
      );
      try {
        return SignedManifestPayload(
          bytes: bytes,
          signatureBase64: utf8.decode(signatureBytes, allowMalformed: false),
        );
      } on FormatException {
        throw const ManifestReadException(
          ManifestReadFailure.malformedSignature,
        );
      }
    } finally {
      client.close();
    }
  }

  Future<List<int>> _readBounded(http.Client client, Uri url, int limit) async {
    final abort = Completer<void>();
    try {
      return await _read(client, url, limit, abort.future).timeout(timeout);
    } on TimeoutException {
      abort.complete();
      throw const ManifestReadException(ManifestReadFailure.timedOut);
    } on ManifestReadException {
      rethrow;
    } on Object {
      throw const ManifestReadException(ManifestReadFailure.network);
    }
  }

  Future<List<int>> _read(
    http.Client client,
    Uri url,
    int limit,
    Future<void> abortTrigger,
  ) async {
    final request = http.AbortableRequest(
      'GET',
      url,
      abortTrigger: abortTrigger,
    )..followRedirects = false;
    final response = await client.send(request);
    if (response.statusCode != 200 ||
        (response.request?.url != null && response.request!.url != url)) {
      throw const ManifestReadException(ManifestReadFailure.http);
    }
    if (response.contentLength != null && response.contentLength! > limit) {
      throw const ManifestReadException(ManifestReadFailure.oversized);
    }
    final bytes = <int>[];
    await for (final chunk in response.stream) {
      if (bytes.length + chunk.length > limit) {
        throw const ManifestReadException(ManifestReadFailure.oversized);
      }
      bytes.addAll(chunk);
    }
    return bytes;
  }
}
