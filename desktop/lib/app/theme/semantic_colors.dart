import 'package:flutter/material.dart';

/// Existing Avelri status hues, centralized so badges and indicators stay
/// consistent while their text uses the active theme's foreground colors.
abstract final class AvelriSemanticColors {
  static const success = Color(0xFF2CB67D);
  static const warning = Color(0xFFF3A712);
  static const error = Color(0xFFB54752);
  static const information = Color(0xFF5374E7);
  static const inactive = Color(0xFF8A8F98);

  static const riskOnLight = Color(0xFFB42318);
  static const riskOnDark = Color(0xFFFF4D4D);

  static Color riskFor(Brightness brightness) =>
      brightness == Brightness.dark ? riskOnDark : riskOnLight;
}
