import 'package:avelri_gestao/app/theme/ac_power_gym_branding.dart';
import 'package:avelri_gestao/app/theme/branding_repository.dart';
import 'package:avelri_gestao/app/theme/gym_branding.dart';
import 'package:avelri_gestao/app/theme/space_branding.dart';

class LocalBrandingRepository implements BrandingRepository {
  const LocalBrandingRepository(this._brandings);

  factory LocalBrandingRepository.forApplication({
    required bool allowHomologationBinding,
    String? acPowerHomologationGymId,
  }) {
    final brandings = <String, GymBranding>{
      '11111111-1111-1111-1111-111111111111': acPowerGymBranding,
      '91f3fdb7-22b9-43ee-8a10-5e411c44cfbe': spaceBranding,
    };

    final gymId = acPowerHomologationGymId?.trim().toLowerCase();
    if (allowHomologationBinding && gymId != null && gymId.isNotEmpty) {
      final isUuid = RegExp(
        r'^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$',
      ).hasMatch(gymId);
      if (!isUuid || brandings.containsKey(gymId)) {
        throw ArgumentError.value(
          acPowerHomologationGymId,
          'acPowerHomologationGymId',
          'Expected a new, valid synthetic GymId.',
        );
      }
      brandings[gymId] = acPowerGymBranding;
    }

    return LocalBrandingRepository(Map.unmodifiable(brandings));
  }

  final Map<String, GymBranding> _brandings;

  @override
  Future<GymBranding?> getByGymId(String gymId) async {
    return _brandings[gymId];
  }
}
