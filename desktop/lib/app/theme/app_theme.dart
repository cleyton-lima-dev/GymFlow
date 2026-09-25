import 'package:flutter/material.dart';

import 'package:avelri_gestao/app/theme/gym_branding.dart';

class AppTheme {
  const AppTheme._();

  static ThemeData fromBranding(GymBranding branding) {
    final colorScheme = ColorScheme.fromSeed(
      seedColor: branding.primaryColor,
      brightness: branding.brightness,
    ).copyWith(
      primary: branding.primaryColor,
      secondary: branding.secondaryColor,
      surface: branding.surfaceColor ?? branding.backgroundColor,
    );

    return ThemeData(
      useMaterial3: true,
      brightness: branding.brightness,
      colorScheme: colorScheme,
      scaffoldBackgroundColor: branding.backgroundColor,
      dividerColor: colorScheme.outlineVariant,
      cardTheme: CardThemeData(
        color: colorScheme.surface,
        elevation: 0,
        margin: EdgeInsets.zero,
      ),
    );
  }
}