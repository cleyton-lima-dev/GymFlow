import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:gymflow/core/network/api_exception.dart';
import 'package:gymflow/features/students/data/students_service.dart';
import 'package:gymflow/features/students/models/student_summary.dart';
import 'package:http/http.dart' as http;

class StudentDetailsViewModel extends ChangeNotifier {
  StudentDetailsViewModel(
      this._studentsService,
      this._studentId,
      );

  final StudentsService _studentsService;
  final String _studentId;

  StudentSummary? _student;
  bool _isLoading = false;
  bool _isUpdatingStatus = false;
  bool _isUpdatingAppAccess = false;
  String? _errorMessage;
  String? _appAccessErrorMessage;

  StudentSummary? get student => _student;
  bool get isLoading => _isLoading;
  bool get isUpdatingStatus => _isUpdatingStatus;
  bool get isUpdatingAppAccess => _isUpdatingAppAccess;
  String? get errorMessage => _errorMessage;
  String? get appAccessErrorMessage => _appAccessErrorMessage;

  Future<({bool succeeded, String? token, DateTime? expiresAt})> setAppAccess({
    required bool enabled,
    String? email,
  }) async {
    if (_isUpdatingAppAccess) {
      return (succeeded: false, token: null, expiresAt: null);
    }

    final currentStudent = _student;
    if (currentStudent == null) {
      _appAccessErrorMessage = 'Não foi possível carregar os dados do aluno.';
      notifyListeners();
      return (succeeded: false, token: null, expiresAt: null);
    }

    _isUpdatingAppAccess = true;
    _appAccessErrorMessage = null;
    notifyListeners();

    try {
      final normalizedEmail = email?.trim().toLowerCase();
      final result = await _studentsService.setAppAccess(
        studentId: _studentId,
        enabled: enabled,
        email: enabled && normalizedEmail?.isNotEmpty == true
            ? normalizedEmail
            : null,
      );

      _student = _copyStudent(
        currentStudent,
        email: enabled && normalizedEmail?.isNotEmpty == true
            ? normalizedEmail
            : currentStudent.email,
        appAccessEnabled: enabled,
      );
      if (result.token == null) {
        try {
          _student = await _studentsService.getStudentById(_studentId);
        } catch (_) {
          // The access operation succeeded; keep its result if refresh fails.
        }
      }

      return (
        succeeded: true,
        token: result.token,
        expiresAt: result.expiresAt,
      );
    } on ApiException catch (exception) {
      _appAccessErrorMessage = _appAccessMessageFromApiException(exception);
      if (exception.statusCode == 409) {
        try {
          _student = await _studentsService.getStudentById(_studentId);
        } catch (_) {
          // Keep the conflict message if the follow-up read also fails.
        }
      }
      return (succeeded: false, token: null, expiresAt: null);
    } on TimeoutException {
      _appAccessErrorMessage =
          'A operação demorou mais que o esperado. Verifique o estado do acesso antes de tentar novamente.';
      return (succeeded: false, token: null, expiresAt: null);
    } on http.ClientException {
      _appAccessErrorMessage =
          'Não foi possível conectar ao servidor. Verifique sua conexão.';
      return (succeeded: false, token: null, expiresAt: null);
    } on FormatException {
      _appAccessErrorMessage = 'O servidor retornou uma resposta inválida.';
      return (succeeded: false, token: null, expiresAt: null);
    } catch (_) {
      _appAccessErrorMessage =
          'Não foi possível liberar o acesso ao aplicativo.';
      return (succeeded: false, token: null, expiresAt: null);
    } finally {
      _isUpdatingAppAccess = false;
      notifyListeners();
    }
  }

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
      'A operação demorou mais que o esperado. Tente novamente.';
      return false;
    } on http.ClientException {
      _errorMessage =
      'Não foi possível conectar ao servidor. Verifique sua conexão.';
      return false;
    } catch (_) {
      _errorMessage =
      'Não foi possível alterar o status do aluno.';
      return false;
    } finally {
      _isUpdatingStatus = false;
      notifyListeners();
    }
  }

  String _messageFromApiException(ApiException exception) {
    if (exception.statusCode == 404) {
      return 'Aluno não encontrado.';
    }

    if (exception.statusCode == 403) {
      return 'Você não possui permissão para acessar este aluno.';
    }

    if (exception.statusCode >= 500) {
      return 'O Avelri está temporariamente indisponível. Tente novamente.';
    }

    return 'Não foi possível carregar os dados do aluno.';
  }

  String _appAccessMessageFromApiException(ApiException exception) {
    return switch (exception.statusCode) {
      400 => 'Informe um e-mail válido para liberar o acesso ao aplicativo.',
      401 => 'Sua sessão expirou. Entre novamente para continuar.',
      403 => 'Somente um Admin autorizado pode liberar o acesso.',
      404 => 'Aluno não encontrado ou indisponível para liberação.',
      409 => 'Já existe uma conta usando este e-mail.',
      >= 500 => 'O Avelri está temporariamente indisponível. Tente novamente.',
      _ => 'Não foi possível liberar o acesso ao aplicativo.',
    };
  }

  StudentSummary _copyStudent(
    StudentSummary student, {
    String? email,
    bool? appAccessEnabled,
  }) =>
      StudentSummary(
        id: student.id,
        name: student.name,
        email: email ?? student.email,
        phone: student.phone,
        birthDate: student.birthDate,
        isActive: student.isActive,
        createdAt: student.createdAt,
        archivedAt: student.archivedAt,
        appAccessEnabled: appAccessEnabled ?? student.appAccessEnabled,
        userId: student.userId,
      );
}
