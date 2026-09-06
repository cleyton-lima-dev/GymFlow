import 'package:flutter/material.dart';

class GymBranding {
  const GymBranding({
    required this.id,
    required this.displayName,
    required this.primaryColor,
    required this.secondaryColor,
    required this.backgroundColor,
    required this.brightness,
    this.surfaceColor,
    this.logoAsset,
    this.primaryButtonForegroundColor,
  });

  final String id;
  final String displayName;
  final Color primaryColor;
  final Color secondaryColor;
  final Color backgroundColor;
  final Brightness brightness;
  final Color? surfaceColor;
  final String? logoAsset;
  final Color? primaryButtonForegroundColor;
}
