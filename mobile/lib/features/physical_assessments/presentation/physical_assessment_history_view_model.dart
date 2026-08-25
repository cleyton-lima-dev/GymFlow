import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:gymflow/core/network/api_exception.dart';
import 'package:gymflow/features/physical_assessments/data/physical_assessments_service.dart';
import 'package:gymflow/features/physical_assessments/models/paged_physical_assessments_response.dart';
import 'package:gymflow/features/physical_assessments/models/physical_assessment.dart';
import 'package:gymflow/features/physical_assessments/models/physical_assessment_history_item.dart';
import 'package:http/http.dart' as http;

class PhysicalAssessmentHistoryViewModel extends ChangeNotifier {
  PhysicalAssessmentHistoryViewModel(
      this._service,
      this._studentId,
      );

  static const int _pageSize = 20;

  final PhysicalAssessmentsService _service;
  final String _studentId;

  List<PhysicalAssessmentHistoryItem> _items = [];
  PhysicalAssessment? _latest;

  bool _isLoading = false;
  bool _isChangingPage = false;

  String? _errorMessage;

  int _page = 1;
  int _totalPages = 1;
  int _totalCount = 0;

  List<PhysicalAssessmentHistoryItem> get items => _items;
  PhysicalAssessment? get latest => _latest;

  bool get isLoading => _isLoading;
  bool get isChangingPage => _isChangingPage;

  String? get errorMessage => _errorMessage;

  int get page => _page;
  int get totalPages => _totalPages;
  int get totalCount => _totalCount;

  bool get hasPreviousPage => _page > 1;
  bool get hasNextPage => _page < _totalPages;

  Future<void> loadInitial() async {
    if (_isLoading) {
      return;
    }

    _isLoading = true;
    _errorMessage = null;
    notifyListeners();

    try {
      _latest = await _service.getLatest(_studentId);

      final response = await _service.getHistory(
        studentId: _studentId,
        page: 1,
        pageSize: _pageSize,
      );

      _applyResponse(response);
    } on ApiException catch (exception) {
      _errorMessage = _messageFromApiException(exception);
    } on TimeoutException {
      _errorMessage =
      'O histórico demorou mais que o esperado para carregar.';
    } on http.ClientException {
      _errorMessage =
      'Não foi possível conectar ao servidor.';
    } on FormatException {
      _errorMessage =
      'Não foi possível interpretar o histórico de avaliações.';
    } catch (_) {
      _errorMessage =
      'Não foi possível carregar o histórico de avaliações.';
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }

  Future<void> refresh() async {
    await _loadPage(_page);
  }

  Future<void> previousPage() async {
    if (!hasPreviousPage || _isChangingPage) {
      return;
    }

    await _loadPage(_page - 1);
  }

  Future<void> nextPage() async {
    if (!hasNextPage || _isChangingPage) {
      return;
    }

    await _loadPage(_page + 1);
  }

  Future<void> _loadPage(int requestedPage) async {
    _isChangingPage = true;
    _errorMessage = null;
    notifyListeners();

    try {
      _latest = await _service.getLatest(_studentId);

      final response = await _service.getHistory(
        studentId: _studentId,
        page: requestedPage,
        pageSize: _pageSize,
      );

      _applyResponse(response);
    } on ApiException catch (exception) {
      _errorMessage = _messageFromApiException(exception);
    } on TimeoutException {
      _errorMessage =
      'O histórico demorou mais que o esperado para carregar.';
    } on http.ClientException {
      _errorMessage =
      'Não foi possível conectar ao servidor.';
    } on FormatException {
      _errorMessage =
      'Não foi possível interpretar o histórico de avaliações.';
    } catch (_) {
      _errorMessage =
      'Não foi possível carregar o histórico de avaliações.';
    } finally {
      _isChangingPage = false;
      notifyListeners();
    }
  }

  void _applyResponse(
      PagedPhysicalAssessmentsResponse response,
      ) {
    _items = response.items;
    _page = response.page;
    _totalPages = response.totalPages;
    _totalCount = response.totalCount;
  }

  String _messageFromApiException(ApiException exception) {
    if (exception.statusCode == 403) {
      return 'Você não possui permissão para acessar '
          'o histórico de avaliações.';
    }

    if (exception.statusCode >= 500) {
      return 'O histórico de avaliações está '
          'temporariamente indisponível.';
    }

    return 'Não foi possível carregar o histórico de avaliações.';
  }
}
