import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:gymflow/core/network/api_exception.dart';
import 'package:gymflow/features/workout_templates/data/workout_templates_service.dart';
import 'package:gymflow/features/workout_templates/models/workout_template_summary.dart';
import 'package:http/http.dart' as http;
import 'package:gymflow/features/workout_templates/models/workout_template_details.dart';

class SelectWorkoutTemplateViewModel extends ChangeNotifier {
  SelectWorkoutTemplateViewModel(this._templatesService);

  final WorkoutTemplatesService _templatesService;

  final List<WorkoutTemplateSummary> _items = [];

  Timer? _searchDebounce;

  int _requestVersion = 0;
  bool _isDisposed = false;

  String _search = '';

  int _page = 1;
  int _totalPages = 1;
  int _totalCount = 0;

  bool _isLoading = false;
  bool _isLoadingMore = false;
  bool _isSubmitting = false;

  String? _submittingTemplateId;
  String? _errorMessage;

  List<WorkoutTemplateSummary> get items => List.unmodifiable(_items);

  int get totalCount => _totalCount;

  bool get isLoading => _isLoading;
  bool get isLoadingMore => _isLoadingMore;
  bool get isSubmitting => _isSubmitting;

  String? get submittingTemplateId => _submittingTemplateId;

  String? get errorMessage => _errorMessage;

  bool get hasNextPage => _page < _totalPages;

  Future<void> loadInitial() {
    return _loadInitial(++_requestVersion);
  }

  Future<void> _loadInitial(int requestVersion) async {
    if (_isStale(requestVersion)) {
      return;
    }

    final search = _search;

    _isLoading = true;
    _isLoadingMore = false;
    _errorMessage = null;
    _notifySafely();

    try {
      final response = await _templatesService.getTemplates(
        search: search,
        isActive: true,
        page: 1,
        pageSize: 20,
      );

      if (_isStale(requestVersion)) {
        return;
      }

      _items
        ..clear()
        ..addAll(response.items);

      _page = response.page;
      _totalPages = response.totalPages;
      _totalCount = response.totalCount;
    } on ApiException catch (exception) {
      if (_isStale(requestVersion)) {
        return;
      }

      _errorMessage = _messageFromApiException(exception);
    } on TimeoutException {
      if (_isStale(requestVersion)) {
        return;
      }

      _errorMessage = 'A busca demorou mais que o esperado.';
    } on http.ClientException {
      if (_isStale(requestVersion)) {
        return;
      }

      _errorMessage = 'Não foi possível conectar ao servidor.';
    } on FormatException {
      if (_isStale(requestVersion)) {
        return;
      }

      _errorMessage = 'Não foi possível interpretar os modelos.';
    } catch (_) {
      if (_isStale(requestVersion)) {
        return;
      }

      _errorMessage = 'Não foi possível carregar os modelos de treino.';
    } finally {
      if (!_isStale(requestVersion)) {
        _isLoading = false;
        _isLoadingMore = false;
        notifyListeners();
      }
    }
  }

  void setSearch(String value) {
    final normalized = value.trim();

    if (_search == normalized) {
      return;
    }

    _search = normalized;

    _searchDebounce?.cancel();

    final requestVersion = ++_requestVersion;

    _searchDebounce = Timer(const Duration(milliseconds: 400), () {
      _loadInitial(requestVersion);
    });
  }

  Future<void> refresh() async {
    await loadInitial();
  }

  Future<void> loadMore() async {
    if (_isLoading || _isLoadingMore || !hasNextPage) {
      return;
    }

    final requestVersion = _requestVersion;
    final search = _search;
    final nextPage = _page + 1;

    _isLoadingMore = true;
    _errorMessage = null;
    _notifySafely();

    try {
      final response = await _templatesService.getTemplates(
        search: search,
        isActive: true,
        page: nextPage,
        pageSize: 20,
      );

      if (_isStale(requestVersion)) {
        return;
      }

      _items.addAll(response.items);

      _page = response.page;
      _totalPages = response.totalPages;
      _totalCount = response.totalCount;
    } on ApiException catch (exception) {
      if (_isStale(requestVersion)) {
        return;
      }

      _errorMessage = _messageFromApiException(exception);
    } on TimeoutException {
      if (_isStale(requestVersion)) {
        return;
      }

      _errorMessage = 'A busca demorou mais que o esperado.';
    } on http.ClientException {
      if (_isStale(requestVersion)) {
        return;
      }

      _errorMessage = 'Não foi possível conectar ao servidor.';
    } on FormatException {
      if (_isStale(requestVersion)) {
        return;
      }

      _errorMessage = 'Não foi possível interpretar os modelos.';
    } catch (_) {
      if (_isStale(requestVersion)) {
        return;
      }

      _errorMessage = 'Não foi possível carregar mais modelos.';
    } finally {
      if (!_isStale(requestVersion)) {
        _isLoadingMore = false;
        notifyListeners();
      }
    }
  }

  Future<WorkoutTemplateDetails?> selectTemplate(
    WorkoutTemplateSummary template,
  ) async {
    if (_isSubmitting) {
      return null;
    }

    _isSubmitting = true;
    _submittingTemplateId = template.id;
    _errorMessage = null;
    notifyListeners();

    try {
      return await _templatesService.getById(template.id);
    } on ApiException catch (exception) {
      _errorMessage = _messageFromApiException(exception);

      return null;
    } on TimeoutException {
      _errorMessage =
          'O servidor demorou mais que o esperado para carregar o modelo.';

      return null;
    } on http.ClientException {
      _errorMessage = 'Não foi possível conectar ao servidor.';

      return null;
    } on FormatException {
      _errorMessage = 'Não foi possível interpretar os dados do modelo.';

      return null;
    } catch (_) {
      _errorMessage = 'Não foi possível carregar este modelo de treino.';

      return null;
    } finally {
      _isSubmitting = false;
      _submittingTemplateId = null;
      notifyListeners();
    }
  }

  bool _isStale(int requestVersion) {
    return _isDisposed || requestVersion != _requestVersion;
  }

  void _notifySafely() {
    if (!_isDisposed) {
      notifyListeners();
    }
  }

  String _messageFromApiException(ApiException exception) {
    if (exception.statusCode == 400) {
      return 'Não foi possível carregar os modelos de treino.';
    }

    if (exception.statusCode == 403) {
      return 'Você não possui permissão para visualizar os modelos de treino.';
    }

    if (exception.statusCode >= 500) {
      return 'O serviço de modelos de treino está temporariamente indisponível.';
    }

    return 'Não foi possível carregar os modelos de treino.';
  }

  @override
  void dispose() {
    _isDisposed = true;
    ++_requestVersion;

    _searchDebounce?.cancel();

    super.dispose();
  }
}
