import 'dart:convert';

import 'package:avelri_gestao/core/network/api_exception.dart';
import 'package:avelri_gestao/features/students/data/student_import_api_service.dart';
import 'package:avelri_gestao/features/students/data/student_import_parser.dart';
import 'package:avelri_gestao/features/students/models/student_import_preview.dart';
import 'package:flutter/foundation.dart';

enum StudentImportField { name, email, phone, birthDate }

class StudentImportViewModel extends ChangeNotifier {
  StudentImportViewModel(this._service);

  final StudentImportApiService _service;
  ParsedStudentImportFile? source;
  StudentImportPreview? preview;
  StudentImportResult? result;
  final Map<StudentImportField, int?> mapping = {
    for (final field in StudentImportField.values) field: null,
  };
  final Map<int, String?> decisions = {};
  bool? newStudentsActive;
  bool isBusy = false;
  String? errorMessage;

  int get validCount => preview?.rows.where((row) => row.isValid).length ?? 0;
  int get errorCount => preview?.rows.where((row) => !row.isValid).length ?? 0;
  int get duplicateCount =>
      preview?.rows
          .where((row) => row.isValid && row.candidates.isNotEmpty)
          .length ??
      0;

  bool get hasRequiredMapping => mapping[StudentImportField.name] != null;
  bool get hasPreview => preview != null;
  bool get isCompleted => result != null;

  bool get canConfirm {
    final current = preview;
    if (current == null || current.alreadyCompleted || isBusy) return false;
    final validRows = current.rows.where((row) => row.isValid).toList();
    if (validRows.any((row) => decisions[row.rowNumber] == null)) return false;
    final hasImports = validRows.any(
      (row) => decisions[row.rowNumber] != 'Ignore',
    );
    return !hasImports || newStudentsActive != null;
  }

  void loadFile(String fileName, Uint8List? bytes) {
    _setBusy(true);
    errorMessage = null;
    try {
      source = parseStudentImportFile(fileName, bytes);
      preview = null;
      result = null;
      decisions.clear();
      newStudentsActive = null;
      for (final field in StudentImportField.values) {
        mapping[field] = _suggestColumn(source!.headers, field);
      }
    } on StudentImportFileException catch (exception) {
      source = null;
      errorMessage = exception.message;
    } catch (_) {
      source = null;
      errorMessage = 'Não foi possível ler este arquivo.';
    } finally {
      _setBusy(false);
    }
  }

  void setMapping(StudentImportField field, int? column) {
    mapping[field] = column;
    preview = null;
    result = null;
    decisions.clear();
    newStudentsActive = null;
    errorMessage = null;
    notifyListeners();
  }

  bool get mappingIsUnique {
    final selected = mapping.values.whereType<int>().toList();
    return selected.length == selected.toSet().length;
  }

  Future<void> createPreview() async {
    final parsed = source;
    if (parsed == null) return;
    if (!hasRequiredMapping) {
      errorMessage = 'Mapeie uma coluna para Nome.';
      notifyListeners();
      return;
    }
    if (!mappingIsUnique) {
      errorMessage = 'Cada campo deve usar uma coluna diferente.';
      notifyListeners();
      return;
    }

    _setBusy(true);
    errorMessage = null;
    result = null;
    try {
      final rows = parsed.rows.map((row) {
        String? value(StudentImportField field) {
          final index = mapping[field];
          if (index == null || index >= row.cells.length) return null;
          final cell = row.cells[index].trim();
          return cell.isEmpty ? null : cell;
        }

        return <String, dynamic>{
          'rowNumber': row.rowNumber,
          'name': value(StudentImportField.name),
          'email': value(StudentImportField.email),
          'phone': value(StudentImportField.phone),
          'birthDate': value(StudentImportField.birthDate),
        };
      }).toList();
      preview = await _service.preview(rows);
      decisions
        ..clear()
        ..addEntries(
          preview!.rows
              .where((row) => row.isValid && row.candidates.isEmpty)
              .map((row) => MapEntry(row.rowNumber, 'Import')),
        );
      newStudentsActive = null;
      if (preview!.alreadyCompleted) {
        result = StudentImportResult(
          batchId: preview!.batchId,
          alreadyCompleted: true,
          rows: preview!.results,
        );
      }
    } catch (exception) {
      errorMessage = _messageFromException(exception);
    } finally {
      _setBusy(false);
    }
  }

  void chooseDecision(int rowNumber, String action) {
    decisions[rowNumber] = action;
    notifyListeners();
  }

  void chooseNewStudentsStatus(bool isActive) {
    newStudentsActive = isActive;
    notifyListeners();
  }

  Future<void> confirmImport() async {
    final current = preview;
    if (current == null || !canConfirm) return;
    _setBusy(true);
    errorMessage = null;
    try {
      final rows = current.rows.where((row) => row.isValid).toList();
      final response = await _service.confirm(
        batchId: current.batchId,
        newStudentsActive: newStudentsActive,
        decisions: rows
            .map(
              (row) => <String, dynamic>{
                'rowNumber': row.rowNumber,
                'action': decisions[row.rowNumber],
              },
            )
            .toList(),
      );
      result = response;
    } catch (exception) {
      errorMessage = _messageFromException(exception);
    } finally {
      _setBusy(false);
    }
  }

  String _messageFromException(Object exception) {
    if (exception is ApiException) {
      try {
        final decoded = jsonDecode(exception.message);
        if (decoded is Map<String, dynamic> && decoded['message'] is String) {
          return decoded['message'] as String;
        }
      } on FormatException {
        // The generic message below avoids showing raw server details.
      }
      if (exception.statusCode == 409) {
        return 'A prévia mudou ou já foi processada. Gere a prévia novamente.';
      }
      if (exception.statusCode == 413) {
        return 'O arquivo excede o limite permitido.';
      }
      return 'Não foi possível comunicar com o serviço de importação.';
    }
    return 'Não foi possível concluir a importação.';
  }

  int? _suggestColumn(List<String> headers, StudentImportField field) {
    final normalized = headers.map(_normalizeHeader).toList();
    final hints = switch (field) {
      StudentImportField.name => ['cliente', 'nome', 'nome completo', 'aluno'],
      StudentImportField.email => ['email', 'e mail', 'e-mail'],
      StudentImportField.phone => ['telefone', 'telefones', 'celular', 'phone'],
      StudentImportField.birthDate => [
        'aniversario',
        'nascimento',
        'data nascimento',
        'data de nascimento',
      ],
    };
    final index = normalized.indexWhere(hints.contains);
    return index < 0 ? null : index;
  }

  String _normalizeHeader(String value) => value
      .trim()
      .toLowerCase()
      .replaceAll('á', 'a')
      .replaceAll('à', 'a')
      .replaceAll('ã', 'a')
      .replaceAll('â', 'a')
      .replaceAll('é', 'e')
      .replaceAll('ê', 'e')
      .replaceAll('í', 'i')
      .replaceAll('ó', 'o')
      .replaceAll('ô', 'o')
      .replaceAll('õ', 'o')
      .replaceAll('ú', 'u')
      .replaceAll('ç', 'c')
      .replaceAll(RegExp(r'[_\s]+'), ' ');

  void _setBusy(bool value) {
    isBusy = value;
    notifyListeners();
  }
}
