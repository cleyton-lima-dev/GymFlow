import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:avelri_gestao/core/network/api_exception.dart';
import 'package:avelri_gestao/features/reports/data/reports_service.dart';

class ReportsViewModel extends ChangeNotifier {
  ReportsViewModel(this.service);
  final ReportsService service;
  ReportResult? result;
  String? error;
  bool isLoading = false, isExporting = false, _disposed = false;
  String _kind = 'students';
  Map<String, String> _applied = {};
  bool get busy => isLoading || isExporting;

  Future<void> load(
    String kind,
    Map<String, String> filters, {
    int page = 1,
  }) async {
    if (busy || _disposed) return;
    isLoading = true;
    result = null;
    error = null;
    notifyListeners();
    try {
      final applied = Map<String, String>.from(filters)..['page'] = '$page';
      final data = await service.load(kind, applied);
      if (!_disposed) {
        result = data;
        _kind = kind;
        if (data.fromDate != null) {
          applied['fromDate'] = data.fromDate!;
          applied['toDate'] = data.toDate!;
        }
        _applied = Map.unmodifiable(applied);
      }
    } catch (e) {
      if (!_disposed) error = _message(e);
    } finally {
      isLoading = false;
      if (!_disposed) notifyListeners();
    }
  }

  Future<void> goTo(int page) => load(_kind, _applied, page: page);

  Future<Uint8List?> export() async {
    if (busy || result == null || _disposed) return null;
    isExporting = true;
    error = null;
    notifyListeners();
    try {
      // Freeze the effective server period, even if midnight passes before exporting.
      final filters = Map<String, String>.from(_applied)..remove('page');
      if (result!.fromDate != null) {
        filters['fromDate'] = result!.fromDate!;
        filters['toDate'] = result!.toDate!;
      }
      return await service.export(_kind, filters);
    } catch (e) {
      if (!_disposed) error = _message(e);
      return null;
    } finally {
      isExporting = false;
      if (!_disposed) notifyListeners();
    }
  }

  String _message(Object e) {
    if (e is ApiException && e.statusCode == 400) {
      try {
        final data = jsonDecode(e.message) as Map<String, dynamic>;
        if (data['message'] is String) return data['message'] as String;
      } catch (_) {
        /* Non-JSON errors use the generic message below. */
      }
    }
    return 'Não foi possível obter o relatório. Verifique os filtros e tente novamente.';
  }

  @override
  void dispose() {
    _disposed = true;
    super.dispose();
  }
}
