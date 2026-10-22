import 'package:flutter/foundation.dart';
import 'package:avelri_gestao/core/network/api_client.dart';

/// Reads tenant entitlements from the API. Failures resolve to disabled so a
/// missing/older feature endpoint never blocks the traditional management UI.
class ErpFeatureController extends ChangeNotifier {
  ErpFeatureController(this._apiClient);

  final ApiClient _apiClient;

  String? _loadedGymId;
  String? _requestedGymId;
  Future<void>? _pendingLoad;
  int _loadGeneration = 0;
  int? _loadedSessionRevision;
  int? _requestedSessionRevision;
  bool _erpEnabled = false;
  bool _disposed = false;

  bool isLoadedForGym(String gymId) =>
      _loadedGymId == gymId &&
      _loadedSessionRevision == _apiClient.sessionRevision;

  bool isEnabledForGym(String gymId) => isLoadedForGym(gymId) && _erpEnabled;

  Future<void> loadForGym(String gymId, {bool refresh = false}) {
    if (_disposed) return Future<void>.value();
    final revision = _apiClient.sessionRevision;
    if (!refresh && isLoadedForGym(gymId)) return Future<void>.value();
    if (_requestedGymId == gymId &&
        _requestedSessionRevision == revision &&
        _pendingLoad != null) {
      return _pendingLoad!;
    }

    _requestedGymId = gymId;
    _requestedSessionRevision = revision;
    final pending = _load(gymId, ++_loadGeneration, revision);
    _pendingLoad = pending;
    return pending;
  }

  Future<void> _load(String gymId, int generation, int revision) async {
    var enabled = false;
    try {
      final response = await _apiClient.get('api/gym-features');
      if (response is Map<String, dynamic> && response['erpEnabled'] is bool) {
        enabled = response['erpEnabled'] as bool;
      }
    } catch (_) {
      // Entitlement lookup is fail-closed while traditional routes stay usable.
    }

    if (_disposed ||
        _requestedGymId != gymId ||
        generation != _loadGeneration ||
        revision != _apiClient.sessionRevision) {
      return;
    }
    _erpEnabled = enabled;
    _loadedGymId = gymId;
    _loadedSessionRevision = revision;
    _requestedGymId = null;
    _requestedSessionRevision = null;
    _pendingLoad = null;
    notifyListeners();
  }

  void reset() {
    _loadGeneration++;
    _loadedGymId = null;
    _loadedSessionRevision = null;
    _requestedGymId = null;
    _requestedSessionRevision = null;
    _pendingLoad = null;
    _erpEnabled = false;
    notifyListeners();
  }

  /// A rejection on a confirmed ERP endpoint closes the UI immediately; a
  /// rejection on an operational endpoint must not change commercial state.
  void denyCurrentSession() {
    _loadGeneration++;
    _requestedGymId = null;
    _requestedSessionRevision = null;
    _pendingLoad = null;
    _erpEnabled = false;
    if (!_disposed) notifyListeners();
  }

  @override
  void dispose() {
    _disposed = true;
    _loadGeneration++;
    super.dispose();
  }
}
