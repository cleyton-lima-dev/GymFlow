import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:avelri_gestao/core/network/api_exception.dart';
import 'package:avelri_gestao/features/plans/data/plans_service.dart';
import 'package:avelri_gestao/features/plans/models/plan_summary.dart';
import 'package:http/http.dart' as http;

enum PlansStatusFilter { all, active, inactive }

class PlansViewModel extends ChangeNotifier {
  PlansViewModel(this._plansService);

  final PlansService _plansService;

  final List<PlanSummary> _plans = [];

  bool _isLoading = false;
  bool _isDisposed = false;

  String? _errorMessage;

  PlansStatusFilter _statusFilter = PlansStatusFilter.all;

  List<PlanSummary> get plans => List.unmodifiable(_plans);

  bool get isLoading => _isLoading;

  String? get errorMessage => _errorMessage;

  PlansStatusFilter get statusFilter => _statusFilter;

  bool get hasPlans => _plans.isNotEmpty;

  Future<bool> updatePlanStatus(PlanSummary plan) async {
    try {
      await _plansService.updatePlanStatus(
        planId: plan.id,
        isActive: !plan.isActive,
      );

      await refresh();

      return true;
    } on ApiException catch (exception) {
      _errorMessage = _messageFromApiException(exception);
      _notifySafely();
      return false;
    } on TimeoutException {
      _errorMessage =
      'A operação demorou mais que o esperado. Tente novamente.';
      _notifySafely();
      return false;
    } on http.ClientException {
      _errorMessage =
      'Não foi possível conectar ao servidor. Verifique sua conexão.';
      _notifySafely();
      return false;
    } catch (_) {
      _errorMessage =
      'Não foi possível alterar o status do plano. Tente novamente.';
      _notifySafely();
      return false;
    }
  }

  Future<void> loadInitial() {
    return _load(showLoading: true);
  }

  Future<void> updateStatusFilter(PlansStatusFilter filter) async {
    if (_statusFilter == filter) {
      return;
    }

    _statusFilter = filter;

    await _load(showLoading: true);
  }

  Future<void> retry() {
    return _load(showLoading: true);
  }

  Future<void> refresh() {
    return _load(showLoading: false);
  }

  Future<void> _load({required bool showLoading}) async {
    if (showLoading) {
      _isLoading = true;
    }

    _errorMessage = null;
    _notifySafely();

    try {
      final plans = await _plansService.getPlans(
        isActive: _isActiveValue,
      );

      if (_isDisposed) {
        return;
      }

      _plans
        ..clear()
        ..addAll(plans);
    } on ApiException catch (exception) {
      if (_isDisposed) {
        return;
      }

      _errorMessage = _messageFromApiException(exception);
    } on TimeoutException {
      if (_isDisposed) {
        return;
      }

      _errorMessage =
      'A consulta demorou mais que o esperado. Tente novamente.';
    } on http.ClientException {
      if (_isDisposed) {
        return;
      }

      _errorMessage =
      'Não foi possível conectar ao servidor. Verifique sua conexão.';
    } on FormatException {
      if (_isDisposed) {
        return;
      }

      _errorMessage =
      'Não foi possível carregar os planos. Tente novamente.';
    } catch (_) {
      if (_isDisposed) {
        return;
      }

      _errorMessage =
      'Não foi possível carregar os planos. Tente novamente.';
    } finally {
      if (!_isDisposed) {
        _isLoading = false;
        notifyListeners();
      }
    }
  }

  bool? get _isActiveValue {
    return switch (_statusFilter) {
      PlansStatusFilter.all => null,
      PlansStatusFilter.active => true,
      PlansStatusFilter.inactive => false,
    };
  }

  String _messageFromApiException(ApiException exception) {
    if (exception.statusCode == 403) {
      return 'Você não possui permissão para acessar os planos.';
    }

    if (exception.statusCode >= 500) {
      return 'O Avelri está temporariamente indisponível. Tente novamente.';
    }

    return 'Não foi possível carregar os planos. Tente novamente.';
  }

  void _notifySafely() {
    if (!_isDisposed) {
      notifyListeners();
    }
  }

  @override
  void dispose() {
    _isDisposed = true;
    super.dispose();
  }
}
