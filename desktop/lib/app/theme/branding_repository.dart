import 'package:avelri_gestao/app/theme/gym_branding.dart';

abstract interface class BrandingRepository {
  Future<GymBranding?> getByGymId(String gymId);
}
