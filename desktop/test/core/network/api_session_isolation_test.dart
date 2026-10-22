import 'dart:async';

import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/core/network/api_exception.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

void main() {
  for (final status in [200, 401, 403]) {
    for (final bytes in [false, true]) {
      test(
        'stale $status ${bytes ? 'export' : 'JSON'} response cannot affect the next session',
        () async {
          final response = Completer<http.Response>();
          final requested = Completer<void>();
          var unauthorized = 0;
          var denied = 0;
          final api =
              ApiClient(
                  baseUrl: 'https://synthetic.invalid',
                  client: MockClient((_) {
                    requested.complete();
                    return response.future;
                  }),
                )
                ..setAccessToken('first-user')
                ..setUnauthorizedHandler(() async {
                  unauthorized++;
                })
                ..setErpAccessDeniedHandler(() {
                  denied++;
                });
          final request = bytes
              ? api.getBytes('api/reports/financial/export')
              : api.get('api/plans');
          final rejected = expectLater(
            request,
            throwsA(isA<SessionChangedException>()),
          );
          await requested.future;
          api.setAccessToken('second-user');
          response.complete(http.Response('{}', status));
          await rejected;
          expect(unauthorized, 0);
          expect(denied, 0);
          api.close();
        },
      );
    }
  }

  for (final path in [
    'api/plans',
    'api/enrollments/a',
    'api/charges/a/confirm-payment',
    'api/students/imports/a/confirm',
    'api/migrations',
    'api/students/a/financial-history',
    'api/students/a/reactivate',
    'api/reports/financial/export',
    'api/reports/enrollments',
    'api/dashboard',
  ]) {
    test(
      'current ERP rejection $path invalidates cached commercial access',
      () async {
        var denied = false;
        final api =
            ApiClient(
              baseUrl: 'https://synthetic.invalid',
              client: MockClient((_) async => http.Response('', 403)),
            )..setErpAccessDeniedHandler(() {
              denied = true;
            });
        await expectLater(api.get(path), throwsA(isA<ApiException>()));
        expect(denied, isTrue);
        api.close();
      },
    );
  }

  for (final path in [
    'api/students',
    'api/check-ins',
    'api/physical-access/decisions',
    'api/reports/students',
    'api/reports/attendance',
    'api/reports/access-events',
    'api/dashboard/operational',
  ]) {
    test(
      'operational rejection $path does not revoke commercial entitlement',
      () async {
        var denied = false;
        final api =
            ApiClient(
              baseUrl: 'https://synthetic.invalid',
              client: MockClient((_) async => http.Response('', 403)),
            )..setErpAccessDeniedHandler(() {
              denied = true;
            });
        await expectLater(api.get(path), throwsA(isA<ApiException>()));
        expect(denied, isFalse);
        api.close();
      },
    );
  }
}
