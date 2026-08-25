import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:gymflow/core/network/api_exception.dart';
import 'package:gymflow/features/students/data/students_service.dart';
import 'package:gymflow/features/students/models/student_summary.dart';
import 'package:http/http.dart' as http;

enum StudentsStatusFilter {
  all,
  active,
  inactive,
}

class StudentsViewModel extends ChangeNotifier {
  StudentsViewModel(this._studentsService);

  static const int pageSize = 20;
  static const Duration _searchDebounce = Duration(milliseconds: 400);

  final StudentsService _studentsService;

  final List<StudentSummary> _students = [];

  Timer? _searchTimer;

  bool _isLoading = false;
  bool _isDisposed = false;

  String _search = '';
  String? _errorMessage;

  StudentsStatusFilter _statusFilter = StudentsStatusFilter.all;

  int _page = 1;
  int _totalPages = 1;
  int _totalCount = 0;

  int _requestVersion = 0;

  List<StudentSummary> get students =>
      List.unmodifiable(_students);

  bool get isLoading => _isLoading;

  String get search => _search;

  String? get errorMessage => _errorMessage;

  StudentsStatusFilter get statusFilter => _statusFilter;

  int get page => _page;

  int get totalPages => _totalPages;

  int get totalCount => _totalCount;

  bool get hasStudents => _students.isNotEmpty;

  bool get hasPreviousPage => _page > 1;

  bool get hasNextPage => _page < _totalPages;

  bool get hasSearch => _search.trim().isNotEmpty;

  bool get hasActiveFilter =>
      _statusFilter != StudentsStatusFilter.all;

  Future<void> loadInitial() {
    return _loadPage(
      page: 1,
      showLoading: true,
    );
  }

  void updateSearch(String value) {
    final normalized = value.trim();

    if (_search == normalized) {
      return;
    }

    _search = normalized;
    _searchTimer?.cancel();

    _searchTimer = Timer(
      _searchDebounce,
          () {
        _loadPage(
          page: 1,
          showLoading: true,
        );
      },
    );
  }

  Future<void> updateStatusFilter(
      StudentsStatusFilter filter,
      ) async {
    if (_statusFilter == filter) {
      return;
    }

    _statusFilter = filter;

    await _loadPage(
      page: 1,
      showLoading: true,
    );
  }

  Future<void> goToPreviousPage() async {
    if (!hasPreviousPage || _isLoading) {
      return;
    }

    await _loadPage(
      page: _page - 1,
      showLoading: true,
    );
  }

  Future<void> goToNextPage() async {
    if (!hasNextPage || _isLoading) {
      return;
    }

    await _loadPage(
      page: _page + 1,
      showLoading: true,
    );
  }

  Future<void> retry() {
    return _loadPage(
      page: _page,
      showLoading: true,
    );
  }

  Future<void> refresh() {
    return _loadPage(
      page: _page,
      showLoading: false,
    );
  }

  Future<void> _loadPage({
    required int page,
    required bool showLoading,
  }) async {
    final requestVersion = ++_requestVersion;

    if (showLoading) {
      _isLoading = true;
    }

    _errorMessage = null;
    _notifySafely();

    try {
      final response = await _studentsService.getStudents(
        search: _search,
        isActive: _isActiveValue,
        page: page,
        pageSize: pageSize,
      );

      if (_isDisposed || requestVersion != _requestVersion) {
        return;
      }

      _students
        ..clear()
        ..addAll(response.items);

      _page = response.page;
      _totalPages = response.totalPages;
      _totalCount = response.totalCount;
    } on ApiException catch (exception) {
      if (_isDisposed || requestVersion != _requestVersion) {
        return;
      }

      _errorMessage = _messageFromApiException(exception);
    } on TimeoutException {
      if (_isDisposed || requestVersion != _requestVersion) {
        return;
      }

      _errorMessage =
      'A consulta demorou mais que o esperado. Tente novamente.';
    } on http.ClientException {
      if (_isDisposed || requestVersion != _requestVersion) {
        return;
      }

      _errorMessage =
      'Não foi possível conectar ao servidor. Verifique sua conexão.';
    } on FormatException {
      if (_isDisposed || requestVersion != _requestVersion) {
        return;
      }

      _errorMessage =
      'Não foi possível carregar os alunos. Tente novamente.';
    } catch (_) {
      if (_isDisposed || requestVersion != _requestVersion) {
        return;
      }

      _errorMessage =
      'Não foi possível carregar os alunos. Tente novamente.';
    } finally {
      if (!_isDisposed && requestVersion == _requestVersion) {
        _isLoading = false;
        notifyListeners();
      }
    }
  }

  bool? get _isActiveValue {
    return switch (_statusFilter) {
      StudentsStatusFilter.all => null,
      StudentsStatusFilter.active => true,
      StudentsStatusFilter.inactive => false,
    };
  }

  String _messageFromApiException(ApiException exception) {
    if (exception.statusCode == 403) {
      return 'Você não possui permissão para acessar os alunos.';
    }

    if (exception.statusCode >= 500) {
      return 'O GymFlow está temporariamente indisponível. Tente novamente.';
    }

    return 'Não foi possível carregar os alunos. Tente novamente.';
  }

  void _notifySafely() {
    if (!_isDisposed) {
      notifyListeners();
    }
  }

  @override
  void dispose() {
    _isDisposed = true;
    _searchTimer?.cancel();

    super.dispose();
  }
}
