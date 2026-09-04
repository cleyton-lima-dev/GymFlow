import 'dart:async';
import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:gymflow/core/network/api_exception.dart';
import 'package:gymflow/features/students/data/students_service.dart';
import 'package:gymflow/features/students/models/student_summary.dart';
import 'package:http/http.dart' as http;

class EditStudentViewModel extends ChangeNotifier {
  EditStudentViewModel(
      this._studentsService,
      this._studentId,
      );

  final StudentsService _studentsService;
  final String _studentId;

  StudentSummary? _student;
  bool _isLoading = false;
  bool _isSaving = false;
  String? _errorMessage;

  StudentSummary? get student => _student;
  bool get isLoading => _isLoading;
  bool get isSaving => _isSaving;
  String? get errorMessage => _errorMessage;

  Future<void> load() async {
    if (_isLoading) {
      return;
    }

    _isLoading = true;
    _errorMessage = null;
    notifyListeners();

    try {
      _student = await _studentsService.getStudentById(
        _studentId,
      );
    } on ApiException catch (exception) {
      _errorMessage = _messageFromApiException(exception);
    } on TimeoutException {
      _errorMessage =
      'A consulta demorou mais que o esperado. Tente novamente.';
    } on http.ClientException {
      _errorMessage =
      'Não foi possível conectar ao servidor. Verifique sua conexão.';
    } on FormatException {
      _errorMessage =
      'Não foi possível carregar os dados do aluno.';
    } catch (_) {
      _errorMessage =
      'Não foi possível carregar os dados do aluno.';
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }

  Future<bool> save({
    required String name,
    required String email,
    String? phone,
    DateTime? birthDate,
  }) async {
    if (_isSaving) {
      return false;
    }

    _isSaving = true;
    _errorMessage = null;
    notifyListeners();

    try {
      await _studentsService.updateStudent(
        studentId: _studentId,
        name: name,
        email: email,
        phone: phone,
        birthDate: birthDate,
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
    } catch (_) {
      _errorMessage =
      'Não foi possível salvar as alterações. Tente novamente.';
      return false;
    } finally {
      _isSaving = false;
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
      return apiMessage ?? 'Já existe um usuário com este e-mail.';
    }

    if (exception.statusCode == 403) {
      return 'Você não possui permissão para editar alunos.';
    }

    if (exception.statusCode == 404) {
      return 'Aluno não encontrado.';
    }

    if (exception.statusCode >= 500) {
      return 'O Avelri está temporariamente indisponível. Tente novamente.';
    }

    return apiMessage ?? 'Não foi possível salvar as alterações.';
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
