import 'package:flutter/material.dart';

import 'package:avelri_gestao/app/theme/gym_branding.dart';

class AppTheme {
  const AppTheme._();

  static ThemeData fromBranding(GymBranding branding) {
    final colorScheme = ColorScheme.fromSeed(
      seedColor: branding.primaryColor,
      brightness: Brightness.light,
    ).copyWith(
      primary: branding.primaryColor,
      secondary: branding.secondaryColor,
      surface: Colors.white,
    );

    return ThemeData(
      useMaterial3: true,
      brightness: Brightness.light,
      colorScheme: colorScheme,
      scaffoldBackgroundColor: const Color(0xFFF5F6FA),
      dividerColor: const Color(0xFFE8EAF1),
      cardTheme: const CardThemeData(
        color: Colors.white,
        elevation: 0,
        margin: EdgeInsets.zero,
      ),
    );
  }
}