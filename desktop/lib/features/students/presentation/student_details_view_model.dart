import 'dart:async';
import 'package:flutter/foundation.dart';
import 'package:avelri_gestao/core/network/api_exception.dart';
import 'package:avelri_gestao/features/students/data/students_service.dart';
import 'package:avelri_gestao/features/students/models/student_summary.dart';
import 'package:http/http.dart' as http;
import 'package:avelri_gestao/features/students/models/student_financial_history_item.dart';
import 'package:avelri_gestao/features/students/models/physical_access_credential.dart';
import 'package:avelri_gestao/features/students/models/physical_access_override.dart';

class StudentDetailsViewModel extends ChangeNotifier {
  StudentDetailsViewModel(this._studentsService, this._studentId);

  final StudentsService _studentsService;
  final String _studentId;
  final List<StudentFinancialHistoryItem> _financialHistory = [];
  final List<PhysicalAccessCredential>
  _physicalAccessCredentials = [];

  PhysicalAccessOverride?
  _physicalAccessOverride;
  bool _isPhysicalAccessLoading = false;
  String? _physicalAccessErrorMessage;

  StudentSummary? _student;
  bool _isLoading = false;
  bool _isUpdatingStatus = false;
  bool _isReactivating = false;

  bool _isFinancialHistoryLoading = false;
  String? _errorMessage;
  String? _financialHistoryErrorMessage;

  StudentSummary? get student => _student;
  bool get isLoading => _isLoading;
  bool get isUpdatingStatus => _isUpdatingStatus;
  String? get errorMessage => _errorMessage;
  bool get isReactivating => _isReactivating;

  List<StudentFinancialHistoryItem> get financialHistory =>
      List.unmodifiable(_financialHistory);

  bool get isFinancialHistoryLoading =>
      _isFinancialHistoryLoading;

  String? get financialHistoryErrorMessage =>
      _financialHistoryErrorMessage;

  List<PhysicalAccessCredential>
  get physicalAccessCredentials =>
      List.unmodifiable(
        _physicalAccessCredentials,
      );

  PhysicalAccessOverride?
  get physicalAccessOverride =>
      _physicalAccessOverride;

  bool get isPhysicalAccessLoading =>
      _isPhysicalAccessLoading;

  String? get physicalAccessErrorMessage =>
      _physicalAccessErrorMessage;


  Future<void> load() async {
    if (_isLoading) {
      return;
    }

    _isLoading = true;
    _errorMessage = null;
    notifyListeners();

    try {
      _student = await _studentsService.getStudentById(_studentId);

      await Future.wait([
        loadFinancialHistory(),
        loadPhysicalAccess(),
      ]);
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

  Future<void> loadPhysicalAccess() async {
    if (_isPhysicalAccessLoading) {
      return;
    }

    _isPhysicalAccessLoading = true;
    _physicalAccessErrorMessage = null;
    notifyListeners();

    try {
      final credentials =
      await _studentsService
          .getPhysicalAccessCredentials(
        _studentId,
      );

      final accessOverride =
      await _studentsService
          .getPhysicalAccessOverride(
        _studentId,
      );

      _physicalAccessCredentials
        ..clear()
        ..addAll(credentials);

      _physicalAccessOverride =
          accessOverride;
    } on ApiException catch (exception) {
      if (exception.statusCode == 403) {
        _physicalAccessErrorMessage =
        'Você não possui permissão para acessar o controle de acesso.';
      } else if (exception.statusCode >= 500) {
        _physicalAccessErrorMessage =
        'O controle de acesso está temporariamente indisponível.';
      } else {
        _physicalAccessErrorMessage =
        'Não foi possível carregar o controle de acesso.';
      }
    } on TimeoutException {
      _physicalAccessErrorMessage =
      'A consulta do controle de acesso demorou mais que o esperado.';
    } on http.ClientException {
      _physicalAccessErrorMessage =
      'Não foi possível conectar ao servidor.';
    } on FormatException {
      _physicalAccessErrorMessage =
      'Não foi possível interpretar os dados de controle de acesso.';
    } catch (_) {
      _physicalAccessErrorMessage =
      'Não foi possível carregar o controle de acesso.';
    } finally {
      _isPhysicalAccessLoading = false;
      notifyListeners();
    }
  }

  Future<bool> createPhysicalAccessCredential({
    required int type,
    required String providerKey,
    required String externalIdentifier,
  }) async {
    if (_isPhysicalAccessLoading) {
      return false;
    }

    _isPhysicalAccessLoading = true;
    _physicalAccessErrorMessage = null;
    notifyListeners();

    try {
      final credential =
      await _studentsService
          .createPhysicalAccessCredential(
        studentId: _studentId,
        type: type,
        providerKey: providerKey,
        externalIdentifier:
        externalIdentifier,
      );

      _physicalAccessCredentials.add(
        credential,
      );

      return true;
    } on ApiException catch (exception) {
      if (exception.statusCode == 409) {
        _physicalAccessErrorMessage =
        'Não foi possível cadastrar a credencial. Verifique se ela já está vinculada.';
      } else {
        _physicalAccessErrorMessage =
        'Não foi possível cadastrar a credencial de acesso.';
      }

      return false;
    } on TimeoutException {
      _physicalAccessErrorMessage =
      'A operação demorou mais que o esperado.';
      return false;
    } on http.ClientException {
      _physicalAccessErrorMessage =
      'Não foi possível conectar ao servidor.';
      return false;
    } catch (_) {
      _physicalAccessErrorMessage =
      'Não foi possível cadastrar a credencial de acesso.';
      return false;
    } finally {
      _isPhysicalAccessLoading = false;
      notifyListeners();
    }
  }

  Future<bool> setPhysicalAccessOverride({
    required int type,
    String? reason,
  }) async {
    if (_isPhysicalAccessLoading) {
      return false;
    }

    _isPhysicalAccessLoading = true;
    _physicalAccessErrorMessage = null;
    notifyListeners();

    try {
      final accessOverride =
      await _studentsService
          .setPhysicalAccessOverride(
        studentId: _studentId,
        type: type,
        reason: reason,
      );

      _physicalAccessOverride =
          accessOverride;

      return true;
    } on ApiException catch (_) {
      _physicalAccessErrorMessage =
      'Não foi possível definir a exceção manual de acesso.';
      return false;
    } on TimeoutException {
      _physicalAccessErrorMessage =
      'A operação demorou mais que o esperado.';
      return false;
    } on http.ClientException {
      _physicalAccessErrorMessage =
      'Não foi possível conectar ao servidor.';
      return false;
    } catch (_) {
      _physicalAccessErrorMessage =
      'Não foi possível definir a exceção manual de acesso.';
      return false;
    } finally {
      _isPhysicalAccessLoading = false;
      notifyListeners();
    }
  }

  Future<bool> removePhysicalAccessOverride() async {
    if (_isPhysicalAccessLoading) {
      return false;
    }

    _isPhysicalAccessLoading = true;
    _physicalAccessErrorMessage = null;
    notifyListeners();

    try {
      await _studentsService
          .removePhysicalAccessOverride(
        studentId: _studentId,
      );

      _physicalAccessOverride = null;

      return true;
    } on ApiException catch (exception) {
      if (exception.statusCode == 404) {
        _physicalAccessOverride = null;
        return true;
      }

      _physicalAccessErrorMessage =
      'Não foi possível remover a exceção manual de acesso.';
      return false;
    } on TimeoutException {
      _physicalAccessErrorMessage =
      'A operação demorou mais que o esperado.';
      return false;
    } on http.ClientException {
      _physicalAccessErrorMessage =
      'Não foi possível conectar ao servidor.';
      return false;
    } catch (_) {
      _physicalAccessErrorMessage =
      'Não foi possível remover a exceção manual de acesso.';
      return false;
    } finally {
      _isPhysicalAccessLoading = false;
      notifyListeners();
    }
  }

  Future<bool> updatePhysicalAccessCredentialStatus({
    required String credentialId,
    required bool isActive,
  }) async {
    if (_isPhysicalAccessLoading) {
      return false;
    }

    _isPhysicalAccessLoading = true;
    _physicalAccessErrorMessage = null;
    notifyListeners();

    try {
      await _studentsService
          .updatePhysicalAccessCredentialStatus(
        credentialId: credentialId,
        isActive: isActive,
      );

      final index =
      _physicalAccessCredentials.indexWhere(
            (credential) =>
        credential.id == credentialId,
      );

      if (index >= 0) {
        final current =
        _physicalAccessCredentials[index];

        _physicalAccessCredentials[index] =
            PhysicalAccessCredential(
              id: current.id,
              studentId: current.studentId,
              type: current.type,
              providerKey: current.providerKey,
              externalIdentifier:
              current.externalIdentifier,
              isActive: isActive,
              createdAt: current.createdAt,
              updatedAt: DateTime.now().toUtc(),
            );
      }

      return true;
    } on ApiException catch (_) {
      _physicalAccessErrorMessage =
      'Não foi possível alterar o status da credencial.';
      return false;
    } on TimeoutException {
      _physicalAccessErrorMessage =
      'A operação demorou mais que o esperado.';
      return false;
    } on http.ClientException {
      _physicalAccessErrorMessage =
      'Não foi possível conectar ao servidor.';
      return false;
    } catch (_) {
      _physicalAccessErrorMessage =
      'Não foi possível alterar o status da credencial.';
      return false;
    } finally {
      _isPhysicalAccessLoading = false;
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

  Future<bool> reactivateArchivedStudent({
    required String planId,
    required DateTime startDate,
  }) async {
    if (_isReactivating || _student?.archivedAt == null) {
      return false;
    }

    _isReactivating = true;
    _errorMessage = null;
    notifyListeners();

    try {
      await _studentsService.reactivateArchivedStudent(
        studentId: _studentId,
        planId: planId,
        startDate: startDate,
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
      _errorMessage = 'Não foi possível reativar o aluno.';
      return false;
    } finally {
      _isReactivating = false;
      notifyListeners();
    }
  }

  Future<void> loadFinancialHistory() async {
    if (_isFinancialHistoryLoading) {
      return;
    }

    _isFinancialHistoryLoading = true;
    _financialHistoryErrorMessage = null;
    notifyListeners();

    try {
      final history =
      await _studentsService
          .getStudentFinancialHistory(
        _studentId,
      );

      _financialHistory
        ..clear()
        ..addAll(history);
    } on ApiException catch (exception) {
      if (exception.statusCode == 403) {
        _financialHistoryErrorMessage =
        'Você não possui permissão para acessar o histórico financeiro.';
      } else if (exception.statusCode >= 500) {
        _financialHistoryErrorMessage =
        'O histórico financeiro está temporariamente indisponível.';
      } else {
        _financialHistoryErrorMessage =
        'Não foi possível carregar o histórico financeiro.';
      }
    } on TimeoutException {
      _financialHistoryErrorMessage =
      'A consulta do histórico financeiro demorou mais que o esperado.';
    } on http.ClientException {
      _financialHistoryErrorMessage =
      'Não foi possível conectar ao servidor.';
    } on FormatException {
      _financialHistoryErrorMessage =
      'Não foi possível interpretar o histórico financeiro.';
    } catch (_) {
      _financialHistoryErrorMessage =
      'Não foi possível carregar o histórico financeiro.';
    } finally {
      _isFinancialHistoryLoading = false;
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
