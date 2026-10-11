import 'dart:async';

import 'package:avelri_gestao/core/network/api_exception.dart';
import 'package:avelri_gestao/features/check_ins/data/physical_access_events_service.dart';
import 'package:avelri_gestao/features/check_ins/models/physical_access_event.dart';
import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;

class PhysicalAccessEventsViewModel extends ChangeNotifier {
  PhysicalAccessEventsViewModel(this._service)
    : _fromDate = DateTime.now().subtract(const Duration(days: 30)),
      _toDate = DateTime.now();

  static const int pageSize = 20;

  final PhysicalAccessEventsService _service;

  final List<PhysicalAccessEvent> _items = [];

  bool _isLoading = false;
  bool _isDisposed = false;

  String? _errorMessage;
  String? _studentId;
  String? _studentName;

  int? _decision;
  int? _source;

  DateTime? _fromDate;
  DateTime? _toDate;

  int _page = 1;
  int _totalPages = 1;
  int _totalCount = 0;
  int _requestVersion = 0;

  List<PhysicalAccessEvent> get items => List.unmodifiable(_items);

  bool get isLoading => _isLoading;
  bool get hasItems => _items.isNotEmpty;
  String? get errorMessage => _errorMessage;

  String? get studentId => _studentId;
  String? get studentName => _studentName;
  bool get hasStudentFilter => _studentId != null;

  int? get decision => _decision;
  int? get source => _source;

  DateTime? get fromDate => _fromDate;
  DateTime? get toDate => _toDate;
  bool get hasDateFilter => _fromDate != null || _toDate != null;

  int get page => _page;
  int get totalPages => _totalPages;
  int get totalCount => _totalCount;

  bool get hasPreviousPage => _page > 1;
  bool get hasNextPage => _page < _totalPages;

  Future<void> loadInitial() => _loadPage(page: 1, showLoading: true);

  Future<void> updateStudentFilter({
    required String studentId,
    required String studentName,
  }) async {
    _studentId = studentId;
    _studentName = studentName;
    await loadInitial();
  }

  Future<void> clearStudentFilter() async {
    _studentId = null;
    _studentName = null;
    await loadInitial();
  }

  Future<void> updateDecision(int? value) async {
    _decision = value;
    await loadInitial();
  }

  Future<void> updateSource(int? value) async {
    _source = value;
    await loadInitial();
  }

  Future<void> updatePeriod({DateTime? fromDate, DateTime? toDate}) async {
    if (fromDate != null && toDate != null && fromDate.isAfter(toDate)) {
      throw ArgumentError(
        'A data inicial não pode ser posterior à data final.',
      );
    }

    _fromDate = fromDate;
    _toDate = toDate;
    await loadInitial();
  }

  Future<void> clearPeriod() async {
    _fromDate = null;
    _toDate = null;
    await loadInitial();
  }

  Future<void> goToPreviousPage() async {
    if (!hasPreviousPage || _isLoading) return;
    await _loadPage(page: _page - 1, showLoading: true);
  }

  Future<void> goToNextPage() async {
    if (!hasNextPage || _isLoading) return;
    await _loadPage(page: _page + 1, showLoading: true);
  }

  Future<void> retry() => _loadPage(page: _page, showLoading: true);

  Future<void> refresh() => _loadPage(page: _page, showLoading: false);

  Future<void> _loadPage({required int page, required bool showLoading}) async {
    final requestVersion = ++_requestVersion;

    if (showLoading) _isLoading = true;

    _errorMessage = null;
    _notifySafely();

    try {
      final response = await _service.getHistory(
        studentId: _studentId,
        decision: _decision,
        source: _source,
        fromDate: _fromDate,
        toDate: _toDate,
        page: page,
        pageSize: pageSize,
      );

      if (_isDisposed || requestVersion != _requestVersion) return;

      _items
        ..clear()
        ..addAll(response.items);

      _page = response.page;
      _totalPages = response.totalPages == 0 ? 1 : response.totalPages;
      _totalCount = response.totalCount;
    } on ApiException catch (exception) {
      if (_isDisposed || requestVersion != _requestVersion) return;

      if (exception.statusCode == 403) {
        _errorMessage =
            'Você não possui permissão para consultar eventos de acesso.';
      } else if (exception.statusCode >= 500) {
        _errorMessage =
            'O Avelri está temporariamente indisponível. Tente novamente.';
      } else {
        _errorMessage = 'Não foi possível carregar os eventos de acesso.';
      }
    } on TimeoutException {
      if (_isDisposed || requestVersion != _requestVersion) return;

      _errorMessage =
          'A consulta demorou mais que o esperado. Tente novamente.';
    } on http.ClientException {
      if (_isDisposed || requestVersion != _requestVersion) return;

      _errorMessage =
          'Não foi possível conectar ao servidor. Verifique sua conexão.';
    } on FormatException {
      if (_isDisposed || requestVersion != _requestVersion) return;

      _errorMessage = 'O histórico de eventos retornou em formato inválido.';
    } catch (_) {
      if (_isDisposed || requestVersion != _requestVersion) return;

      _errorMessage = 'Não foi possível carregar os eventos de acesso.';
    } finally {
      if (!_isDisposed && requestVersion == _requestVersion) {
        _isLoading = false;
        notifyListeners();
      }
    }
  }

  void _notifySafely() {
    if (!_isDisposed) notifyListeners();
  }

  @override
  void dispose() {
    _isDisposed = true;
    super.dispose();
  }
}
