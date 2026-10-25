import 'dart:convert';

import 'package:avelri_gestao/app/theme/branding_controller.dart';
import 'package:avelri_gestao/app/theme/ac_power_gym_branding.dart';
import 'package:avelri_gestao/app/theme/app_theme.dart';
import 'package:avelri_gestao/app/theme/default_branding.dart';
import 'package:avelri_gestao/app/theme/gym_branding.dart';
import 'package:avelri_gestao/app/theme/local_branding_repository.dart';
import 'package:avelri_gestao/app/theme/space_branding.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/charges/presentation/charges_page.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:provider/provider.dart';

void main() {
  testWidgets(
    'Financeiro fits desktop widths and switches to cards on narrow windows',
    (tester) async {
      final api = ApiClient(
        baseUrl: 'https://synthetic.invalid',
        client: MockClient(
          (_) async => http.Response(jsonEncode([_charge]), 200),
        ),
      );
      addTearDown(api.close);
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      final sizes = [
        const Size(1280, 720),
        const Size(1366, 768),
        const Size(1920, 1080),
        const Size(900, 650),
      ];

      for (final branding in [
        avelriDefaultBranding,
        acPowerGymBranding,
        spaceBranding,
      ]) {
        for (final size in sizes) {
          tester.view.physicalSize = size;
          tester.view.devicePixelRatio = 1;
          await tester.pumpWidget(_financeHarness(api, branding));
          await tester.pumpAndSettle();

          final usesCards = size.width == 900;
          expect(
            find.byKey(
              ValueKey(
                usesCards ? 'charges-compact-list' : 'charges-responsive-table',
              ),
            ),
            findsOneWidget,
            reason: '$size',
          );
          if (usesCards) {
            expect(
              find.byKey(const ValueKey('charge-card-synthetic-charge')),
              findsOneWidget,
              reason: '$size',
            );
          } else {
            expect(find.text('Aluno'), findsOneWidget, reason: '$size');
            expect(find.text('Plano'), findsOneWidget, reason: '$size');
            expect(find.text('Vencimento'), findsOneWidget, reason: '$size');
            expect(find.text('Ações'), findsOneWidget, reason: '$size');
          }
          expect(
            tester.takeException(),
            isNull,
            reason: '${branding.displayName} $size',
          );

          final actionButton = find.byTooltip('Ações');
          final actionRect = tester.getRect(actionButton);
          expect(actionRect.left, greaterThanOrEqualTo(240), reason: '$size');
          expect(
            actionRect.right,
            lessThanOrEqualTo(size.width),
            reason: '$size',
          );

          await tester.tap(actionButton);
          await tester.pumpAndSettle();
          expect(find.text('Ver detalhes'), findsOneWidget, reason: '$size');
          expect(
            find.text('Confirmar pagamento'),
            findsOneWidget,
            reason: '$size',
          );
          expect(tester.takeException(), isNull, reason: '$size');

          await tester.pumpWidget(const SizedBox.shrink());
        }
      }
    },
  );
}

Widget _financeHarness(ApiClient api, GymBranding branding) => MultiProvider(
  providers: [
    Provider<ApiClient>.value(value: api),
    ChangeNotifierProvider(
      create: (_) => BrandingController(
        const LocalBrandingRepository({}),
        defaultBranding: branding,
      ),
    ),
  ],
  child: MaterialApp(
    theme: AppTheme.fromBranding(branding),
    home: Scaffold(
      body: Row(
        children: [
          SizedBox(width: 240),
          Expanded(child: ChargesPage()),
        ],
      ),
    ),
  ),
);

final _charge = {
  'id': 'synthetic-charge',
  'enrollmentId': 'synthetic-enrollment',
  'studentId': 'synthetic-student',
  'studentName': 'Synthetic Student With a Long Name to Test Truncation',
  'planName': 'Synthetic Monthly Plan With a Long Description',
  'amount': 159.90,
  'discountAmount': 0,
  'paidAmount': null,
  'paymentMethod': null,
  'dueDate': '2026-10-30T00:00:00',
  'status': 1,
  'paidAt': null,
  'createdAt': '2026-10-01T12:00:00Z',
  'updatedAt': null,
};
