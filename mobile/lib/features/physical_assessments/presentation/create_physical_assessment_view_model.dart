import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:gymflow/core/network/api_exception.dart';
import 'package:gymflow/features/physical_assessments/data/physical_assessments_service.dart';
import 'package:gymflow/features/physical_assessments/models/create_physical_assessment_request.dart';
import 'package:http/http.dart' as http;

class CreatePhysicalAssessmentViewModel extends ChangeNotifier {
  CreatePhysicalAssessmentViewModel(this._service, this._studentId);

  final PhysicalAssessmentsService _service;
  final String _studentId;

  bool _isSubmitting = false;
  String? _errorMessage;

  bool get isSubmitting => _isSubmitting;
  String? get errorMessage => _errorMessage;

  Future<DateTime?> loadCurrentGymDate() async {
    _errorMessage = null;

    try {
      return await _service.getCurrentDate(_studentId);
    } on ApiException {
      _errorMessage = 'Não foi possível obter a data atual da academia.';
    } on TimeoutException {
      _errorMessage =
          'A consulta da data da academia demorou mais que o esperado.';
    } on http.ClientException {
      _errorMessage = 'Não foi possível conectar ao servidor.';
    } on FormatException {
      _errorMessage = 'Não foi possível interpretar a data atual da academia.';
    } catch (_) {
      _errorMessage = 'Não foi possível obter a data atual da academia.';
    }

    notifyListeners();
    return null;
  }

  Future<bool> submit({
    required DateTime assessmentDate,
    required String weightKg,
    required String heightCm,
    required String bodyFatPercentage,
    required String chestCm,
    required String waistCm,
    required String abdomenCm,
    required String hipCm,
    required String rightArmCm,
    required String leftArmCm,
    required String rightThighCm,
    required String leftThighCm,
    required String rightCalfCm,
    required String leftCalfCm,
    required String notes,
  }) async {
    if (_isSubmitting) {
      return false;
    }

    _errorMessage = null;

    final parsedWeight = _parseRequiredNumber(
      value: weightKg,
      fieldName: 'peso',
    );

    if (parsedWeight == null) {
      return false;
    }

    final parsedHeight = _parseRequiredNumber(
      value: heightCm,
      fieldName: 'altura',
    );

    if (parsedHeight == null) {
      return false;
    }

    final parsedBodyFat = _parseOptionalNumber(
      value: bodyFatPercentage,
      fieldName: '% de gordura',
    );

    if (_errorMessage != null) {
      return false;
    }

    final parsedChest = _parseOptionalNumber(
      value: chestCm,
      fieldName: 'peito',
    );

    if (_errorMessage != null) {
      return false;
    }

    final parsedWaist = _parseOptionalNumber(
      value: waistCm,
      fieldName: 'cintura',
    );

    if (_errorMessage != null) {
      return false;
    }

    final parsedAbdomen = _parseOptionalNumber(
      value: abdomenCm,
      fieldName: 'abdômen',
    );

    if (_errorMessage != null) {
      return false;
    }

    final parsedHip = _parseOptionalNumber(value: hipCm, fieldName: 'quadril');

    if (_errorMessage != null) {
      return false;
    }

    final parsedRightArm = _parseOptionalNumber(
      value: rightArmCm,
      fieldName: 'braço direito',
    );

    if (_errorMessage != null) {
      return false;
    }

    final parsedLeftArm = _parseOptionalNumber(
      value: leftArmCm,
      fieldName: 'braço esquerdo',
    );

    if (_errorMessage != null) {
      return false;
    }

    final parsedRightThigh = _parseOptionalNumber(
      value: rightThighCm,
      fieldName: 'coxa direita',
    );

    if (_errorMessage != null) {
      return false;
    }

    final parsedLeftThigh = _parseOptionalNumber(
      value: leftThighCm,
      fieldName: 'coxa esquerda',
    );

    if (_errorMessage != null) {
      return false;
    }

    final parsedRightCalf = _parseOptionalNumber(
      value: rightCalfCm,
      fieldName: 'panturrilha direita',
    );

    if (_errorMessage != null) {
      return false;
    }

    final parsedLeftCalf = _parseOptionalNumber(
      value: leftCalfCm,
      fieldName: 'panturrilha esquerda',
    );

    if (_errorMessage != null) {
      return false;
    }

    final normalizedNotes = notes.trim();

    _isSubmitting = true;
    notifyListeners();

    try {
      await _service.create(
        studentId: _studentId,
        request: CreatePhysicalAssessmentRequest(
          assessmentDate: assessmentDate,
          weightKg: parsedWeight,
          heightCm: parsedHeight,
          bodyFatPercentage: parsedBodyFat,
          chestCm: parsedChest,
          waistCm: parsedWaist,
          abdomenCm: parsedAbdomen,
          hipCm: parsedHip,
          rightArmCm: parsedRightArm,
          leftArmCm: parsedLeftArm,
          rightThighCm: parsedRightThigh,
          leftThighCm: parsedLeftThigh,
          rightCalfCm: parsedRightCalf,
          leftCalfCm: parsedLeftCalf,
          notes: normalizedNotes.isEmpty ? null : normalizedNotes,
        ),
      );

      return true;
    } on ApiException catch (exception) {
      _errorMessage = _messageFromApiException(exception);
      return false;
    } on TimeoutException {
      _errorMessage =
          'O cadastro demorou mais que o esperado. Tente novamente.';
      return false;
    } on http.ClientException {
      _errorMessage = 'Não foi possível conectar ao servidor.';
      return false;
    } catch (_) {
      _errorMessage = 'Não foi possível cadastrar a avaliação física.';
      return false;
    } finally {
      _isSubmitting = false;
      notifyListeners();
    }
  }

  double? _parseRequiredNumber({
    required String value,
    required String fieldName,
  }) {
    final normalized = value.trim().replaceAll(',', '.');

    if (normalized.isEmpty) {
      _errorMessage = 'Informe $fieldName.';
      notifyListeners();
      return null;
    }

    final parsed = double.tryParse(normalized);

    if (parsed == null || parsed <= 0) {
      _errorMessage = 'Informe um valor válido para $fieldName.';
      notifyListeners();
      return null;
    }

    return parsed;
  }

  double? _parseOptionalNumber({
    required String value,
    required String fieldName,
  }) {
    final normalized = value.trim().replaceAll(',', '.');

    if (normalized.isEmpty) {
      return null;
    }

    final parsed = double.tryParse(normalized);

    if (parsed == null || parsed <= 0) {
      _errorMessage = 'Informe um valor válido para $fieldName.';
      notifyListeners();
      return null;
    }

    return parsed;
  }

  String _messageFromApiException(ApiException exception) {
    if (exception.statusCode == 409) {
      return 'Já existe uma avaliação física registrada para esta data.';
    }

    if (exception.statusCode == 403) {
      return 'Você não possui permissão para cadastrar avaliações físicas.';
    }

    if (exception.statusCode == 400) {
      return 'Verifique os dados informados e tente novamente.';
    }

    if (exception.statusCode >= 500) {
      return 'O serviço de avaliações físicas está temporariamente indisponível.';
    }

    return 'Não foi possível cadastrar a avaliação física.';
  }
}
