import 'dart:async';

import 'package:avelri_gestao/core/network/api_exception.dart';
import 'package:avelri_gestao/features/check_ins/data/check_ins_service.dart';
import 'package:avelri_gestao/features/check_ins/models/check_in_history_item.dart';
import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;

class CheckInsViewModel extends ChangeNotifier {
  CheckInsViewModel(this._service)
    : _fromDate = DateTime.now().subtract(const Duration(days: 30)),
      _toDate = DateTime.now();

  static const int pageSize = 20;

  final CheckInsService _service;

  final List<CheckInHistoryItem> _items = [];

  bool _isLoading = false;
  bool _isDisposed = false;

  String? _errorMessage;

  String? _studentId;
  String? _studentName;

  DateTime? _fromDate;
  DateTime? _toDate;

  int _page = 1;
  int _totalPages = 1;
  int _totalCount = 0;

  int _requestVersion = 0;

  List<CheckInHistoryItem> get items => List.unmodifiable(_items);

  bool get isLoading => _isLoading;

  bool get hasItems => _items.isNotEmpty;

  String? get errorMessage => _errorMessage;

  String? get studentId => _studentId;

  String? get studentName => _studentName;

  bool get hasStudentFilter => _studentId != null;

  DateTime? get fromDate => _fromDate;

  DateTime? get toDate => _toDate;

  int get page => _page;

  int get totalPages => _totalPages;

  int get totalCount => _totalCount;

  bool get hasPreviousPage => _page > 1;

  bool get hasNextPage => _page < _totalPages;

  bool get hasDateFilter => _fromDate != null || _toDate != null;

  Future<void> loadInitial() {
    return _loadPage(page: 1, showLoading: true);
  }

  Future<void> updateStudentFilter({
    required String studentId,
    required String studentName,
  }) async {
    _studentId = studentId;
    _studentName = studentName;

    await _loadPage(page: 1, showLoading: true);
  }

  Future<void> clearStudentFilter() async {
    _studentId = null;
    _studentName = null;

    await _loadPage(page: 1, showLoading: true);
  }

  Future<void> updatePeriod({DateTime? fromDate, DateTime? toDate}) async {
    if (fromDate != null && toDate != null && fromDate.isAfter(toDate)) {
      throw ArgumentError(
        'A data inicial não pode ser posterior à data final.',
      );
    }

    _fromDate = fromDate;
    _toDate = toDate;

    await _loadPage(page: 1, showLoading: true);
  }

  Future<void> clearPeriod() async {
    _fromDate = null;
    _toDate = null;

    await _loadPage(page: 1, showLoading: true);
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
      final response = await _service.getHistory(
        studentId: _studentId,
        fromDate: _fromDate,
        toDate: _toDate,
        page: page,
        pageSize: pageSize,
      );

      if (_isDisposed || requestVersion != _requestVersion) {
        return;
      }

      _items
        ..clear()
        ..addAll(response.items);

      _page = response.page;

      _totalPages = response.totalPages == 0 ? 1 : response.totalPages;

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

      _errorMessage = 'O histórico retornou em formato inválido.';
    } catch (_) {
      if (_isDisposed || requestVersion != _requestVersion) {
        return;
      }

      _errorMessage = 'Não foi possível carregar o histórico de frequência.';
    } finally {
      if (!_isDisposed && requestVersion == _requestVersion) {
        _isLoading = false;
        notifyListeners();
      }
    }
  }

  String _messageFromApiException(ApiException exception) {
    if (exception.statusCode == 403) {
      return 'Você não possui permissão para acessar o histórico de frequência.';
    }

    if (exception.statusCode >= 500) {
      return 'O Avelri está temporariamente indisponível. Tente novamente.';
    }

    return 'Não foi possível carregar o histórico de frequência.';
  }

  void _notifySafely() {
    if (!_isDisposed) {
      notifyListeners();
    }
  }

  @override
  void dispose() {
    _isDisposed = true;

    super.dispose();
  }
}
