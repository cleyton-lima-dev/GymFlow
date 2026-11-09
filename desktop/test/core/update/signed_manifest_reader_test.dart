import 'dart:convert';
import 'dart:io';

import 'package:avelri_gestao/core/update/signed_manifest_reader.dart';
import 'package:avelri_gestao/core/update/update_distribution_config.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;

void main() {
  UpdateDistributionConfig? config({
    bool enabled = true,
    String manifestUrl = 'https://updates.example.invalid/release.json',
    String signatureUrl = 'https://updates.example.invalid/release.sig',
    String downloadOrigin = 'https://updates.example.invalid',
    String appId = UpdateDistributionConfig.productionAppId,
    String channel = 'production',
  }) => UpdateDistributionConfig.tryCreate(
    enabled: enabled,
    appEnvironment: 'production',
    appId: appId,
    channel: channel,
    manifestUrl: manifestUrl,
    signatureUrl: signatureUrl,
    downloadOrigin: downloadOrigin,
    keyId: 'synthetic-key',
    publicKeyBase64: base64.encode(List<int>.filled(32, 1)),
  );

  test(
    'configuration fails closed without all trusted distribution settings',
    () {
      expect(config(enabled: false), isNull);
      expect(config(manifestUrl: ''), isNull);
      expect(config(signatureUrl: 'https://other.example.invalid/sig'), isNull);
      expect(config(downloadOrigin: 'http://updates.example.invalid'), isNull);
      expect(config(appId: 'wrong-app'), isNull);
      expect(config(channel: 'homologation'), isNull);
      expect(config(), isNotNull);
    },
  );

  test(
    'reader returns exact bytes and detached signature without parsing JSON',
    () async {
      final bytes = utf8.encode('this is signed but not JSON');
      final signature = base64.encode(List<int>.filled(64, 9));
      var requests = 0;
      final reader = HttpsSignedManifestReader(
        clientFactory: () => _Client((request) async {
          requests++;
          expect(request.followRedirects, isFalse);
          return http.StreamedResponse(
            Stream.value(
              request.url.path.endsWith('.json')
                  ? bytes
                  : utf8.encode(signature),
            ),
            HttpStatus.ok,
            request: request,
          );
        }),
      );
      final payload = await reader.read(config()!);
      expect(payload.bytes, bytes);
      expect(payload.signatureBase64, signature);
      expect(requests, 2);
    },
  );

  test('reader rejects redirect before requesting signature', () async {
    var requests = 0;
    final reader = HttpsSignedManifestReader(
      clientFactory: () => _Client((request) async {
        requests++;
        expect(request.followRedirects, isFalse);
        return http.StreamedResponse(
          Stream<List<int>>.empty(),
          HttpStatus.found,
          request: request,
        );
      }),
    );
    await expectLater(
      reader.read(config()!),
      throwsA(
        isA<ManifestReadException>().having(
          (error) => error.reason,
          'reason',
          ManifestReadFailure.http,
        ),
      ),
    );
    expect(requests, 1);
  });

  test('reader rejects oversized response while streaming', () async {
    final reader = HttpsSignedManifestReader(
      clientFactory: () => _Client(
        (request) async => http.StreamedResponse(
          Stream.value(List<int>.filled(16 * 1024 + 1, 1)),
          HttpStatus.ok,
          request: request,
        ),
      ),
    );
    await expectLater(
      reader.read(config()!),
      throwsA(
        isA<ManifestReadException>().having(
          (error) => error.reason,
          'reason',
          ManifestReadFailure.oversized,
        ),
      ),
    );
  });
}

final class _Client extends http.BaseClient {
  _Client(this.handler);
  final Future<http.StreamedResponse> Function(http.BaseRequest) handler;

  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) =>
      handler(request);
}
