import 'package:flutter/foundation.dart';
import 'package:avelri_gestao/app/theme/branding_repository.dart';
import 'package:avelri_gestao/app/theme/branding_preview.dart';
import 'package:avelri_gestao/app/theme/gym_branding.dart';

class BrandingController extends ChangeNotifier {
  BrandingController(
    this._repository, {
    required GymBranding defaultBranding,
    this.preview,
  }) : _defaultBranding = defaultBranding,
       _branding = defaultBranding;

  final BrandingRepository _repository;
  final GymBranding _defaultBranding;
  final BrandingPreview? preview;

  GymBranding _branding;
  String? _loadedGymId;

  GymBranding get branding => _branding;
  String? get previewLabel => preview?.label;

  bool isLoadedForGym(String gymId) {
    return _loadedGymId == gymId;
  }

  Future<void> loadForGym(String gymId) async {
    if (_loadedGymId == gymId) {
      return;
    }

    final branding = await _repository.getByGymId(gymId);

    _branding = preview?.branding ?? branding ?? _defaultBranding;
    _loadedGymId = gymId;

    notifyListeners();
  }

  void reset() {
    _loadedGymId = null;
    _branding = preview?.branding ?? _defaultBranding;
    notifyListeners();
  }
}
