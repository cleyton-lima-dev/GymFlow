import 'package:flutter_test/flutter_test.dart';
import 'package:gymflow/features/students/models/student_summary.dart';

void main() {
  test('StudentSummary accepts an administrative student without email', () {
    final student = StudentSummary.fromJson({
      'id': 'student-id',
      'name': 'Aluno sem conta',
      'email': null,
      'phone': null,
      'birthDate': null,
      'isActive': true,
      'createdAt': '2026-10-19T12:00:00Z',
    });

    expect(student.email, isNull);
    expect(student.name, 'Aluno sem conta');
  });

  test('StudentSummary continues to read email for existing accounts', () {
    final student = StudentSummary.fromJson({
      'id': 'student-id',
      'name': 'Aluno com conta',
      'email': 'aluno@example.test',
      'phone': null,
      'birthDate': null,
      'isActive': true,
      'createdAt': '2026-10-19T12:00:00Z',
    });

    expect(student.email, 'aluno@example.test');
  });
}
