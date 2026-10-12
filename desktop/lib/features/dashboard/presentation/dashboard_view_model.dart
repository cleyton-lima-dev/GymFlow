import 'package:flutter/foundation.dart';
import 'package:avelri_gestao/features/dashboard/data/dashboard_service.dart';
import 'package:avelri_gestao/features/dashboard/models/dashboard_summary.dart';

class DashboardViewModel extends ChangeNotifier {
  DashboardViewModel(this._service);
  final DashboardService _service;
  DashboardSummary? summary;
  bool isLoading = false;
  String? error;
  bool _disposed = false;

  Future<void> load() async {
    if (isLoading || _disposed) return;
    isLoading = true;
    error = null;
    notifyListeners();
    try {
      final result = await _service.load();
      if (!_disposed) summary = result;
    } catch (_) {
      if (!_disposed)
        error = 'Não foi possível carregar os indicadores. Tente novamente.';
    } finally {
      isLoading = false;
      if (!_disposed) notifyListeners();
    }
  }

  @override
  void dispose() {
    _disposed = true;
    super.dispose();
  }
}
