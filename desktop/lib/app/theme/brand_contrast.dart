import 'package:flutter/material.dart';

class BrandContrast {
  const BrandContrast._();

  static Color foregroundFor(Color background, {Color? preferred}) {
    if (preferred != null && contrastRatio(background, preferred) >= 4.5) {
      return preferred;
    }

    const light = Colors.white;
    const dark = Colors.black;
    return contrastRatio(background, dark) >= contrastRatio(background, light)
        ? dark
        : light;
  }

  static double contrastRatio(Color first, Color second) {
    final firstLuminance = first.computeLuminance();
    final secondLuminance = second.computeLuminance();
    final lighter = firstLuminance > secondLuminance
        ? firstLuminance
        : secondLuminance;
    final darker = firstLuminance > secondLuminance
        ? secondLuminance
        : firstLuminance;
    return (lighter + 0.05) / (darker + 0.05);
  }
}
