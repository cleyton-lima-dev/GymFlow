import 'package:flutter/material.dart';
import 'package:gymflow/app/theme/gym_branding.dart';

class AppTheme {
  const AppTheme._();

  static ThemeData fromBranding(GymBranding branding) {
    final colorScheme = ColorScheme.fromSeed(
      seedColor: branding.primaryColor,
      brightness: branding.brightness,
    ).copyWith(
        primary: branding.primaryColor,
        secondary: branding.secondaryColor,
        surface: branding.backgroundColor,
    );

    return ThemeData(
      useMaterial3: true,
      colorScheme: colorScheme,
      scaffoldBackgroundColor: branding.backgroundColor,
    );
  }
}
