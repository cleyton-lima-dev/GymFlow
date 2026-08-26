import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:gymflow/core/network/api_exception.dart';
import 'package:gymflow/features/students/data/students_service.dart';
import 'package:gymflow/features/students/models/student_summary.dart';
import 'package:http/http.dart' as http;

class StudentProfileViewModel extends ChangeNotifier {
  StudentProfileViewModel(this._studentsService);

  final StudentsService _studentsService;

  StudentSummary? _student;
  bool _isLoading = false;
  bool _hasLoaded = false;
  String? _errorMessage;

  StudentSummary? get student => _student;
  bool get isLoading => _isLoading;
  bool get hasLoaded => _hasLoaded;
  String? get errorMessage => _errorMessage;

  Future<void> load() async {
    if (_isLoading) {
      return;
    }

    _isLoading = true;
    _errorMessage = null;
    notifyListeners();

    try {
      _student = await _studentsService.getMe();
      _hasLoaded = true;
    } on ApiException catch (exception) {
      _errorMessage = _messageFromApiException(exception);
      _hasLoaded = true;
    } on TimeoutException {
      _errorMessage = 'O servidor demorou mais que o esperado.';
      _hasLoaded = true;
    } on http.ClientException {
      _errorMessage = 'Não foi possível conectar ao servidor.';
      _hasLoaded = true;
    } on FormatException {
      _errorMessage = 'Não foi possível interpretar seus dados.';
      _hasLoaded = true;
    } catch (_) {
      _errorMessage = 'Não foi possível carregar seus dados.';
      _hasLoaded = true;
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }

  Future<void> refresh() {
    return load();
  }

  String _messageFromApiException(ApiException exception) {
    if (exception.statusCode == 404) {
      return 'Cadastro de aluno não encontrado.';
    }

    if (exception.statusCode == 403) {
      return 'Você não possui permissão para acessar estes dados.';
    }

    if (exception.statusCode >= 500) {
      return 'O serviço está temporariamente indisponível.';
    }

    return 'Não foi possível carregar seus dados.';
  }
}
