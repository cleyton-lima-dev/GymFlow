import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/features/students/data/students_service.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

void main() {
  test('createStudent sends nullable administrative fields without password', () async {
    late http.Request request;
    final client = ApiClient(
      baseUrl: 'https://api.example.test/',
      client: MockClient((captured) async {
        request = captured;
        return http.Response('{}', 201,
            headers: {'content-type': 'application/json'});
      }),
    );
    addTearDown(client.close);

    await StudentsService(client).createStudent(name: 'Aluno sem conta');

    final body = jsonDecode(request.body) as Map<String, dynamic>;
    expect(body['name'], 'Aluno sem conta');
    expect(body['email'], isNull);
    expect(body.containsKey('password'), isFalse);
    expect(body['phone'], isNull);
    expect(body['birthDate'], isNull);
  });

  test('updateStudent serializes an empty email as null', () async {
    late http.Request request;
    final client = ApiClient(
      baseUrl: 'https://api.example.test/',
      client: MockClient((captured) async {
        request = captured;
        return http.Response('', 204);
      }),
    );
    addTearDown(client.close);

    await StudentsService(client).updateStudent(
      studentId: 'student-id',
      name: 'Aluno sem e-mail',
      email: '  ',
    );

    final body = jsonDecode(request.body) as Map<String, dynamic>;
    expect(body['email'], isNull);
  });
}
