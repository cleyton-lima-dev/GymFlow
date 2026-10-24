import 'dart:convert';

import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/check_ins/data/check_ins_service.dart';
import 'package:avelri_gestao/features/check_ins/data/physical_access_events_service.dart';
import 'package:avelri_gestao/features/check_ins/presentation/check_ins_view_model.dart';
import 'package:avelri_gestao/features/check_ins/presentation/physical_access_events_view_model.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

void main() {
  test(
    'check-in history navigates all pages and resets pagination for filters',
    () async {
      final requests = <Uri>[];
      final client = MockClient((request) async {
        requests.add(request.url);
        final page = int.parse(request.url.queryParameters['page']!);
        return _jsonResponse(_checkInPage(page));
      });
      final apiClient = ApiClient(
        baseUrl: 'http://localhost:5137/',
        client: client,
      );
      final viewModel = CheckInsViewModel(CheckInsService(apiClient));
      final visitedIds = <String>[];

      await viewModel.loadInitial();
      expect(viewModel.items, hasLength(20));
      expect(viewModel.page, 1);
      expect(viewModel.totalPages, 3);
      expect(viewModel.totalCount, 45);
      expect(viewModel.hasPreviousPage, isFalse);
      expect(viewModel.hasNextPage, isTrue);
      visitedIds.addAll(viewModel.items.map((item) => item.id));

      await viewModel.goToNextPage();
      expect(viewModel.page, 2);
      visitedIds.addAll(viewModel.items.map((item) => item.id));
      await viewModel.goToNextPage();
      expect(viewModel.page, 3);
      expect(viewModel.items, hasLength(5));
      expect(viewModel.hasNextPage, isFalse);
      visitedIds.addAll(viewModel.items.map((item) => item.id));
      expect(visitedIds, hasLength(45));
      expect(visitedIds.toSet(), hasLength(45));

      await viewModel.goToPreviousPage();
      expect(viewModel.page, 2);
      await viewModel.updateStudentFilter(
        studentId: 'student-1',
        studentName: 'Aluno 1',
      );
      expect(viewModel.page, 1);
      expect(requests.last.queryParameters['studentId'], 'student-1');
      expect(requests.last.queryParameters['page'], '1');

      await viewModel.updatePeriod(
        fromDate: DateTime(2026, 10, 2),
        toDate: DateTime(2026, 10, 3),
      );
      expect(viewModel.page, 1);
      expect(requests.last.queryParameters['fromDate'], '2026-10-02');
      expect(requests.last.queryParameters['toDate'], '2026-10-03');
      expect(requests.last.queryParameters['studentId'], 'student-1');
      expect(viewModel.isLoading, isFalse);
      expect(viewModel.errorMessage, isNull);

      viewModel.dispose();
      apiClient.close();
    },
  );

  test('physical events navigate pages and apply all event filters', () async {
    final requests = <Uri>[];
    final client = MockClient((request) async {
      requests.add(request.url);
      final page = int.parse(request.url.queryParameters['page']!);
      return _jsonResponse(_eventPage(page));
    });
    final apiClient = ApiClient(
      baseUrl: 'http://localhost:5137/',
      client: client,
    );
    final viewModel = PhysicalAccessEventsViewModel(
      PhysicalAccessEventsService(apiClient),
    );
    final visitedIds = <String>[];

    await viewModel.loadInitial();
    visitedIds.addAll(viewModel.items.map((item) => item.id));
    await viewModel.goToNextPage();
    expect(viewModel.page, 2);
    visitedIds.addAll(viewModel.items.map((item) => item.id));
    await viewModel.goToNextPage();
    expect(viewModel.page, 3);
    expect(viewModel.items, hasLength(5));
    expect(viewModel.totalCount, 45);
    expect(viewModel.totalPages, 3);
    expect(viewModel.hasNextPage, isFalse);
    visitedIds.addAll(viewModel.items.map((item) => item.id));
    expect(visitedIds, hasLength(45));
    expect(visitedIds.toSet(), hasLength(45));

    await viewModel.updateStudentFilter(
      studentId: 'student-2',
      studentName: 'Aluno 2',
    );
    await viewModel.updateDecision(2);
    await viewModel.updateSource(3);
    await viewModel.updatePeriod(
      fromDate: DateTime(2026, 10, 2),
      toDate: DateTime(2026, 10, 3),
    );

    expect(viewModel.page, 1);
    expect(requests.last.queryParameters['studentId'], 'student-2');
    expect(requests.last.queryParameters['decision'], '2');
    expect(requests.last.queryParameters['source'], '3');
    expect(requests.last.queryParameters['fromDate'], '2026-10-02');
    expect(requests.last.queryParameters['toDate'], '2026-10-03');
    expect(requests.last.queryParameters['page'], '1');
    expect(viewModel.isLoading, isFalse);
    expect(viewModel.errorMessage, isNull);

    viewModel.dispose();
    apiClient.close();
  });

  test(
    'empty response, network error, loading state and retry remain usable',
    () async {
      var checkInCall = 0;
      final checkInClient = MockClient((_) async {
        checkInCall++;
        if (checkInCall == 1) throw http.ClientException('offline');
        return _jsonResponse(_checkInPage(1, totalCount: 0));
      });
      final checkInApi = ApiClient(
        baseUrl: 'http://localhost:5137/',
        client: checkInClient,
      );
      final checkInViewModel = CheckInsViewModel(CheckInsService(checkInApi));

      final checkInLoad = checkInViewModel.loadInitial();
      expect(checkInViewModel.isLoading, isTrue);
      await checkInLoad;
      expect(checkInViewModel.errorMessage, contains('conectar'));
      expect(checkInViewModel.isLoading, isFalse);
      await checkInViewModel.retry();
      expect(checkInViewModel.items, isEmpty);
      expect(checkInViewModel.totalCount, 0);
      expect(checkInViewModel.totalPages, 1);
      expect(checkInViewModel.hasNextPage, isFalse);
      expect(checkInViewModel.errorMessage, isNull);
      checkInViewModel.dispose();
      checkInApi.close();

      var eventCall = 0;
      final eventClient = MockClient((_) async {
        eventCall++;
        if (eventCall == 1) throw http.ClientException('offline');
        return _jsonResponse(_eventPage(1, totalCount: 0));
      });
      final eventApi = ApiClient(
        baseUrl: 'http://localhost:5137/',
        client: eventClient,
      );
      final eventViewModel = PhysicalAccessEventsViewModel(
        PhysicalAccessEventsService(eventApi),
      );

      final eventLoad = eventViewModel.loadInitial();
      expect(eventViewModel.isLoading, isTrue);
      await eventLoad;
      expect(eventViewModel.errorMessage, contains('conectar'));
      expect(eventViewModel.isLoading, isFalse);
      await eventViewModel.retry();
      expect(eventViewModel.items, isEmpty);
      expect(eventViewModel.totalCount, 0);
      expect(eventViewModel.totalPages, 1);
      expect(eventViewModel.hasNextPage, isFalse);
      expect(eventViewModel.errorMessage, isNull);
      eventViewModel.dispose();
      eventApi.close();
    },
  );
}

http.Response _jsonResponse(Map<String, dynamic> body) => http.Response(
  jsonEncode(body),
  200,
  headers: {'content-type': 'application/json'},
);

Map<String, dynamic> _checkInPage(int page, {int totalCount = 45}) {
  final start = (page - 1) * 20;
  final count = (totalCount - start).clamp(0, 20).toInt();
  return {
    'items': List.generate(count, (offset) {
      final index = start + offset;
      return {
        'id': 'check-in-$index',
        'studentId': 'student-${index % 2 + 1}',
        'studentName': 'Aluno ${index % 2 + 1}',
        'source': 1,
        'physicalAccessEventId': null,
        'recordedByUserId': null,
        'occurredAt': DateTime.utc(
          2026,
          10,
          1,
        ).add(Duration(minutes: index)).toIso8601String(),
      };
    }),
    'page': page,
    'pageSize': 20,
    'totalCount': totalCount,
    'totalPages': (totalCount / 20).ceil(),
  };
}

Map<String, dynamic> _eventPage(int page, {int totalCount = 45}) {
  final start = (page - 1) * 20;
  final count = (totalCount - start).clamp(0, 20).toInt();
  return {
    'items': List.generate(count, (offset) {
      final index = start + offset;
      final occurredAt = DateTime.utc(
        2026,
        10,
        1,
      ).add(Duration(minutes: index));
      return {
        'id': 'event-$index',
        'requestId': 'request-$index',
        'studentId': 'student-${index % 2 + 1}',
        'studentName': 'Aluno ${index % 2 + 1}',
        'decision': index % 2 + 1,
        'reason': index % 2 == 0 ? 1 : 100,
        'source': index % 3 + 1,
        'occurredAt': occurredAt.toIso8601String(),
        'processedAt': occurredAt
            .add(const Duration(seconds: 1))
            .toIso8601String(),
      };
    }),
    'page': page,
    'pageSize': 20,
    'totalCount': totalCount,
    'totalPages': (totalCount / 20).ceil(),
  };
}
