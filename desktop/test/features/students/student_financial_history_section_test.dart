import 'package:avelri_gestao/features/students/models/student_financial_history_item.dart';
import 'package:avelri_gestao/features/students/presentation/student_financial_history_section.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('distingue cobranças não verificadas e recebimentos históricos',
      (tester) async {
    StudentFinancialHistoryItem item(String id, int status, int verification,
        int paymentOrigin) =>
        StudentFinancialHistoryItem.fromJson({
          'enrollmentId': id,
          'planName': id,
          'planPrice': 100,
          'startDate': '2026-10-01',
          'endDate': '2026-11-01',
          'enrollmentStatus': 3,
          'chargeId': id,
          'chargeAmount': 100,
          'dueDate': '2026-10-01',
          'chargeStatus': status,
          'verificationStatus': verification,
          'paymentOrigin': paymentOrigin,
          'createdAt': '2026-10-01T12:00:00Z',
        });

    await tester.pumpWidget(MaterialApp(
      home: Scaffold(
        body: SingleChildScrollView(
          child: StudentFinancialHistorySection(
            history: [
              item('Histórico sem verificação', 1, 1, 2),
              item('Histórico quitado', 2, 0, 1),
              item('Vencido verificado', 3, 0, 0),
            ],
            isLoading: false,
            errorMessage: null,
            onRetry: () async {},
            primaryColor: Colors.blue,
          ),
        ),
      ),
    ));

    expect(find.text('Financeiro não verificado'), findsOneWidget);
    expect(find.text('Recebimento histórico'), findsOneWidget);
    expect(find.text('Em atraso'), findsOneWidget);
    expect(find.text('Pago'), findsNothing);
  });
}
