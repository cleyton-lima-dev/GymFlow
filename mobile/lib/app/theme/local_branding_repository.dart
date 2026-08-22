import 'package:gymflow/app/theme/branding_repository.dart';
import 'package:gymflow/app/theme/gym_branding.dart';

class LocalBrandingRepository implements BrandingRepository {
  const LocalBrandingRepository(this._brandings);

  final Map<String, GymBranding> _brandings;

  @override
  Future<GymBranding?> getByGymId(String gymId) async {
    return _brandings[gymId];
  }
}
