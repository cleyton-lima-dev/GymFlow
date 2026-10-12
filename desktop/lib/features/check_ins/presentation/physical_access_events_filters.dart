import 'package:avelri_gestao/features/check_ins/presentation/physical_access_events_view_model.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/check_ins/presentation/student_picker_dialog.dart';
import 'package:avelri_gestao/features/students/data/students_service.dart';
import 'package:avelri_gestao/features/students/models/student_summary.dart';
import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:provider/provider.dart';

class PhysicalAccessEventsFilters extends StatelessWidget {
  const PhysicalAccessEventsFilters({required this.viewModel, super.key});

  final PhysicalAccessEventsViewModel viewModel;

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          'Filtros',
          style: TextStyle(
            color: colors.onSurface,
            fontSize: 14,
            fontWeight: FontWeight.w800,
          ),
        ),
        const SizedBox(height: 12),
        Wrap(
          spacing: 12,
          runSpacing: 12,
          crossAxisAlignment: WrapCrossAlignment.center,
          children: [
            OutlinedButton.icon(
              onPressed: viewModel.isLoading
                  ? null
                  : () => _selectStudent(context),
              icon: const Icon(Icons.person_search_outlined, size: 18),
              label: Text(viewModel.studentName ?? 'Todos os alunos'),
            ),
            if (viewModel.hasStudentFilter)
              IconButton(
                tooltip: 'Remover filtro de aluno',
                onPressed: viewModel.isLoading
                    ? null
                    : viewModel.clearStudentFilter,
                icon: const Icon(Icons.close_rounded),
              ),
            OutlinedButton.icon(
              onPressed: viewModel.isLoading
                  ? null
                  : () => _selectFromDate(context),
              icon: const Icon(Icons.calendar_today_outlined, size: 17),
              label: Text(
                viewModel.fromDate == null
                    ? 'Data inicial'
                    : DateFormat('dd/MM/yyyy').format(viewModel.fromDate!),
              ),
            ),
            OutlinedButton.icon(
              onPressed: viewModel.isLoading
                  ? null
                  : () => _selectToDate(context),
              icon: const Icon(Icons.event_outlined, size: 17),
              label: Text(
                viewModel.toDate == null
                    ? 'Data final'
                    : DateFormat('dd/MM/yyyy').format(viewModel.toDate!),
              ),
            ),
            if (viewModel.hasDateFilter)
              TextButton(
                onPressed: viewModel.isLoading ? null : viewModel.clearPeriod,
                child: const Text('Limpar período'),
              ),
          ],
        ),
        const SizedBox(height: 16),
        Wrap(
          spacing: 12,
          runSpacing: 12,
          crossAxisAlignment: WrapCrossAlignment.center,
          children: [
            DropdownButton<int?>(
              value: viewModel.decision,
              hint: const Text('Todas as decisões'),
              items: const [
                DropdownMenuItem<int?>(
                  value: null,
                  child: Text('Todas as decisões'),
                ),
                DropdownMenuItem<int?>(value: 1, child: Text('Permitido')),
                DropdownMenuItem<int?>(value: 2, child: Text('Negado')),
              ],
              onChanged: viewModel.isLoading
                  ? null
                  : (value) => viewModel.updateDecision(value),
            ),
            DropdownButton<int?>(
              value: viewModel.source,
              hint: const Text('Todas as origens'),
              items: const [
                DropdownMenuItem<int?>(
                  value: null,
                  child: Text('Todas as origens'),
                ),
                DropdownMenuItem<int?>(value: 1, child: Text('Online')),
                DropdownMenuItem<int?>(value: 2, child: Text('Cache offline')),
                DropdownMenuItem<int?>(value: 3, child: Text('Negado offline')),
              ],
              onChanged: viewModel.isLoading
                  ? null
                  : (value) => viewModel.updateSource(value),
            ),
          ],
        ),
      ],
    );
  }

  Future<void> _selectStudent(BuildContext context) async {
    final selected = await showDialog<StudentSummary>(
      context: context,
      builder: (dialogContext) {
        return StudentPickerDialog(
          service: StudentsService(context.read<ApiClient>()),
          archiveFilter: 3,
        );
      },
    );

    if (selected == null || !context.mounted) {
      return;
    }

    await viewModel.updateStudentFilter(
      studentId: selected.id,
      studentName: selected.name,
    );
  }

  Future<void> _selectFromDate(BuildContext context) async {
    final selected = await showDatePicker(
      context: context,
      initialDate: viewModel.fromDate ?? DateTime.now(),
      firstDate: DateTime(2020),
      lastDate: DateTime.now().add(const Duration(days: 365)),
    );

    if (selected == null || !context.mounted) return;

    if (viewModel.toDate != null && selected.isAfter(viewModel.toDate!)) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('A data inicial não pode ser posterior à data final.'),
        ),
      );
      return;
    }

    await viewModel.updatePeriod(fromDate: selected, toDate: viewModel.toDate);
  }

  Future<void> _selectToDate(BuildContext context) async {
    final selected = await showDatePicker(
      context: context,
      initialDate: viewModel.toDate ?? DateTime.now(),
      firstDate: DateTime(2020),
      lastDate: DateTime.now().add(const Duration(days: 365)),
    );

    if (selected == null || !context.mounted) return;

    if (viewModel.fromDate != null && selected.isBefore(viewModel.fromDate!)) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('A data final não pode ser anterior à data inicial.'),
        ),
      );
      return;
    }

    await viewModel.updatePeriod(
      fromDate: viewModel.fromDate,
      toDate: selected,
    );
  }
}
