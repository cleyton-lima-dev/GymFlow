import 'package:avelri_gestao/features/check_ins/models/check_in_history_item.dart';
import 'package:avelri_gestao/features/check_ins/models/paged_check_ins_response.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('parses paged check-in history', () {
    final response = PagedCheckInsResponse.fromJson({
      'items': [
        {
          'id': 'check-in-1',
          'studentId': 'student-1',
          'studentName': 'Aluno Teste',
          'source': 2,
          'physicalAccessEventId': 'event-1',
          'recordedByUserId': null,
          'occurredAt': '2026-10-11T03:00:00Z',
        },
      ],
      'page': 1,
      'pageSize': 20,
      'totalCount': 1,
      'totalPages': 1,
    });

    expect(response.page, 1);
    expect(response.pageSize, 20);
    expect(response.totalCount, 1);
    expect(response.totalPages, 1);

    final item = response.items.single;

    expect(item.id, 'check-in-1');
    expect(item.studentId, 'student-1');
    expect(item.studentName, 'Aluno Teste');
    expect(item.source, CheckInSource.physicalAccess);
    expect(item.physicalAccessEventId, 'event-1');
    expect(item.recordedByUserId, isNull);
    expect(item.occurredAt, DateTime.parse('2026-10-11T03:00:00Z'));
  });
}
