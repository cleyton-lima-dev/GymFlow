import 'package:flutter_test/flutter_test.dart';
import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/core/network/api_exception.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

void main() {
  group('ApiClient', () {
    test('buildUri resolve corretamente o caminho da API', () {
      final client = ApiClient(
        baseUrl: 'https://api.example.com/',
        client: MockClient((_) async => http.Response('{}', 200)),
      );

      addTearDown(client.close);

      final uri = client.buildUri('api/students');

      expect(
        uri.toString(),
        'https://api.example.com/api/students',
      );
    });

    test('envia Bearer token em requisicao autenticada', () async {
      late http.Request capturedRequest;

      final mockClient = MockClient((request) async {
        capturedRequest = request;

        return http.Response(
          '{"success":true}',
          200,
          headers: {
            'content-type': 'application/json',
          },
        );
      });

      final client = ApiClient(
        baseUrl: 'https://api.example.com/',
        client: mockClient,
      );

      addTearDown(client.close);

      client.setAccessToken('test-token');

      final result = await client.get('api/auth/me');

      expect(
        capturedRequest.headers['Authorization'],
        'Bearer test-token',
      );

      expect(
        capturedRequest.headers['Accept'],
        'application/json',
      );

      expect(
        capturedRequest.headers['Content-Type'],
        'application/json',
      );

      expect(
        result,
        {
          'success': true,
        },
      );
    });

    test(
      'nao envia Bearer token quando authenticated for false',
          () async {
        late http.Request capturedRequest;

        final mockClient = MockClient((request) async {
          capturedRequest = request;

          return http.Response(
            '{}',
            200,
            headers: {
              'content-type': 'application/json',
            },
          );
        });

        final client = ApiClient(
          baseUrl: 'https://api.example.com/',
          client: mockClient,
        );

        addTearDown(client.close);

        client.setAccessToken('test-token');

        await client.get(
          'api/auth/public',
          authenticated: false,
        );

        expect(
          capturedRequest.headers.containsKey('Authorization'),
          isFalse,
        );
      },
    );

    test(
      '401 autenticado chama handler de sessao e lanca ApiException',
          () async {
        var unauthorizedCalls = 0;

        final client = ApiClient(
          baseUrl: 'https://api.example.com/',
          client: MockClient(
                (_) async => http.Response(
              '{"message":"Unauthorized"}',
              401,
            ),
          ),
        );

        addTearDown(client.close);

        client.setAccessToken('expired-token');

        client.setUnauthorizedHandler(() async {
          unauthorizedCalls++;
        });

        await expectLater(
          client.get('api/auth/me'),
          throwsA(
            isA<ApiException>().having(
                  (exception) => exception.statusCode,
              'statusCode',
              401,
            ),
          ),
        );

        expect(unauthorizedCalls, 1);
      },
    );

    test(
      '401 nao autenticado nao invalida sessao',
          () async {
        var unauthorizedCalls = 0;

        final client = ApiClient(
          baseUrl: 'https://api.example.com/',
          client: MockClient(
                (_) async => http.Response(
              '{"message":"Unauthorized"}',
              401,
            ),
          ),
        );

        addTearDown(client.close);

        client.setUnauthorizedHandler(() async {
          unauthorizedCalls++;
        });

        await expectLater(
          client.get(
            'api/auth/login',
            authenticated: false,
          ),
          throwsA(
            isA<ApiException>().having(
                  (exception) => exception.statusCode,
              'statusCode',
              401,
            ),
          ),
        );

        expect(unauthorizedCalls, 0);
      },
    );

    test('resposta 204 sem corpo retorna null', () async {
      final client = ApiClient(
        baseUrl: 'https://api.example.com/',
        client: MockClient(
              (_) async => http.Response('', 204),
        ),
      );

      addTearDown(client.close);

      final result = await client.get('api/test');

      expect(result, isNull);
    });
  });
}
