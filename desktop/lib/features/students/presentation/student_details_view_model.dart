import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:avelri_gestao/core/network/api_exception.dart';
import 'package:avelri_gestao/features/students/data/students_service.dart';
import 'package:avelri_gestao/features/students/models/student_summary.dart';
import 'package:http/http.dart' as http;

class StudentDetailsViewModel extends ChangeNotifier {
  StudentDetailsViewModel(this._studentsService, this._studentId);

  final StudentsService _studentsService;
  final String _studentId;

  StudentSummary? _student;
  bool _isLoading = false;
  bool _isUpdatingStatus = false;
  String? _errorMessage;

  StudentSummary? get student => _student;
  bool get isLoading => _isLoading;
  bool get isUpdatingStatus => _isUpdatingStatus;
  String? get errorMessage => _errorMessage;

  Future<void> load() async {
    if (_isLoading) {
      return;
    }

    _isLoading = true;
    _errorMessage = null;
    notifyListeners();

    try {
      _student = await _studentsService.getStudentById(_studentId);
    } on ApiException catch (exception) {
      _errorMessage = _messageFromApiException(exception);
    } on TimeoutException {
      _errorMessage =
          'A consulta demorou mais que o esperado. Tente novamente.';
    } on http.ClientException {
      _errorMessage =
          'NÃ£o foi possÃ­vel conectar ao servidor. Verifique sua conexÃ£o.';
    } on FormatException {
      _errorMessage = 'NÃ£o foi possÃ­vel carregar os dados do aluno.';
    } catch (_) {
      _errorMessage = 'NÃ£o foi possÃ­vel carregar os dados do aluno.';
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }

  Future<bool> updateStatus(bool isActive) async {
    if (_isUpdatingStatus || _student == null) {
      return false;
    }

    _isUpdatingStatus = true;
    _errorMessage = null;
    notifyListeners();

    try {
      await _studentsService.updateStudentStatus(
        studentId: _studentId,
        isActive: isActive,
      );

      await load();

      return true;
    } on ApiException catch (exception) {
      _errorMessage = _messageFromApiException(exception);
      return false;
    } on TimeoutException {
      _errorMessage =
          'A operaÃ§Ã£o demorou mais que o esperado. Tente novamente.';
      return false;
    } on http.ClientException {
      _errorMessage =
          'NÃ£o foi possÃ­vel conectar ao servidor. Verifique sua conexÃ£o.';
      return false;
    } catch (_) {
      _errorMessage = 'NÃ£o foi possÃ­vel alterar o status do aluno.';
      return false;
    } finally {
      _isUpdatingStatus = false;
      notifyListeners();
    }
  }

  String _messageFromApiException(ApiException exception) {
    if (exception.statusCode == 404) {
      return 'Aluno nÃ£o encontrado.';
    }

    if (exception.statusCode == 403) {
      return 'VocÃª nÃ£o possui permissÃ£o para acessar este aluno.';
    }

    if (exception.statusCode >= 500) {
      return 'O Avelri estÃ¡ temporariamente indisponÃ­vel. Tente novamente.';
    }

    return 'NÃ£o foi possÃ­vel carregar os dados do aluno.';
  }
}
