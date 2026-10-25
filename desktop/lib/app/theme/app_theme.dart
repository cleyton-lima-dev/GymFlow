import 'package:flutter/material.dart';

import 'package:avelri_gestao/app/theme/gym_branding.dart';
import 'package:avelri_gestao/app/theme/brand_contrast.dart';

class AppTheme {
  const AppTheme._();

  static ThemeData fromBranding(GymBranding branding) {
    final colorScheme = ColorScheme.fromSeed(
      seedColor: branding.primaryColor,
      brightness: branding.brightness,
    );
    final themedColorScheme = colorScheme.copyWith(
      primary: branding.primaryColor,
      secondary: branding.secondaryColor,
      surface: branding.surfaceColor ?? branding.backgroundColor,
      onPrimary: BrandContrast.foregroundFor(
        branding.primaryColor,
        preferred: branding.primaryButtonForegroundColor,
      ),
    );

    const actionRadius = BorderRadius.all(Radius.circular(10));
    const actionTextStyle = TextStyle(
      fontSize: 13,
      fontWeight: FontWeight.w700,
    );

    return ThemeData(
      useMaterial3: true,
      brightness: branding.brightness,
      colorScheme: themedColorScheme,
      scaffoldBackgroundColor: branding.backgroundColor,
      dividerColor: themedColorScheme.outlineVariant,
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          minimumSize: const Size(0, 48),
          padding: const EdgeInsets.symmetric(horizontal: 16),
          shape: const RoundedRectangleBorder(borderRadius: actionRadius),
          textStyle: actionTextStyle,
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          minimumSize: const Size(0, 48),
          padding: const EdgeInsets.symmetric(horizontal: 16),
          shape: const RoundedRectangleBorder(borderRadius: actionRadius),
          textStyle: actionTextStyle,
          side: BorderSide(color: themedColorScheme.outlineVariant),
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(
          minimumSize: const Size(0, 48),
          padding: const EdgeInsets.symmetric(horizontal: 12),
          shape: const RoundedRectangleBorder(borderRadius: actionRadius),
          textStyle: actionTextStyle,
        ),
      ),
      iconButtonTheme: IconButtonThemeData(
        style: IconButton.styleFrom(
          minimumSize: const Size(48, 48),
          padding: const EdgeInsets.all(8),
          shape: const RoundedRectangleBorder(borderRadius: actionRadius),
        ),
      ),
      cardTheme: CardThemeData(
        color: themedColorScheme.surface,
        elevation: 0,
        margin: EdgeInsets.zero,
      ),
    );
  }
}
