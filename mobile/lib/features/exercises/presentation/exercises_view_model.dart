import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:gymflow/core/network/api_exception.dart';
import 'package:gymflow/features/exercises/data/exercises_service.dart';
import 'package:gymflow/features/exercises/models/exercise_summary.dart';
import 'package:http/http.dart' as http;

enum ExerciseStatusFilter {
  all,
  active,
  inactive,
}

class ExercisesViewModel extends ChangeNotifier {
  ExercisesViewModel(this._service);

  static const int _pageSize = 20;

  final ExercisesService _service;

  final List<ExerciseSummary> _items = [];

  Timer? _searchDebounce;

  bool _isLoading = false;
  bool _isChangingPage = false;

  String? _errorMessage;

  String _search = '';
  String? _muscleGroup;
  ExerciseStatusFilter _statusFilter =
      ExerciseStatusFilter.all;

  int _page = 1;
  int _totalPages = 1;
  int _totalCount = 0;

  int _requestVersion = 0;

  List<ExerciseSummary> get items =>
      List.unmodifiable(_items);

  bool get isLoading => _isLoading;
  bool get isChangingPage => _isChangingPage;

  String? get errorMessage => _errorMessage;

  String get search => _search;
  String? get muscleGroup => _muscleGroup;

  ExerciseStatusFilter get statusFilter =>
      _statusFilter;

  int get page => _page;
  int get totalPages => _totalPages;
  int get totalCount => _totalCount;

  bool get hasPreviousPage => _page > 1;
  bool get hasNextPage => _page < _totalPages;

  Future<void> loadInitial() async {
    await _loadPage(
      requestedPage: 1,
      showInitialLoading: true,
    );
  }

  Future<void> refresh() async {
    await _loadPage(
      requestedPage: _page,
      showInitialLoading: false,
    );
  }

  void setSearch(String value) {
    _search = value;

    _searchDebounce?.cancel();

    _searchDebounce = Timer(
      const Duration(milliseconds: 400),
          () {
        _loadPage(
          requestedPage: 1,
          showInitialLoading: false,
        );
      },
    );
  }

  Future<void> setMuscleGroup(
      String? muscleGroup,
      ) async {
    final normalized = muscleGroup?.trim();

    final newValue =
    normalized == null || normalized.isEmpty
        ? null
        : normalized;

    if (_muscleGroup == newValue) {
      return;
    }

    _muscleGroup = newValue;

    await _loadPage(
      requestedPage: 1,
      showInitialLoading: false,
    );
  }

  Future<void> setStatusFilter(
      ExerciseStatusFilter filter,
      ) async {
    if (_statusFilter == filter) {
      return;
    }

    _statusFilter = filter;

    await _loadPage(
      requestedPage: 1,
      showInitialLoading: false,
    );
  }

  Future<void> previousPage() async {
    if (!hasPreviousPage || _isChangingPage) {
      return;
    }

    await _loadPage(
      requestedPage: _page - 1,
      showInitialLoading: false,
      changingPage: true,
    );
  }

  Future<void> nextPage() async {
    if (!hasNextPage || _isChangingPage) {
      return;
    }

    await _loadPage(
      requestedPage: _page + 1,
      showInitialLoading: false,
      changingPage: true,
    );
  }

  Future<void> _loadPage({
    required int requestedPage,
    required bool showInitialLoading,
    bool changingPage = false,
  }) async {
    final requestVersion = ++_requestVersion;

    _errorMessage = null;

    if (showInitialLoading) {
      _isLoading = true;
    }

    if (changingPage) {
      _isChangingPage = true;
    }

    notifyListeners();

    try {
      final response = await _service.getExercises(
        search: _search,
        muscleGroup: _muscleGroup,
        isActive: _statusAsBool(),
        page: requestedPage,
        pageSize: _pageSize,
      );

      if (requestVersion != _requestVersion) {
        return;
      }

      _items
        ..clear()
        ..addAll(response.items);

      _page = response.page;
      _totalPages = response.totalPages;
      _totalCount = response.totalCount;
    } on ApiException catch (exception) {
      if (requestVersion != _requestVersion) {
        return;
      }

      _errorMessage =
          _messageFromApiException(exception);
    } on TimeoutException {
      if (requestVersion != _requestVersion) {
        return;
      }

      _errorMessage =
      'A busca demorou mais que o esperado.';
    } on http.ClientException {
      if (requestVersion != _requestVersion) {
        return;
      }

      _errorMessage =
      'Não foi possível conectar ao servidor.';
    } on FormatException {
      if (requestVersion != _requestVersion) {
        return;
      }

      _errorMessage =
      'Não foi possível interpretar os exercícios.';
    } catch (_) {
      if (requestVersion != _requestVersion) {
        return;
      }

      _errorMessage =
      'Não foi possível carregar os exercícios.';
    } finally {
      if (requestVersion == _requestVersion) {
        _isLoading = false;
        _isChangingPage = false;
        notifyListeners();
      }
    }
  }

  bool? _statusAsBool() {
    return switch (_statusFilter) {
      ExerciseStatusFilter.all => null,
      ExerciseStatusFilter.active => true,
      ExerciseStatusFilter.inactive => false,
    };
  }

  String _messageFromApiException(
      ApiException exception,
      ) {
    if (exception.statusCode == 403) {
      return 'Você não possui permissão para acessar '
          'o banco de exercícios.';
    }

    if (exception.statusCode >= 500) {
      return 'O banco de exercícios está '
          'temporariamente indisponível.';
    }

    return 'Não foi possível carregar os exercícios.';
  }

  @override
  void dispose() {
    _searchDebounce?.cancel();
    super.dispose();
  }
}
