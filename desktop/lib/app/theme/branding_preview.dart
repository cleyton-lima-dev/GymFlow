import 'package:avelri_gestao/app/theme/ac_power_gym_branding.dart';
import 'package:avelri_gestao/app/theme/default_branding.dart';
import 'package:avelri_gestao/app/theme/gym_branding.dart';

enum BrandingPreview {
  acPowerGym,
  avelri;

  static BrandingPreview? resolve(
    String value, {
    required bool isDevelopment,
    required bool isDebug,
  }) {
    if (!isDevelopment || !isDebug) return null;

    return switch (value) {
      'ac_power_gym' => BrandingPreview.acPowerGym,
      'avelri' => BrandingPreview.avelri,
      _ => null,
    };
  }

  GymBranding get branding => switch (this) {
    BrandingPreview.acPowerGym => acPowerGymBranding,
    BrandingPreview.avelri => avelriDefaultBranding,
  };

  String get label => switch (this) {
    BrandingPreview.acPowerGym => 'AC Power Gym',
    BrandingPreview.avelri => 'Avelri neutro',
  };
}
