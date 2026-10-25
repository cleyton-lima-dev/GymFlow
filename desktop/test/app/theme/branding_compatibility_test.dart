import 'package:avelri_gestao/app/app.dart';
import 'package:avelri_gestao/app/theme/ac_power_gym_branding.dart';
import 'package:avelri_gestao/app/theme/app_theme.dart';
import 'package:avelri_gestao/app/theme/brand_contrast.dart';
import 'package:avelri_gestao/app/theme/branding_controller.dart';
import 'package:avelri_gestao/app/theme/branding_preview.dart';
import 'package:avelri_gestao/app/theme/branding_repository.dart';
import 'package:avelri_gestao/app/theme/default_branding.dart';
import 'package:avelri_gestao/app/theme/gym_branding.dart';
import 'package:avelri_gestao/app/theme/local_branding_repository.dart';
import 'package:avelri_gestao/app/theme/space_branding.dart';
import 'package:avelri_gestao/core/widgets/app_status_states.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('development branding preview selection', () {
    test('accepts both presets only in development debug mode', () {
      expect(
        BrandingPreview.resolve(
          'ac_power_gym',
          isDevelopment: true,
          isDebug: true,
        ),
        BrandingPreview.acPowerGym,
      );
      expect(
        BrandingPreview.resolve('avelri', isDevelopment: true, isDebug: true),
        BrandingPreview.avelri,
      );
    });

    test('leaves the existing selection unchanged without a valid preview', () {
      for (final value in ['', 'invalid', 'AC_POWER_GYM']) {
        expect(
          BrandingPreview.resolve(value, isDevelopment: true, isDebug: true),
          isNull,
        );
      }
    });

    test('ignores preview in production and non-debug builds', () {
      expect(
        BrandingPreview.resolve(
          'ac_power_gym',
          isDevelopment: false,
          isDebug: true,
        ),
        isNull,
      );
      expect(
        BrandingPreview.resolve(
          'ac_power_gym',
          isDevelopment: true,
          isDebug: false,
        ),
        isNull,
      );
    });

    test(
      'visual presets keep repository lookup on the authenticated GymId',
      () async {
        for (final preview in BrandingPreview.values) {
          final repository = _RecordingBrandingRepository({
            'authenticated-gym': spaceBranding,
          });
          final controller = BrandingController(
            repository,
            defaultBranding: avelriDefaultBranding,
            preview: preview,
          );

          await controller.loadForGym('authenticated-gym');

          expect(repository.requestedGymIds, ['authenticated-gym']);
          expect(controller.isLoadedForGym('authenticated-gym'), isTrue);
          expect(controller.isLoadedForGym(acPowerGymBranding.id), isFalse);
          expect(controller.branding, preview.branding);
          expect(controller.previewLabel, preview.label);

          controller.reset();
          expect(controller.isLoadedForGym('authenticated-gym'), isFalse);
          expect(controller.branding, preview.branding);
        }
      },
    );

    testWidgets('preview notice names the appearance and uses theme contrast', (
      tester,
    ) async {
      for (final branding in [acPowerGymBranding, avelriDefaultBranding]) {
        final theme = AppTheme.fromBranding(branding);
        await tester.pumpWidget(
          MaterialApp(
            theme: theme,
            home: Scaffold(
              body: BrandingPreviewNotice(
                label: branding.displayName,
                backgroundColor: theme.colorScheme.surfaceContainerHighest,
                foregroundColor: theme.colorScheme.onSurface,
              ),
            ),
          ),
        );

        final notice = tester.widget<Text>(
          find.text('Pré-visualização local do tema: ${branding.displayName}'),
        );
        expect(
          BrandContrast.contrastRatio(
            theme.colorScheme.surfaceContainerHighest,
            notice.style!.color!,
          ),
          greaterThanOrEqualTo(4.5),
        );
      }
    });
  });

  test(
    'local branding selects the preset only for its configured GymId',
    () async {
      final controller = BrandingController(
        const LocalBrandingRepository({
          '11111111-1111-1111-1111-111111111111': acPowerGymBranding,
          '91f3fdb7-22b9-43ee-8a10-5e411c44cfbe': spaceBranding,
        }),
        defaultBranding: avelriDefaultBranding,
      );

      await controller.loadForGym(acPowerGymBranding.id);
      expect(controller.branding, acPowerGymBranding);
      expect(controller.isLoadedForGym(acPowerGymBranding.id), isTrue);

      await controller.loadForGym('unmapped-gym');
      expect(controller.branding, avelriDefaultBranding);
    },
  );

  test('unknown GymId uses the neutral Avelri fallback', () async {
    final controller = BrandingController(
      const LocalBrandingRepository({}),
      defaultBranding: avelriDefaultBranding,
    );

    await controller.loadForGym('unmapped-gym');

    expect(controller.branding.id, 'avelri');
    expect(
      controller.branding.primaryColor,
      avelriDefaultBranding.primaryColor,
    );
    expect(controller.branding.brightness, Brightness.light);
  });

  test('preset surfaces and text roles keep readable contrast', () {
    for (final branding in [
      avelriDefaultBranding,
      acPowerGymBranding,
      spaceBranding,
    ]) {
      final scheme = AppTheme.fromBranding(branding).colorScheme;
      final surface = branding.surfaceColor ?? branding.backgroundColor;

      expect(
        BrandContrast.contrastRatio(surface, scheme.onSurface),
        greaterThanOrEqualTo(4.5),
        reason: '${branding.displayName} surface text',
      );
      expect(
        BrandContrast.contrastRatio(branding.primaryColor, scheme.onPrimary),
        greaterThanOrEqualTo(4.5),
        reason: '${branding.displayName} primary action text',
      );
      expect(
        BrandContrast.contrastRatio(scheme.error, scheme.onError),
        greaterThanOrEqualTo(4.5),
        reason: '${branding.displayName} error foreground',
      );
    }
  });

  test(
    'explicit button foreground is honored only when it passes contrast',
    () {
      final spaceScheme = AppTheme.fromBranding(spaceBranding).colorScheme;
      expect(spaceScheme.onPrimary, spaceBranding.primaryButtonForegroundColor);

      final acScheme = AppTheme.fromBranding(acPowerGymBranding).colorScheme;
      expect(acScheme.onPrimary, Colors.black);
      expect(
        BrandContrast.contrastRatio(
          acPowerGymBranding.primaryColor,
          Colors.black,
        ),
        greaterThanOrEqualTo(4.5),
      );
    },
  );

  test(
    'shell foreground is contrast-safe across existing secondary colors',
    () {
      for (final branding in [
        avelriDefaultBranding,
        acPowerGymBranding,
        spaceBranding,
      ]) {
        final sidebarText = BrandContrast.foregroundFor(
          branding.secondaryColor,
        );
        final selectedText = BrandContrast.foregroundFor(branding.primaryColor);

        expect(
          BrandContrast.contrastRatio(branding.secondaryColor, sidebarText),
          greaterThanOrEqualTo(4.5),
          reason: '${branding.displayName} sidebar',
        );
        expect(
          BrandContrast.contrastRatio(branding.primaryColor, selectedText),
          greaterThanOrEqualTo(4.5),
          reason: '${branding.displayName} selected navigation item',
        );
      }
    },
  );

  testWidgets('shared empty and error states use accessible theme roles', (
    tester,
  ) async {
    for (final branding in [
      avelriDefaultBranding,
      acPowerGymBranding,
      spaceBranding,
    ]) {
      final theme = AppTheme.fromBranding(branding);
      await tester.pumpWidget(
        MaterialApp(
          theme: theme,
          home: const Scaffold(
            body: Column(
              children: [
                AppEmptyState(
                  icon: Icons.people_outline,
                  title: 'Lista vazia',
                  message: 'Nenhum resultado',
                ),
                AppErrorState(message: 'Falha de teste', onRetry: _noop),
              ],
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();

      final title = tester.widget<Text>(find.text('Lista vazia'));
      final message = tester.widget<Text>(find.text('Nenhum resultado'));
      final error = tester.widget<Text>(find.text('Falha de teste'));
      final errorIcon = tester.widget<Icon>(
        find.byIcon(Icons.error_outline_rounded),
      );
      expect(title.style?.color, theme.colorScheme.onSurface);
      expect(message.style?.color, theme.colorScheme.onSurfaceVariant);
      expect(error.style?.color, theme.colorScheme.onSurface);
      expect(errorIcon.color, theme.colorScheme.error);
      expect(
        BrandContrast.contrastRatio(
          theme.colorScheme.surface,
          title.style!.color!,
        ),
        greaterThanOrEqualTo(4.5),
      );
      expect(
        BrandContrast.contrastRatio(
          theme.colorScheme.surface,
          errorIcon.color!,
        ),
        greaterThanOrEqualTo(3),
      );
    }
  });
}

class _RecordingBrandingRepository implements BrandingRepository {
  _RecordingBrandingRepository(this.brandings);

  final Map<String, GymBranding> brandings;
  final List<String> requestedGymIds = [];

  @override
  Future<GymBranding?> getByGymId(String gymId) async {
    requestedGymIds.add(gymId);
    return brandings[gymId];
  }
}

void _noop() {}
