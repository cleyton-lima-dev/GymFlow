import 'dart:convert';

import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/charges/data/charges_service.dart';
import 'package:avelri_gestao/features/enrollments/data/enrollments_service.dart';
import 'package:avelri_gestao/features/students/data/students_service.dart';
import 'package:avelri_gestao/features/students/presentation/students_view_model.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

void main() {
  test('dashboard period is sent to the paid charges endpoint', () async {
    final api = ApiClient(
      baseUrl: 'https://synthetic.invalid',
      client: MockClient((request) async {
        expect(request.url.path, '/api/charges');
        expect(request.url.queryParameters, {'paidMonth': '2026-10-01'});
        return http.Response('[]', 200);
      }),
    );

    await ChargesService(api).getCharges(paidMonth: DateTime(2026, 10, 1));
    api.close();
  });

  test('expiring enrollment filter is sent to the server list', () async {
    final api = ApiClient(
      baseUrl: 'https://synthetic.invalid',
      client: MockClient((request) async {
        expect(request.url.path, '/api/enrollments');
        expect(request.url.queryParameters, {'expiringWithinDays': '15'});
        return http.Response('[]', 200);
      }),
    );

    await EnrollmentsService(api).getEnrollments(expiringWithinDays: 15);
    api.close();
  });

  test('assessment filter retains server pagination parameters', () async {
    final api = ApiClient(
      baseUrl: 'https://synthetic.invalid',
      client: MockClient((request) async {
        expect(request.url.path, '/api/students');
        expect(request.url.queryParameters, {
          'page': '3',
          'pageSize': '20',
          'isActive': 'true',
          'archiveFilter': '1',
          'assessmentFilter': '2',
        });
        return http.Response(
          jsonEncode({
            'items': <Object>[],
            'page': 3,
            'pageSize': 20,
            'totalCount': 0,
            'totalPages': 0,
          }),
          200,
        );
      }),
    );

    await StudentsService(api).getStudents(
      isActive: true,
      archiveFilter: 1,
      assessmentFilter: '2',
      page: 3,
      pageSize: 20,
    );
    api.close();
  });

  test('student dashboard filter is visible, applied, and removable', () async {
    final queries = <Map<String, String>>[];
    final api = ApiClient(
      baseUrl: 'https://synthetic.invalid',
      client: MockClient((request) async {
        queries.add(request.url.queryParameters);
        return http.Response(
          jsonEncode({
            'items': <Object>[],
            'page': 1,
            'pageSize': 20,
            'totalCount': 0,
            'totalPages': 0,
          }),
          200,
        );
      }),
    );
    final model = StudentsViewModel(
      StudentsService(api),
      assessmentFilter: StudentsAssessmentFilter.reassessmentDue,
    );

    expect(model.assessmentFilterLabel, contains('alunos ativos'));
    await model.loadInitial();
    expect(queries.single['assessmentFilter'], '2');
    expect(queries.single['isActive'], 'true');
    await model.clearAssessmentFilter();
    expect(queries.last.containsKey('assessmentFilter'), isFalse);
    expect(queries.last.containsKey('isActive'), isFalse);

    model.dispose();
    api.close();
  });
}
