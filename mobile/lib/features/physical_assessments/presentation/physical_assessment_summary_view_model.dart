import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:gymflow/core/network/api_exception.dart';
import 'package:gymflow/features/physical_assessments/data/physical_assessments_service.dart';
import 'package:gymflow/features/physical_assessments/models/physical_assessment.dart';
import 'package:http/http.dart' as http;

class PhysicalAssessmentSummaryViewModel extends ChangeNotifier {
  PhysicalAssessmentSummaryViewModel(this._service, String studentId)
    : _studentId = studentId;

  PhysicalAssessmentSummaryViewModel.forCurrentUser(this._service)
    : _studentId = null;

  final PhysicalAssessmentsService _service;
  final String? _studentId;

  PhysicalAssessment? _latest;
  bool _isLoading = false;
  String? _errorMessage;

  PhysicalAssessment? get latest => _latest;
  bool get isLoading => _isLoading;
  String? get errorMessage => _errorMessage;

  bool get hasAssessment => _latest != null;

  bool get isReassessmentDue => _latest?.isReassessmentDue ?? false;

  Future<void> load() async {
    if (_isLoading) {
      return;
    }

    _isLoading = true;
    _errorMessage = null;
    notifyListeners();

    try {
      final studentId = _studentId;

      _latest = studentId == null
          ? await _service.getMyLatest()
          : await _service.getLatest(studentId);
    } on ApiException catch (exception) {
      _errorMessage = _messageFromApiException(exception);
    } on TimeoutException {
      _errorMessage =
          'A avaliação física demorou mais que o esperado para carregar.';
    } on http.ClientException {
      _errorMessage = 'Não foi possível carregar a avaliação física.';
    } on FormatException {
      _errorMessage =
          'Não foi possível interpretar os dados da avaliação física.';
    } catch (_) {
      _errorMessage = 'Não foi possível carregar a avaliação física.';
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }

  String _messageFromApiException(ApiException exception) {
    if (exception.statusCode == 403) {
      return 'Você não possui permissão para acessar a avaliação física.';
    }

    if (exception.statusCode >= 500) {
      return 'A avaliação física está temporariamente indisponível.';
    }

    return 'Não foi possível carregar a avaliação física.';
  }
}
