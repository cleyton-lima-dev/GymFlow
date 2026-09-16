import 'dart:async';
import 'package:avelri_gestao/features/enrollments/models/enrollment_summary.dart';
import 'package:avelri_gestao/core/network/api_exception.dart';
import 'package:avelri_gestao/features/enrollments/data/enrollments_service.dart';
import 'package:avelri_gestao/features/plans/data/plans_service.dart';
import 'package:avelri_gestao/features/plans/models/plan_summary.dart';
import 'package:avelri_gestao/features/students/data/students_service.dart';
import 'package:avelri_gestao/features/students/models/student_summary.dart';
import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;

class CreateEnrollmentViewModel extends ChangeNotifier {
  CreateEnrollmentViewModel({
    required this._enrollmentsService,
    required this._studentsService,
    required this._plansService,
  });
  final EnrollmentsService _enrollmentsService;
  final StudentsService _studentsService;
  final PlansService _plansService;

  final List<StudentSummary> _students = [];
  final List<PlanSummary> _plans = [];

  bool _isLoading = false;
  bool _isSubmitting = false;
  bool _isDisposed = false;

  String? _errorMessage;
  String _studentSearch = '';

  List<StudentSummary> get students =>
      List.unmodifiable(_students);

  List<PlanSummary> get plans =>
      List.unmodifiable(_plans);

  List<StudentSummary> get filteredStudents {
    final search = _studentSearch
        .trim()
        .toLowerCase();

    if (search.isEmpty) {
      return students;
    }

    return _students
        .where(
          (student) =>
      student.name.toLowerCase().contains(search) ||
          student.email.toLowerCase().contains(search),
    )
        .toList();
  }

  bool get isLoading => _isLoading;
  bool get isSubmitting => _isSubmitting;
  String? get errorMessage => _errorMessage;

  Future<void> loadInitial() async {
    _isLoading = true;
    _errorMessage = null;
    _notifySafely();

    try {
      final students = await _loadActiveStudents();

      final activeEnrollments =
      await _enrollmentsService.getEnrollments(
        status: EnrollmentStatus.active,
      );

      final enrolledStudentIds = activeEnrollments
          .map((enrollment) => enrollment.studentId)
          .toSet();

      final availableStudents = students
          .where(
            (student) =>
        !enrolledStudentIds.contains(student.id),
      )
          .toList();

      final plans = await _plansService.getPlans(
        isActive: true,
      );

      if (_isDisposed) {
        return;
      }

      _students
        ..clear()
        ..addAll(availableStudents);

      _plans
        ..clear()
        ..addAll(plans);
    } on ApiException catch (exception) {
      if (_isDisposed) {
        return;
      }

      _errorMessage =
          _messageFromApiException(exception);
    } on TimeoutException {
      if (_isDisposed) {
        return;
      }

      _errorMessage =
      'A consulta demorou mais que o esperado. Tente novamente.';
    } on http.ClientException {
      if (_isDisposed) {
        return;
      }

      _errorMessage =
      'Não foi possível conectar ao servidor. Verifique sua conexão.';
    } catch (_) {
      if (_isDisposed) {
        return;
      }

      _errorMessage =
      'Não foi possível carregar os dados da matrícula.';
    } finally {
      if (!_isDisposed) {
        _isLoading = false;
        notifyListeners();
      }
    }
  }
  void updateStudentSearch(String value) {
    _studentSearch = value;
    _notifySafely();
  }

  Future<bool> createEnrollment({
    required String studentId,
    required String planId,
    required DateTime startDate,
  }) async {
    if (_isSubmitting) {
      return false;
    }

    _isSubmitting = true;
    _errorMessage = null;
    _notifySafely();

    try {
      await _enrollmentsService.createEnrollment(
        studentId: studentId,
        planId: planId,
        startDate: startDate,
      );

      return true;
    } on ApiException catch (exception) {
      _errorMessage =
          _messageFromApiException(exception);

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
      _errorMessage =
      'Não foi possível criar a matrícula. Tente novamente.';

      return false;
    } finally {
      if (!_isDisposed) {
        _isSubmitting = false;
        notifyListeners();
      }
    }
  }

  void clearError() {
    if (_errorMessage == null) {
      return;
    }

    _errorMessage = null;
    _notifySafely();
  }

  Future<List<StudentSummary>>
  _loadActiveStudents() async {
    const pageSize = 100;

    final students = <StudentSummary>[];
    var page = 1;
    var totalPages = 1;

    do {
      final response =
      await _studentsService.getStudents(
        isActive: true,
        page: page,
        pageSize: pageSize,
      );

      students.addAll(response.items);
      totalPages = response.totalPages;
      page++;
    } while (page <= totalPages);

    return students;
  }

  String _messageFromApiException(
      ApiException exception,
      ) {
    if (exception.statusCode == 409) {
      return 'O aluno já possui uma matrícula que conflita com este período.';
    }

    if (exception.statusCode == 403) {
      return 'Você não possui permissão para gerenciar matrículas.';
    }

    if (exception.statusCode >= 500) {
      return 'O Avelri está temporariamente indisponível. Tente novamente.';
    }

    return 'Não foi possível realizar a operação.';
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
