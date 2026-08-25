import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:gymflow/core/network/api_exception.dart';
import 'package:gymflow/features/workout_templates/data/workout_templates_service.dart';
import 'package:gymflow/features/workout_templates/models/workout_template_summary.dart';
import 'package:http/http.dart' as http;
import 'package:gymflow/features/workout_templates/models/workout_template_details.dart';

class SelectWorkoutTemplateViewModel
    extends ChangeNotifier {
  SelectWorkoutTemplateViewModel(
      this._templatesService,
      );

  final WorkoutTemplatesService _templatesService;

  final List<WorkoutTemplateSummary> _items = [];

  Timer? _searchDebounce;

  String _search = '';

  int _page = 1;
  int _totalPages = 1;
  int _totalCount = 0;

  bool _isLoading = false;
  bool _isLoadingMore = false;
  bool _isSubmitting = false;

  String? _submittingTemplateId;
  String? _errorMessage;

  List<WorkoutTemplateSummary> get items =>
      List.unmodifiable(_items);

  int get totalCount => _totalCount;

  bool get isLoading => _isLoading;
  bool get isLoadingMore => _isLoadingMore;
  bool get isSubmitting => _isSubmitting;

  String? get submittingTemplateId =>
      _submittingTemplateId;

  String? get errorMessage => _errorMessage;

  bool get hasNextPage => _page < _totalPages;

  Future<void> loadInitial() async {
    if (_isLoading) {
      return;
    }

    _isLoading = true;
    _errorMessage = null;
    notifyListeners();

    try {
      final response =
      await _templatesService.getTemplates(
        search: _search,
        isActive: true,
        page: 1,
        pageSize: 20,
      );

      _items
        ..clear()
        ..addAll(response.items);

      _page = response.page;
      _totalPages = response.totalPages;
      _totalCount = response.totalCount;
    } on ApiException catch (exception) {
      _errorMessage =
          _messageFromApiException(exception);
    } on TimeoutException {
      _errorMessage =
      'A busca demorou mais que o esperado.';
    } on http.ClientException {
      _errorMessage =
      'Não foi possível conectar ao servidor.';
    } on FormatException {
      _errorMessage =
      'Não foi possível interpretar os modelos.';
    } catch (_) {
      _errorMessage =
      'Não foi possível carregar os modelos de treino.';
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }

  void setSearch(String value) {
    _search = value.trim();

    _searchDebounce?.cancel();

    _searchDebounce = Timer(
      const Duration(milliseconds: 400),
      loadInitial,
    );
  }

  Future<void> refresh() async {
    await loadInitial();
  }

  Future<void> loadMore() async {
    if (_isLoading ||
        _isLoadingMore ||
        !hasNextPage) {
      return;
    }

    _isLoadingMore = true;
    _errorMessage = null;
    notifyListeners();

    try {
      final response =
      await _templatesService.getTemplates(
        search: _search,
        isActive: true,
        page: _page + 1,
        pageSize: 20,
      );

      _items.addAll(response.items);

      _page = response.page;
      _totalPages = response.totalPages;
      _totalCount = response.totalCount;
    } on ApiException catch (exception) {
      _errorMessage =
          _messageFromApiException(exception);
    } on TimeoutException {
      _errorMessage =
      'A busca demorou mais que o esperado.';
    } on http.ClientException {
      _errorMessage =
      'Não foi possível conectar ao servidor.';
    } on FormatException {
      _errorMessage =
      'Não foi possível interpretar os modelos.';
    } catch (_) {
      _errorMessage =
      'Não foi possível carregar mais modelos.';
    } finally {
      _isLoadingMore = false;
      notifyListeners();
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
      return await _templatesService.getById(
        template.id,
      );
    } on ApiException catch (exception) {
      _errorMessage =
          _messageFromApiException(exception);

      return null;
    } on TimeoutException {
      _errorMessage =
      'O servidor demorou mais que o esperado para carregar o modelo.';

      return null;
    } on http.ClientException {
      _errorMessage =
      'Não foi possível conectar ao servidor.';

      return null;
    } on FormatException {
      _errorMessage =
      'Não foi possível interpretar os dados do modelo.';

      return null;
    } catch (_) {
      _errorMessage =
      'Não foi possível carregar este modelo de treino.';

      return null;
    } finally {
      _isSubmitting = false;
      _submittingTemplateId = null;
      notifyListeners();
    }
  }

  String _messageFromApiException(
      ApiException exception,
      ) {
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
    _searchDebounce?.cancel();
    super.dispose();
  }
}
