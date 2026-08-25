import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:gymflow/core/network/api_exception.dart';
import 'package:gymflow/features/physical_assessments/data/physical_assessments_service.dart';
import 'package:gymflow/features/physical_assessments/models/physical_assessment.dart';
import 'package:http/http.dart' as http;

class PhysicalAssessmentDetailsViewModel extends ChangeNotifier {
  PhysicalAssessmentDetailsViewModel(
      this._service,
      this._studentId,
      this._assessmentId,
      );

  final PhysicalAssessmentsService _service;
  final String _studentId;
  final String _assessmentId;

  PhysicalAssessment? _assessment;
  bool _isLatest = false;
  bool _isLoading = false;
  String? _errorMessage;

  PhysicalAssessment? get assessment => _assessment;
  bool get isLatest => _isLatest;
  bool get isLoading => _isLoading;
  String? get errorMessage => _errorMessage;

  Future<void> load() async {
    if (_isLoading) {
      return;
    }

    _isLoading = true;
    _errorMessage = null;
    notifyListeners();

    try {
      final assessment = await _service.getById(
        studentId: _studentId,
        assessmentId: _assessmentId,
      );

      final latest = await _service.getLatest(_studentId);

      _assessment = assessment;
      _isLatest = latest?.id == assessment.id;
    } on ApiException catch (exception) {
      _errorMessage = _messageFromApiException(exception);
    } on TimeoutException {
      _errorMessage =
      'A avaliação demorou mais que o esperado para carregar.';
    } on http.ClientException {
      _errorMessage =
      'Não foi possível conectar ao servidor.';
    } on FormatException {
      _errorMessage =
      'Não foi possível interpretar os dados da avaliação.';
    } catch (_) {
      _errorMessage =
      'Não foi possível carregar a avaliação física.';
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }

  String _messageFromApiException(ApiException exception) {
    if (exception.statusCode == 404) {
      return 'Avaliação física não encontrada.';
    }

    if (exception.statusCode == 403) {
      return 'Você não possui permissão para acessar esta avaliação.';
    }

    if (exception.statusCode >= 500) {
      return 'A avaliação física está temporariamente indisponível.';
    }

    return 'Não foi possível carregar a avaliação física.';
  }
}
