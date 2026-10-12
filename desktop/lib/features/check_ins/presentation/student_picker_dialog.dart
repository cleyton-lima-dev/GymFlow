import 'dart:async';

import 'package:avelri_gestao/features/students/data/students_service.dart';
import 'package:avelri_gestao/features/students/models/student_summary.dart';
import 'package:flutter/material.dart';

class StudentPickerDialog extends StatefulWidget {
  const StudentPickerDialog({
    required this.service,
    this.archiveFilter = 3,
    super.key,
  });

  final StudentsService service;

  /// 1 = não arquivados
  /// 2 = arquivados
  /// 3 = todos
  final int archiveFilter;

  @override
  State<StudentPickerDialog> createState() => _StudentPickerDialogState();
}

class _StudentPickerDialogState extends State<StudentPickerDialog> {
  final TextEditingController _searchController = TextEditingController();

  Timer? _searchTimer;

  List<StudentSummary> _students = [];

  bool _isLoading = false;

  String? _errorMessage;

  int _requestVersion = 0;

  @override
  void initState() {
    super.initState();

    _load();
  }

  @override
  void dispose() {
    _searchTimer?.cancel();
    _searchController.dispose();

    super.dispose();
  }

  void _onSearchChanged(String value) {
    _searchTimer?.cancel();

    _searchTimer = Timer(const Duration(milliseconds: 350), _load);
  }

  Future<void> _load() async {
    final requestVersion = ++_requestVersion;

    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });

    try {
      final response = await widget.service.getStudents(
        search: _searchController.text,
        archiveFilter: widget.archiveFilter,
        page: 1,
        pageSize: 20,
      );

      if (!mounted || requestVersion != _requestVersion) {
        return;
      }

      setState(() {
        _students = response.items;
      });
    } catch (_) {
      if (!mounted || requestVersion != _requestVersion) {
        return;
      }

      setState(() {
        _errorMessage = 'Não foi possível carregar os alunos.';
      });
    } finally {
      if (mounted && requestVersion == _requestVersion) {
        setState(() {
          _isLoading = false;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return AlertDialog(
      title: const Text('Selecionar aluno'),
      content: SizedBox(
        width: 520,
        height: 470,
        child: Column(
          children: [
            TextField(
              controller: _searchController,
              autofocus: true,
              onChanged: _onSearchChanged,
              decoration: const InputDecoration(
                hintText: 'Buscar por nome, e-mail ou telefone...',
                prefixIcon: Icon(Icons.search_rounded),
              ),
            ),
            const SizedBox(height: 16),
            Expanded(child: _buildContent(colorScheme)),
          ],
        ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.of(context).pop(),
          child: const Text('Cancelar'),
        ),
      ],
    );
  }

  Widget _buildContent(ColorScheme colorScheme) {
    if (_isLoading && _students.isEmpty) {
      return const Center(child: CircularProgressIndicator());
    }

    if (_errorMessage != null) {
      return Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(_errorMessage!, textAlign: TextAlign.center),
            const SizedBox(height: 12),
            OutlinedButton(
              onPressed: _load,
              child: const Text('Tentar novamente'),
            ),
          ],
        ),
      );
    }

    if (_students.isEmpty) {
      return Center(
        child: Text(
          'Nenhum aluno encontrado.',
          style: TextStyle(color: colorScheme.onSurfaceVariant),
        ),
      );
    }

    return ListView.separated(
      itemCount: _students.length,
      separatorBuilder: (_, _) => const Divider(height: 1),
      itemBuilder: (context, index) {
        final student = _students[index];

        return ListTile(
          contentPadding: const EdgeInsets.symmetric(
            horizontal: 4,
            vertical: 4,
          ),
          leading: CircleAvatar(child: Text(_initials(student.name))),
          title: Text(student.name),
          subtitle: Text(
            student.archivedAt == null
                ? student.email
                : '${student.email} • Arquivado',
          ),
          trailing: const Icon(Icons.chevron_right_rounded),
          onTap: () {
            Navigator.of(context).pop(student);
          },
        );
      },
    );
  }

  String _initials(String name) {
    final parts = name.trim().split(RegExp(r'\s+'));

    if (parts.isEmpty || parts.first.isEmpty) {
      return 'A';
    }

    if (parts.length == 1) {
      return parts.first[0].toUpperCase();
    }

    return '${parts.first[0]}${parts.last[0]}'.toUpperCase();
  }
}
