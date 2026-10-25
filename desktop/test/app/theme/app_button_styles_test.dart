import 'package:avelri_gestao/app/theme/app_button_styles.dart';
import 'package:avelri_gestao/app/theme/app_theme.dart';
import 'package:avelri_gestao/app/theme/default_branding.dart';
import 'package:avelri_gestao/core/widgets/app_filter_button.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('shared action buttons use consistent desktop dimensions', (
    tester,
  ) async {
    await tester.pumpWidget(
      MaterialApp(
        theme: AppTheme.fromBranding(avelriDefaultBranding),
        home: Scaffold(
          body: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              FilledButton(onPressed: () {}, child: const Text('Salvar')),
              OutlinedButton(onPressed: () {}, child: const Text('Cancelar')),
              TextButton(onPressed: () {}, child: const Text('Voltar')),
              IconButton(onPressed: () {}, icon: const Icon(Icons.refresh)),
            ],
          ),
        ),
      ),
    );

    expect(tester.getSize(find.byType(FilledButton)).height, 48);
    expect(tester.getSize(find.byType(OutlinedButton)).height, 48);
    expect(tester.getSize(find.byType(TextButton)).height, 48);
    expect(tester.getSize(find.byType(IconButton)).height, 48);
  });

  testWidgets('filter button responds to mouse and keyboard activation', (
    tester,
  ) async {
    var activations = 0;
    await tester.pumpWidget(
      MaterialApp(
        theme: AppTheme.fromBranding(avelriDefaultBranding),
        home: Scaffold(
          body: AppFilterButton(
            label: 'Ativas',
            selected: true,
            onPressed: () => activations++,
          ),
        ),
      ),
    );

    await tester.tap(find.text('Ativas'));
    expect(activations, 1);

    await tester.sendKeyEvent(LogicalKeyboardKey.tab);
    await tester.sendKeyEvent(LogicalKeyboardKey.enter);
    expect(activations, 2);
  });

  testWidgets('destructive action uses the theme error color', (tester) async {
    final theme = AppTheme.fromBranding(avelriDefaultBranding);
    await tester.pumpWidget(
      MaterialApp(
        theme: theme,
        home: Builder(
          builder: (context) => Scaffold(
            body: FilledButton(
              style: AppButtonStyles.destructive(context),
              onPressed: () {},
              child: const Text('Cancelar matrícula'),
            ),
          ),
        ),
      ),
    );

    final button = tester.widget<FilledButton>(find.byType(FilledButton));
    expect(
      button.style!.backgroundColor!.resolve(<WidgetState>{}),
      theme.colorScheme.error,
    );
    expect(tester.getSize(find.byType(FilledButton)).height, 48);
  });
}
