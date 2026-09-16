import 'dart:async';

import 'package:avelri_gestao/core/network/api_exception.dart';
import 'package:avelri_gestao/features/enrollments/data/enrollments_service.dart';
import 'package:avelri_gestao/features/enrollments/models/enrollment_summary.dart';
import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;

enum EnrollmentsStatusFilter {
  all,
  active,
  cancelled,
  expired,
}

class EnrollmentsViewModel extends ChangeNotifier {
  EnrollmentsViewModel(this._enrollmentsService);

  final EnrollmentsService _enrollmentsService;

  final List<EnrollmentSummary> _enrollments = [];

  bool _isLoading = false;
  bool _isDisposed = false;
  String? _errorMessage;

  EnrollmentsStatusFilter _statusFilter =
      EnrollmentsStatusFilter.all;

  List<EnrollmentSummary> get enrollments =>
      List.unmodifiable(_enrollments);

  bool get isLoading => _isLoading;

  String? get errorMessage => _errorMessage;

  bool get hasEnrollments => _enrollments.isNotEmpty;

  EnrollmentsStatusFilter get statusFilter =>
      _statusFilter;

  Future<void> loadInitial() {
    return _load(showLoading: true);
  }

  Future<void> retry() {
    return _load(showLoading: true);
  }

  Future<void> refresh() {
    return _load(showLoading: false);
  }

  Future<void> updateStatusFilter(
      EnrollmentsStatusFilter filter,
      ) async {
    if (_statusFilter == filter) {
      return;
    }

    _statusFilter = filter;

    await _load(showLoading: true);
  }

  Future<bool> cancelEnrollment(
      EnrollmentSummary enrollment,
      ) async {
    try {
      await _enrollmentsService.cancelEnrollment(
        enrollmentId: enrollment.id,
      );

      await refresh();

      return true;
    } on ApiException catch (exception) {
      _errorMessage =
          _messageFromApiException(exception);
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
      'Não foi possível cancelar a matrícula. Tente novamente.';
      _notifySafely();

      return false;
    }
  }

  Future<bool> renewEnrollment(
      EnrollmentSummary enrollment,
      String planId,
      ) async {
    try {
      await _enrollmentsService.renewEnrollment(
        enrollmentId: enrollment.id,
        planId: planId,
      );

      await refresh();

      return true;
    } on ApiException catch (exception) {
      _errorMessage =
          _messageFromApiException(exception);
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
      'Não foi possível renovar a matrícula. Tente novamente.';
      _notifySafely();

      return false;
    }
  }

  Future<void> _load({
    required bool showLoading,
  }) async {
    if (showLoading) {
      _isLoading = true;
    }

    _errorMessage = null;
    _notifySafely();

    try {
      final enrollments =
      await _enrollmentsService.getEnrollments(
        status: _statusValue,
      );

      if (_isDisposed) {
        return;
      }

      _enrollments
        ..clear()
        ..addAll(enrollments);
    } on ApiException catch (exception) {
      if (_isDisposed) {
        return;
      }

      _errorMessage =
          _messageFromApiException(exception);
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
      'Não foi possível carregar as matrículas. Tente novamente.';
    } catch (_) {
      if (_isDisposed) {
        return;
      }

      _errorMessage =
      'Não foi possível carregar as matrículas. Tente novamente.';
    } finally {
      if (!_isDisposed) {
        _isLoading = false;
        notifyListeners();
      }
    }
  }

  EnrollmentStatus? get _statusValue {
    return switch (_statusFilter) {
      EnrollmentsStatusFilter.all => null,
      EnrollmentsStatusFilter.active =>
      EnrollmentStatus.active,
      EnrollmentsStatusFilter.cancelled =>
      EnrollmentStatus.cancelled,
      EnrollmentsStatusFilter.expired =>
      EnrollmentStatus.expired,
    };
  }

  String _messageFromApiException(
      ApiException exception,
      ) {
    if (exception.statusCode == 403) {
      return 'Você não possui permissão para acessar as matrículas.';
    }

    if (exception.statusCode == 409) {
      return 'Não foi possível realizar a operação com esta matrícula.';
    }

    if (exception.statusCode >= 500) {
      return 'O Avelri está temporariamente indisponível. Tente novamente.';
    }

    return 'Não foi possível carregar as matrículas. Tente novamente.';
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
