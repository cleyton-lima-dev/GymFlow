import 'package:avelri_gestao/features/students/models/physical_access_credential.dart';
import 'package:avelri_gestao/features/students/presentation/student_physical_access_section.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('PIN identifier is hidden and reset action is available',
      (tester) async {
    var issued = false;
    await tester.pumpWidget(MaterialApp(
      home: Scaffold(
        body: SingleChildScrollView(
          child: StudentPhysicalAccessSection(
            credentials: [
              PhysicalAccessCredential(
                id: 'synthetic-credential',
                studentId: 'synthetic-student',
                type: 5,
                providerKey: 'toletus-litenet2',
                externalIdentifier: '1234',
                isActive: true,
                createdAt: DateTime.utc(2026),
                updatedAt: null,
              ),
            ],
            accessOverride: null,
            isLoading: false,
            errorMessage: null,
            primaryColor: Colors.blue,
            onRefresh: () async {},
            onAddCredential: () {},
            onIssuePin: () => issued = true,
            onToggleCredential: (_) async {},
            onAllowAccess: () {},
            onBlockAccess: () {},
            onRemoveOverride: () {},
          ),
        ),
      ),
    ));

    expect(find.text('1234'), findsNothing);
    expect(find.textContaining('Identificador técnico: 1234'), findsNothing);
    expect(find.text('Redefinir PIN'), findsOneWidget);
    await tester.tap(find.text('Redefinir PIN'));
    expect(issued, isTrue);
  });
}
