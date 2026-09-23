import 'dart:async';

import 'package:avelri_gestao/core/network/api_exception.dart';
import 'package:avelri_gestao/features/charges/data/charges_service.dart';
import 'package:avelri_gestao/features/charges/models/charge_summary.dart';
import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;

class ChargesViewModel extends ChangeNotifier {
  ChargesViewModel(this._chargesService);

  final ChargesService _chargesService;

  final List<ChargeSummary> _charges = [];

  bool _isLoading = false;
  bool _isDisposed = false;
  String? _errorMessage;

  List<ChargeSummary> get charges =>
      List.unmodifiable(_charges);

  bool get isLoading => _isLoading;

  String? get errorMessage => _errorMessage;

  bool get hasCharges => _charges.isNotEmpty;

  Future<void> loadInitial() {
    return _load(showLoading: true);
  }

  Future<void> retry() {
    return _load(showLoading: true);
  }

  Future<void> refresh() {
    return _load(showLoading: false);
  }

  Future<bool> confirmPayment(
      ChargeSummary charge,
      ) async {
    try {
      await _chargesService.confirmPayment(
        chargeId: charge.id,
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
      'Não foi possível confirmar o pagamento. Tente novamente.';
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
      final charges =
      await _chargesService.getCharges();

      if (_isDisposed) {
        return;
      }

      _charges
        ..clear()
        ..addAll(charges);
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
      'Não foi possível carregar as cobranças. Tente novamente.';
    } catch (_) {
      if (_isDisposed) {
        return;
      }

      _errorMessage =
      'Não foi possível carregar as cobranças. Tente novamente.';
    } finally {
      if (!_isDisposed) {
        _isLoading = false;
        notifyListeners();
      }
    }
  }

  String _messageFromApiException(
      ApiException exception,
      ) {
    if (exception.statusCode == 403) {
      return 'Você não possui permissão para acessar as cobranças.';
    }

    if (exception.statusCode >= 500) {
      return 'O Avelri está temporariamente indisponível. Tente novamente.';
    }

    return 'Não foi possível carregar as cobranças. Tente novamente.';
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
