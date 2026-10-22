import 'dart:async';

import 'package:avelri_gestao/app/session/erp_feature_controller.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

void main() {
  test(
    'enables ERP only from the authenticated tenant feature response',
    () async {
      final client = ApiClient(
        baseUrl: 'https://api.example.invalid/',
        client: MockClient((request) async {
          expect(request.url.path, '/api/gym-features');
          expect(request.headers['authorization'], 'Bearer admin-token');
          return http.Response('{"erpEnabled":true}', 200);
        }),
      )..setAccessToken('admin-token');
      final controller = ErpFeatureController(client);
      addTearDown(client.close);
      addTearDown(controller.dispose);

      await controller.loadForGym('gym-enabled');

      expect(controller.isLoadedForGym('gym-enabled'), isTrue);
      expect(controller.isEnabledForGym('gym-enabled'), isTrue);
      expect(controller.isEnabledForGym('another-gym'), isFalse);
    },
  );

  test('missing configuration and unavailable endpoint fail closed without blocking legacy access', () async {
    final client = ApiClient(
      baseUrl: 'https://api.example.invalid/',
      client: MockClient((_) async => http.Response('not found', 404)),
    );
    final controller = ErpFeatureController(client);
    addTearDown(client.close);
    addTearDown(controller.dispose);

    await controller.loadForGym('legacy-gym');

    expect(controller.isLoadedForGym('legacy-gym'), isTrue);
    expect(controller.isEnabledForGym('legacy-gym'), isFalse);
  });

  test(
    'same-gym replacement session cannot use a previous permission',
    () async {
      var enabled = true;
      final client = ApiClient(
        baseUrl: 'https://api.example.invalid/',
        client: MockClient(
          (_) async => http.Response('{"erpEnabled":$enabled}', 200),
        ),
      )..setAccessToken('first-user');
      final controller = ErpFeatureController(client);
      await controller.loadForGym('same-gym');
      expect(controller.isEnabledForGym('same-gym'), isTrue);
      client.setAccessToken('second-user');
      expect(controller.isLoadedForGym('same-gym'), isFalse);
      expect(controller.isEnabledForGym('same-gym'), isFalse);
      enabled = false;
      await controller.loadForGym('same-gym');
      expect(controller.isLoadedForGym('same-gym'), isTrue);
      expect(controller.isEnabledForGym('same-gym'), isFalse);
      controller.dispose();
      client.close();
    },
  );

  test(
    'old in-flight entitlement never re-enables a replacement session',
    () async {
      final first = Completer<http.Response>();
      final firstRequested = Completer<void>();
      final client = ApiClient(
        baseUrl: 'https://api.example.invalid/',
        client: MockClient((request) {
          if (request.headers['authorization'] == 'Bearer first-user') {
            firstRequested.complete();
            return first.future;
          }
          return Future.value(http.Response('{"erpEnabled":false}', 200));
        }),
      )..setAccessToken('first-user');
      final controller = ErpFeatureController(client);
      final oldLoad = controller.loadForGym('same-gym');
      await firstRequested.future;
      client.setAccessToken('second-user');
      await controller.loadForGym('same-gym');
      first.complete(http.Response('{"erpEnabled":true}', 200));
      await oldLoad;
      expect(controller.isEnabledForGym('same-gym'), isFalse);
      expect(controller.isLoadedForGym('same-gym'), isTrue);
      controller.dispose();
      client.close();
    },
  );

  for (final response in [
    http.Response('{"erpEnabled":"true"}', 200),
    http.Response('{}', 200),
    http.Response('malformed', 200),
    http.Response('failed', 500),
  ]) {
    test(
      'invalid entitlement response ${response.body} fails closed',
      () async {
        final client = ApiClient(
          baseUrl: 'https://api.example.invalid/',
          client: MockClient((_) async => response),
        );
        final controller = ErpFeatureController(client);
        await controller.loadForGym('gym');
        expect(controller.isLoadedForGym('gym'), isTrue);
        expect(controller.isEnabledForGym('gym'), isFalse);
        controller.dispose();
        client.close();
      },
    );
  }

  test(
    'refresh observes revocation and failed refresh removes cached permission',
    () async {
      var response = http.Response('{"erpEnabled":true}', 200);
      final client = ApiClient(
        baseUrl: 'https://api.example.invalid/',
        client: MockClient((_) async => response),
      );
      final controller = ErpFeatureController(client);
      await controller.loadForGym('gym');
      expect(controller.isEnabledForGym('gym'), isTrue);
      response = http.Response('failed', 500);
      await controller.loadForGym('gym', refresh: true);
      expect(controller.isEnabledForGym('gym'), isFalse);
      response = http.Response('{"erpEnabled":true}', 200);
      await controller.loadForGym('gym', refresh: true);
      expect(controller.isEnabledForGym('gym'), isTrue);
      response = http.Response('{"erpEnabled":false}', 200);
      await controller.loadForGym('gym', refresh: true);
      expect(controller.isEnabledForGym('gym'), isFalse);
      controller.dispose();
      client.close();
    },
  );

  test('reset cancels a late grant from the logged-out session', () async {
    final pending = Completer<http.Response>();
    final client = ApiClient(
      baseUrl: 'https://api.example.invalid/',
      client: MockClient((_) => pending.future),
    );
    final controller = ErpFeatureController(client);
    final loading = controller.loadForGym('gym');
    controller.reset();
    pending.complete(http.Response('{"erpEnabled":true}', 200));
    await loading;
    expect(controller.isLoadedForGym('gym'), isFalse);
    expect(controller.isEnabledForGym('gym'), isFalse);
    controller.dispose();
    client.close();
  });
}
