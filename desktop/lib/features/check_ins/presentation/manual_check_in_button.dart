import 'dart:async';

import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/core/network/api_exception.dart';
import 'package:avelri_gestao/features/check_ins/data/check_ins_service.dart';
import 'package:avelri_gestao/features/check_ins/presentation/manual_check_in_request_id.dart';
import 'package:avelri_gestao/features/check_ins/presentation/student_picker_dialog.dart';
import 'package:avelri_gestao/features/students/data/students_service.dart';
import 'package:avelri_gestao/features/students/models/student_summary.dart';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

class ManualCheckInButton extends StatefulWidget {
  const ManualCheckInButton({
    required this.apiClient,
    required this.onCompleted,
    super.key,
  });

  final ApiClient apiClient;
  final Future<void> Function() onCompleted;

  @override
  State<ManualCheckInButton> createState() => _ManualCheckInButtonState();
}

class _ManualCheckInButtonState extends State<ManualCheckInButton> {
  bool _isSubmitting = false;

  String? _pendingRequestId;
  String? _pendingStudentId;

  Future<void> _start() async {
    final student = await showDialog<StudentSummary>(
      context: context,
      builder: (dialogContext) {
        return StudentPickerDialog(
          service: StudentsService(widget.apiClient),
          archiveFilter: 1,
        );
      },
    );

    if (student == null || !mounted) {
      return;
    }

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) {
        return AlertDialog(
          title: const Text('Registrar check-in manual?'),
          content: SizedBox(
            width: 430,
            child: Text(
              'Registrar a presença de ${student.name} manualmente?\n\n'
              'Use esta opção como contingência quando o fluxo automático '
              'pela catraca estiver indisponível. Este registro representa '
              'presença e não altera matrícula ou situação financeira.',
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(dialogContext).pop(false),
              child: const Text('Cancelar'),
            ),
            FilledButton(
              onPressed: () => Navigator.of(dialogContext).pop(true),
              child: const Text('Registrar'),
            ),
          ],
        );
      },
    );

    if (confirmed != true || !mounted) {
      return;
    }

    final requestId =
        _pendingStudentId == student.id && _pendingRequestId != null
        ? _pendingRequestId!
        : createManualCheckInRequestId();

    _pendingStudentId = student.id;
    _pendingRequestId = requestId;

    setState(() {
      _isSubmitting = true;
    });

    try {
      await CheckInsService(widget.apiClient)
          .createManual(requestId: requestId, studentId: student.id);

      _pendingStudentId = null;
      _pendingRequestId = null;

      await widget.onCompleted();

      if (!mounted) {
        return;
      }

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Check-in manual de ${student.name} registrado.'),
        ),
      );
    } on ApiException catch (exception) {
      if (!mounted) {
        return;
      }

      _showError(_messageFromApiException(exception));
    } on TimeoutException {
      if (!mounted) {
        return;
      }

      _showError(
        'A operação demorou mais que o esperado. '
        'Tente novamente; o Avelri reutilizará a mesma solicitação.',
      );
    } on http.ClientException {
      if (!mounted) {
        return;
      }

      _showError(
        'Não foi possível conectar ao servidor. '
        'Tente novamente quando a conexão estiver disponível.',
      );
    } catch (_) {
      if (!mounted) {
        return;
      }

      _showError('Não foi possível registrar o check-in manual.');
    } finally {
      if (mounted) {
        setState(() {
          _isSubmitting = false;
        });
      }
    }
  }

  String _messageFromApiException(ApiException exception) {
    if (exception.statusCode == 403) {
      return 'Você não possui permissão para registrar check-in manual.';
    }

    if (exception.statusCode == 404) {
      return 'O aluno não foi encontrado.';
    }

    if (exception.statusCode == 409) {
      return 'Não foi possível registrar a presença deste aluno.';
    }

    if (exception.statusCode >= 500) {
      return 'O Avelri está temporariamente indisponível. Tente novamente.';
    }

    return 'Não foi possível registrar o check-in manual.';
  }

  void _showError(String message) {
    ScaffoldMessenger.of(context)
        .showSnackBar(SnackBar(content: Text(message)));
  }

  @override
  Widget build(BuildContext context) {
    return OutlinedButton.icon(
      onPressed: _isSubmitting ? null : _start,
      icon: _isSubmitting
          ? const SizedBox(
              width: 16,
              height: 16,
              child: CircularProgressIndicator(strokeWidth: 2),
            )
          : const Icon(Icons.person_add_alt_1_outlined, size: 18),
      label: const Text('Check-in manual'),
    );
  }
}
