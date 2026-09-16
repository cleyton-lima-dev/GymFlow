import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:avelri_gestao/core/network/api_exception.dart';
import 'package:avelri_gestao/features/students/data/students_service.dart';
import 'package:avelri_gestao/features/students/models/student_summary.dart';
import 'package:http/http.dart' as http;

enum StudentsStatusFilter { all, active, inactive }
enum StudentsEnrollmentFilter {
  all,
  active,
  cancelled,
  expired,
  none,
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
  StudentsEnrollmentFilter _enrollmentFilter =
      StudentsEnrollmentFilter.all;

  int _page = 1;
  int _totalPages = 1;
  int _totalCount = 0;

  int _requestVersion = 0;

  List<StudentSummary> get students => List.unmodifiable(_students);

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
      _statusFilter != StudentsStatusFilter.all ||
          _enrollmentFilter != StudentsEnrollmentFilter.all;

  StudentsEnrollmentFilter get enrollmentFilter =>
      _enrollmentFilter;

  Future<void> loadInitial() {
    return _loadPage(page: 1, showLoading: true);
  }

  void updateSearch(String value) {
    final normalized = value.trim();

    if (_search == normalized) {
      return;
    }

    _search = normalized;
    _searchTimer?.cancel();

    _searchTimer = Timer(_searchDebounce, () {
      _loadPage(page: 1, showLoading: true);
    });
  }

  Future<void> updateStatusFilter(StudentsStatusFilter filter) async {
    if (_statusFilter == filter) {
      return;
    }

    _statusFilter = filter;

    await _loadPage(page: 1, showLoading: true);
  }

  Future<void> updateFilters({
    required StudentsStatusFilter statusFilter,
    required StudentsEnrollmentFilter enrollmentFilter,
  }) async {
    if (_statusFilter == statusFilter &&
        _enrollmentFilter == enrollmentFilter) {
      return;
    }

    _statusFilter = statusFilter;
    _enrollmentFilter = enrollmentFilter;

    await _loadPage(
      page: 1,
      showLoading: true,
    );
  }

  Future<void> updateEnrollmentFilter(
      StudentsEnrollmentFilter filter,
      ) async {
    if (_enrollmentFilter == filter) {
      return;
    }

    _enrollmentFilter = filter;

    await _loadPage(
      page: 1,
      showLoading: true,
    );
  }

  Future<void> goToPreviousPage() async {
    if (!hasPreviousPage || _isLoading) {
      return;
    }

    await _loadPage(page: _page - 1, showLoading: true);
  }

  Future<void> goToNextPage() async {
    if (!hasNextPage || _isLoading) {
      return;
    }

    await _loadPage(page: _page + 1, showLoading: true);
  }

  Future<void> retry() {
    return _loadPage(page: _page, showLoading: true);
  }

  Future<void> refresh() {
    return _loadPage(page: _page, showLoading: false);
  }

  Future<void> _loadPage({required int page, required bool showLoading}) async {
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
        enrollmentFilter: _enrollmentFilterValue,
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
          'NÃ£o foi possÃ­vel conectar ao servidor. Verifique sua conexÃ£o.';
    } on FormatException {
      if (_isDisposed || requestVersion != _requestVersion) {
        return;
      }

      _errorMessage = 'NÃ£o foi possÃ­vel carregar os alunos. Tente novamente.';
    } catch (_) {
      if (_isDisposed || requestVersion != _requestVersion) {
        return;
      }

      _errorMessage = 'NÃ£o foi possÃ­vel carregar os alunos. Tente novamente.';
    } finally {
      if (!_isDisposed && requestVersion == _requestVersion) {
        _isLoading = false;
        notifyListeners();
      }
    }
  }
  int? get _enrollmentFilterValue {
    return switch (_enrollmentFilter) {
      StudentsEnrollmentFilter.all => null,
      StudentsEnrollmentFilter.active => 1,
      StudentsEnrollmentFilter.cancelled => 2,
      StudentsEnrollmentFilter.expired => 3,
      StudentsEnrollmentFilter.none => 4,
    };
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
      return 'VocÃª nÃ£o possui permissÃ£o para acessar os alunos.';
    }

    if (exception.statusCode >= 500) {
      return 'O Avelri estÃ¡ temporariamente indisponÃ­vel. Tente novamente.';
    }

    return 'NÃ£o foi possÃ­vel carregar os alunos. Tente novamente.';
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
