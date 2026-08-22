import 'package:flutter/foundation.dart';
import 'package:gymflow/app/theme/branding_repository.dart';
import 'package:gymflow/app/theme/gym_branding.dart';

class BrandingController extends ChangeNotifier {
  BrandingController(
      this._repository, {
        required GymBranding defaultBranding,
      })  : _defaultBranding = defaultBranding,
        _branding = defaultBranding;

  final BrandingRepository _repository;
  final GymBranding _defaultBranding;

  GymBranding _branding;
  String? _loadedGymId;

  GymBranding get branding => _branding;
  bool isLoadedForGym(String gymId) {
    return _loadedGymId == gymId;
  }

  Future<void> loadForGym(String gymId) async {
    if (_loadedGymId == gymId) {
      return;
    }
    final branding = await _repository.getByGymId(gymId);

    _branding = branding ?? _defaultBranding;
    _loadedGymId = gymId;

    notifyListeners();
  }

  void reset() {
    _loadedGymId = null;
    _branding = _defaultBranding;
    notifyListeners();
  }
}
