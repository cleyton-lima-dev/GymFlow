import 'dart:async';
import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:avelri_gestao/core/network/api_exception.dart';
import 'package:avelri_gestao/features/plans/data/plans_service.dart';
import 'package:avelri_gestao/features/plans/models/plan_summary.dart';
import 'package:http/http.dart' as http;

class CreatePlanViewModel extends ChangeNotifier {
  CreatePlanViewModel(this._plansService);

  final PlansService _plansService;

  bool _isLoading = false;
  String? _errorMessage;

  bool get isLoading => _isLoading;
  String? get errorMessage => _errorMessage;

  Future<bool> createPlan({
    required String name,
    required double price,
    required int durationMonths,
    required PlanBillingCycle billingCycle,
  }) async {
    if (_isLoading) {
      return false;
    }

    _isLoading = true;
    _errorMessage = null;
    notifyListeners();

    try {
      await _plansService.createPlan(
        name: name,
        price: price,
        durationMonths: durationMonths,
        billingCycle: billingCycle,
      );

      return true;
    } on ApiException catch (exception) {
      _errorMessage = _messageFromApiException(exception);
      return false;
    } on TimeoutException {
      _errorMessage =
      'A operação demorou mais que o esperado. Tente novamente.';
      return false;
    } on http.ClientException {
      _errorMessage =
      'Não foi possível conectar ao servidor. Verifique sua conexão.';
      return false;
    } on FormatException {
      _errorMessage =
      'O servidor retornou uma resposta inválida. Tente novamente.';
      return false;
    } catch (_) {
      _errorMessage =
      'Não foi possível cadastrar o plano. Tente novamente.';
      return false;
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }

  void clearError() {
    if (_errorMessage == null) {
      return;
    }

    _errorMessage = null;
    notifyListeners();
  }

  String _messageFromApiException(ApiException exception) {
    final apiMessage = _extractApiMessage(exception.message);

    if (exception.statusCode == 409) {
      return apiMessage ?? 'Já existe um plano com este nome.';
    }

    if (exception.statusCode == 403) {
      return 'Você não possui permissão para cadastrar planos.';
    }

    if (exception.statusCode >= 500) {
      return 'O Avelri está temporariamente indisponível. Tente novamente.';
    }

    return apiMessage ?? 'Não foi possível cadastrar o plano.';
  }

  String? _extractApiMessage(String responseBody) {
    try {
      final decoded = jsonDecode(responseBody);

      if (decoded is Map<String, dynamic>) {
        final message = decoded['message'];

        if (message is String && message.trim().isNotEmpty) {
          return message.trim();
        }
      }
    } catch (_) {
      return null;
    }

    return null;
  }
}
