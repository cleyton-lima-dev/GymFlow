import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/features/students/data/students_service.dart';
import 'package:gymflow/features/students/presentation/student_details_view_model.dart';
import 'package:gymflow/features/students/presentation/student_invite_share_links.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

void main() {
  group('student app access', () {
    test('posts the official endpoint and returns the invite token', () async {
      late http.Request postRequest;
      final apiClient = ApiClient(
        baseUrl: 'http://127.0.0.1:5137/',
        client: MockClient((request) async {
          if (request.method == 'GET') {
            return http.Response(jsonEncode(_studentJson()), 200);
          }
          postRequest = request;
          return http.Response(
            jsonEncode({
              'enabled': true,
              'token': 'synthetic-invite-token',
              'expiresAt': '2030-01-02T03:04:05Z',
            }),
            200,
          );
        }),
      );
      addTearDown(apiClient.close);

      final viewModel = StudentDetailsViewModel(
        StudentsService(apiClient),
        'student-130-a',
      );
      addTearDown(viewModel.dispose);
      await viewModel.load();

      final result = await viewModel.setAppAccess(
        enabled: true,
        email: '  Aluno@Example.Test  ',
      );

      expect(result.succeeded, isTrue);
      expect(result.token, 'synthetic-invite-token');
      expect(result.expiresAt, DateTime.utc(2030, 1, 2, 3, 4, 5));
      expect(postRequest.url.path, '/api/students/student-130-a/app-access');
      expect(postRequest.method, 'POST');
      expect(jsonDecode(postRequest.body), {
        'enabled': true,
        'email': 'aluno@example.test',
      });
      expect(viewModel.student?.appAccessEnabled, isTrue);
      expect(viewModel.student?.email, 'aluno@example.test');
      expect(viewModel.student?.userId, isNull);
    });

    test(
      'surfaces duplicate identity as a conflict without issuing invite',
      () async {
        final apiClient = ApiClient(
          baseUrl: 'http://127.0.0.1:5137/',
          client: MockClient((request) async {
            if (request.method == 'GET') {
              return http.Response(jsonEncode(_studentJson()), 200);
            }
            return http.Response(
              jsonEncode({'title': 'Email already used'}),
              409,
            );
          }),
        );
        addTearDown(apiClient.close);

        final viewModel = StudentDetailsViewModel(
          StudentsService(apiClient),
          'student-130-a',
        );
        addTearDown(viewModel.dispose);
        await viewModel.load();

        final result = await viewModel.setAppAccess(
          enabled: true,
          email: 'other@example.test',
        );

        expect(result.succeeded, isFalse);
        expect(result.token, isNull);
        expect(
          viewModel.appAccessErrorMessage,
          'Já existe uma conta usando este e-mail.',
        );
        expect(viewModel.student?.appAccessEnabled, isFalse);
      },
    );

    test('does not replace an existing linked account identity', () async {
      var appAccessEnabled = false;
      final apiClient = ApiClient(
        baseUrl: 'http://127.0.0.1:5137/',
        client: MockClient((request) async {
          if (request.method == 'GET') {
            return http.Response(
              jsonEncode(
                _studentJson(
                  userId: 'existing-user-id',
                  appAccessEnabled: appAccessEnabled,
                ),
              ),
              200,
            );
          }
          appAccessEnabled = true;
          return http.Response(
            jsonEncode({'enabled': true, 'token': null, 'expiresAt': null}),
            200,
          );
        }),
      );
      addTearDown(apiClient.close);

      final viewModel = StudentDetailsViewModel(
        StudentsService(apiClient),
        'student-130-a',
      );
      addTearDown(viewModel.dispose);
      await viewModel.load();

      final result = await viewModel.setAppAccess(enabled: true);

      expect(result.succeeded, isTrue);
      expect(result.token, isNull);
      expect(viewModel.student?.userId, 'existing-user-id');
      expect(viewModel.student?.appAccessEnabled, isTrue);
    });
  });

  test(
    'copied invite uses the student activation route and safe instructions',
    () {
      const baseUrl = 'https://student.example.test';
      const token = 'synthetic-token';
      final link = StudentInviteShareLinks.inviteLink(
        baseUrl: baseUrl,
        token: token,
      );
      final inviteMessage = StudentInviteShareLinks.message(
        email: 'aluno@example.test',
        inviteLink: link,
      );

      expect(link, 'https://student.example.test/ativar?token=synthetic-token');
      expect(inviteMessage, contains(link));
      expect(inviteMessage, contains('defina sua própria senha'));
      expect(inviteMessage, contains('aluno@example.test'));
      expect(inviteMessage, contains('24 horas'));
      expect(
        StudentInviteShareLinks.email(
          email: 'aluno@example.test',
          inviteLink: link,
        ).queryParameters['body'],
        contains(link),
      );
      expect(
        StudentInviteShareLinks.whatsapp(
          email: 'aluno@example.test',
          inviteLink: link,
        ).queryParameters['text'],
        contains(link),
      );
    },
  );

  test('validates the email required for a new app-access invite', () {
    expect(
      StudentInviteShareLinks.isValidEmail(' aluno@example.test '),
      isTrue,
    );
    expect(StudentInviteShareLinks.isValidEmail(null), isFalse);
    expect(StudentInviteShareLinks.isValidEmail('not-an-email'), isFalse);
    expect(StudentInviteShareLinks.isValidEmail('student@invalid'), isFalse);
  });
}

Map<String, dynamic> _studentJson({
  String? userId,
  bool appAccessEnabled = false,
}) => {
  'id': 'student-130-a',
  'name': 'Aluno Sintético A',
  'email': null,
  'phone': null,
  'birthDate': null,
  'isActive': true,
  'archivedAt': null,
  'appAccessEnabled': appAccessEnabled,
  'userId': userId,
  'createdAt': '2026-11-12T00:00:00Z',
};
