import 'package:avelri_gestao/app/theme/branding_controller.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/check_ins/data/check_ins_service.dart';
import 'package:avelri_gestao/features/check_ins/data/physical_access_events_service.dart';
import 'package:avelri_gestao/features/check_ins/presentation/physical_access_events_view_model.dart';
import 'package:avelri_gestao/features/check_ins/presentation/physical_access_events_table.dart';
import 'package:avelri_gestao/features/check_ins/presentation/physical_access_events_filters.dart';
import 'package:avelri_gestao/features/check_ins/models/check_in_history_item.dart';
import 'package:avelri_gestao/features/check_ins/presentation/check_ins_view_model.dart';
import 'package:avelri_gestao/features/check_ins/presentation/manual_check_in_button.dart';
import 'package:avelri_gestao/features/check_ins/presentation/student_picker_dialog.dart';
import 'package:avelri_gestao/features/students/data/students_service.dart';
import 'package:avelri_gestao/features/students/models/student_summary.dart';
import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:provider/provider.dart';

class CheckInsPage extends StatelessWidget {
  const CheckInsPage({super.key});

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        ChangeNotifierProvider(
          create: (context) =>
              CheckInsViewModel(CheckInsService(context.read<ApiClient>()))
                ..loadInitial(),
        ),
        ChangeNotifierProvider(
          create: (context) => PhysicalAccessEventsViewModel(
            PhysicalAccessEventsService(context.read<ApiClient>()),
          ),
        ),
      ],
      child: const _CheckInsView(),
    );
  }
}

class _CheckInsView extends StatefulWidget {
  const _CheckInsView();

  @override
  State<_CheckInsView> createState() => _CheckInsViewState();
}

class _CheckInsViewState extends State<_CheckInsView> {
  int _selectedTab = 0;

  @override
  Widget build(BuildContext context) {
    final viewModel = context.watch<CheckInsViewModel>();

    final branding = context.watch<BrandingController>().branding;

    final primaryColor = branding.primaryColor;

    final theme = Theme.of(context);

    final colorScheme = theme.colorScheme;

    return SingleChildScrollView(
      padding: const EdgeInsets.all(30),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 1250),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Frequência e presença',
                        style: TextStyle(
                          color: colorScheme.onSurface,
                          fontSize: 28,
                          fontWeight: FontWeight.w800,
                        ),
                      ),
                      const SizedBox(height: 7),
                      Text(
                        _selectedTab == 0
                            ? 'Acompanhe as presenças confirmadas dos alunos na academia.'
                            : 'Consulte tentativas de acesso, decisões e motivos registrados.',
                        style: TextStyle(
                          color: colorScheme.onSurfaceVariant,
                          fontSize: 14,
                        ),
                      ),
                    ],
                  ),
                ),
                if (_selectedTab == 0) ...[
                  ManualCheckInButton(
                    apiClient: context.read<ApiClient>(),
                    onCompleted: viewModel.refresh,
                  ),
                  const SizedBox(width: 8),
                  IconButton(
                    tooltip: 'Atualizar presenças',
                    onPressed: viewModel.isLoading ? null : viewModel.refresh,
                    icon: Icon(Icons.refresh_rounded, color: primaryColor),
                  ),
                ],
              ],
            ),
            const SizedBox(height: 26),
            SegmentedButton<int>(
              segments: const [
                ButtonSegment<int>(
                  value: 0,
                  icon: Icon(Icons.how_to_reg_outlined),
                  label: Text('Presenças'),
                ),
                ButtonSegment<int>(
                  value: 1,
                  icon: Icon(Icons.sensor_door_outlined),
                  label: Text('Eventos de acesso'),
                ),
              ],
              selected: {_selectedTab},
              showSelectedIcon: false,
              onSelectionChanged: (selection) {
                final nextTab = selection.first;

                if (nextTab == 1 && _selectedTab != 1) {
                  context.read<PhysicalAccessEventsViewModel>().loadInitial();
                }

                setState(() => _selectedTab = nextTab);
              },
            ),
            const SizedBox(height: 20),
            if (_selectedTab == 0)
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(22),
                decoration: BoxDecoration(
                  color: colorScheme.surface,
                  borderRadius: BorderRadius.circular(18),
                  border: Border.all(color: theme.dividerColor),
                ),
                child: Column(
                  children: [
                    _PeriodFilters(
                      viewModel: viewModel,
                      primaryColor: primaryColor,
                    ),
                    const SizedBox(height: 20),
                    Divider(color: theme.dividerColor, height: 1),
                    const SizedBox(height: 20),
                    if (viewModel.isLoading && !viewModel.hasItems)
                      const Padding(
                        padding: EdgeInsets.symmetric(vertical: 70),
                        child: CircularProgressIndicator(),
                      )
                    else if (viewModel.errorMessage != null)
                      _ErrorState(
                        message: viewModel.errorMessage!,
                        onRetry: viewModel.retry,
                      )
                    else if (!viewModel.hasItems)
                      const _EmptyState()
                    else
                      _CheckInsTable(items: viewModel.items),
                    if (viewModel.hasItems) ...[
                      const SizedBox(height: 18),
                      Divider(color: theme.dividerColor, height: 1),
                      const SizedBox(height: 16),
                      Row(
                        children: [
                          Text(
                            '${viewModel.totalCount} presença${viewModel.totalCount == 1 ? '' : 's'} registrada${viewModel.totalCount == 1 ? '' : 's'}',
                            style: TextStyle(
                              color: colorScheme.onSurfaceVariant,
                              fontSize: 12,
                            ),
                          ),
                          const Spacer(),
                          IconButton(
                            tooltip: 'Página anterior',
                            onPressed:
                                viewModel.hasPreviousPage &&
                                    !viewModel.isLoading
                                ? viewModel.goToPreviousPage
                                : null,
                            icon: const Icon(Icons.chevron_left_rounded),
                          ),
                          Padding(
                            padding: const EdgeInsets.symmetric(horizontal: 8),
                            child: Text(
                              'Página ${viewModel.page} de ${viewModel.totalPages}',
                              style: TextStyle(
                                color: colorScheme.onSurface,
                                fontSize: 12,
                                fontWeight: FontWeight.w700,
                              ),
                            ),
                          ),
                          IconButton(
                            tooltip: 'Próxima página',
                            onPressed:
                                viewModel.hasNextPage && !viewModel.isLoading
                                ? viewModel.goToNextPage
                                : null,
                            icon: const Icon(Icons.chevron_right_rounded),
                          ),
                        ],
                      ),
                    ],
                  ],
                ),
              ),
            if (_selectedTab == 1)
              Consumer<PhysicalAccessEventsViewModel>(
                builder: (context, events, _) {
                  return Container(
                    width: double.infinity,
                    padding: const EdgeInsets.all(22),
                    decoration: BoxDecoration(
                      color: colorScheme.surface,
                      borderRadius: BorderRadius.circular(18),
                      border: Border.all(color: theme.dividerColor),
                    ),
                    child: Column(
                      children: [
                        Row(
                          children: [
                            Expanded(
                              child: Text(
                                'Histórico de eventos de acesso',
                                style: TextStyle(
                                  color: colorScheme.onSurface,
                                  fontSize: 17,
                                  fontWeight: FontWeight.w700,
                                ),
                              ),
                            ),
                            IconButton(
                              tooltip: 'Atualizar eventos',
                              onPressed: events.isLoading
                                  ? null
                                  : events.refresh,
                              icon: Icon(
                                Icons.refresh_rounded,
                                color: primaryColor,
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: 8),
                        Align(
                          alignment: Alignment.centerLeft,
                          child: Text(
                            'Tentativas de acesso não representam '
                            'presenças confirmadas.',
                            style: TextStyle(
                              color: colorScheme.onSurfaceVariant,
                              fontSize: 12,
                            ),
                          ),
                        ),
                        const SizedBox(height: 20),
                        Align(
                          alignment: Alignment.centerLeft,
                          child: PhysicalAccessEventsFilters(viewModel: events),
                        ),
                        const SizedBox(height: 20),
                        Divider(color: theme.dividerColor, height: 1),
                        const SizedBox(height: 20),
                        if (events.isLoading && !events.hasItems)
                          const Padding(
                            padding: EdgeInsets.symmetric(vertical: 70),
                            child: CircularProgressIndicator(),
                          )
                        else if (events.errorMessage != null)
                          _ErrorState(
                            message: events.errorMessage!,
                            onRetry: events.retry,
                          )
                        else if (!events.hasItems)
                          const Padding(
                            padding: EdgeInsets.symmetric(vertical: 65),
                            child: Column(
                              children: [
                                Icon(Icons.sensor_door_outlined, size: 38),
                                SizedBox(height: 12),
                                Text('Nenhum evento de acesso encontrado.'),
                              ],
                            ),
                          )
                        else
                          PhysicalAccessEventsTable(items: events.items),
                        if (events.hasItems) ...[
                          const SizedBox(height: 18),
                          Divider(color: theme.dividerColor, height: 1),
                          const SizedBox(height: 16),
                          Row(
                            children: [
                              Text(
                                '${events.totalCount} evento${events.totalCount == 1 ? '' : 's'} encontrado${events.totalCount == 1 ? '' : 's'}',
                                style: TextStyle(
                                  color: colorScheme.onSurfaceVariant,
                                  fontSize: 12,
                                ),
                              ),
                              const Spacer(),
                              IconButton(
                                tooltip: 'Página anterior',
                                onPressed:
                                    events.hasPreviousPage && !events.isLoading
                                    ? events.goToPreviousPage
                                    : null,
                                icon: const Icon(Icons.chevron_left_rounded),
                              ),
                              Padding(
                                padding: const EdgeInsets.symmetric(
                                  horizontal: 8,
                                ),
                                child: Text(
                                  'Página ${events.page} de ${events.totalPages}',
                                  style: TextStyle(
                                    color: colorScheme.onSurface,
                                    fontSize: 12,
                                    fontWeight: FontWeight.w700,
                                  ),
                                ),
                              ),
                              IconButton(
                                tooltip: 'Próxima página',
                                onPressed:
                                    events.hasNextPage && !events.isLoading
                                    ? events.goToNextPage
                                    : null,
                                icon: const Icon(Icons.chevron_right_rounded),
                              ),
                            ],
                          ),
                        ],
                      ],
                    ),
                  );
                },
              ),
          ],
        ),
      ),
    );
  }
}

class _PeriodFilters extends StatelessWidget {
  const _PeriodFilters({required this.viewModel, required this.primaryColor});

  final CheckInsViewModel viewModel;
  final Color primaryColor;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          'Filtros',
          style: TextStyle(
            color: colorScheme.onSurface,
            fontSize: 14,
            fontWeight: FontWeight.w800,
          ),
        ),
        const SizedBox(height: 12),
        Wrap(
          spacing: 10,
          runSpacing: 10,
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
    final initial = viewModel.fromDate ?? DateTime.now();

    final selected = await showDatePicker(
      context: context,
      initialDate: initial,
      firstDate: DateTime(2020),
      lastDate: DateTime.now().add(const Duration(days: 365)),
    );

    if (selected == null || !context.mounted) {
      return;
    }

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
    final initial = viewModel.toDate ?? DateTime.now();

    final selected = await showDatePicker(
      context: context,
      initialDate: initial,
      firstDate: DateTime(2020),
      lastDate: DateTime.now().add(const Duration(days: 365)),
    );

    if (selected == null || !context.mounted) {
      return;
    }

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

class _CheckInsTable extends StatelessWidget {
  const _CheckInsTable({required this.items});

  final List<CheckInHistoryItem> items;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    final colorScheme = theme.colorScheme;

    return ClipRRect(
      borderRadius: BorderRadius.circular(12),
      child: SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: DataTable(
          columnSpacing: 42,
          horizontalMargin: 16,
          headingRowColor: WidgetStateProperty.all(
            colorScheme.surfaceContainerHighest,
          ),
          headingTextStyle: TextStyle(
            color: colorScheme.onSurfaceVariant,
            fontSize: 11,
            fontWeight: FontWeight.w800,
          ),
          columns: const [
            DataColumn(label: Text('ALUNO')),
            DataColumn(label: Text('DATA')),
            DataColumn(label: Text('HORÁRIO')),
            DataColumn(label: Text('ORIGEM')),
          ],
          rows: items.map((item) {
            final local = item.occurredAt.toLocal();

            return DataRow(
              cells: [
                DataCell(
                  SizedBox(
                    width: 220,
                    child: Text(
                      item.studentName,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(
                        color: colorScheme.onSurface,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ),
                ),
                DataCell(Text(DateFormat('dd/MM/yyyy').format(local))),
                DataCell(Text(DateFormat('HH:mm').format(local))),
                DataCell(_SourceBadge(source: item.source)),
              ],
            );
          }).toList(),
        ),
      ),
    );
  }
}

class _SourceBadge extends StatelessWidget {
  const _SourceBadge({required this.source});

  final CheckInSource source;

  @override
  Widget build(BuildContext context) {
    final color = switch (source) {
      CheckInSource.physicalAccess => const Color(0xFF2CB67D),
      CheckInSource.manual => const Color(0xFFF3A712),
      CheckInSource.wellhub => const Color(0xFF5374E7),
      CheckInSource.otherIntegration => Theme.of(
        context,
      ).colorScheme.onSurfaceVariant,
    };

    final label = switch (source) {
      CheckInSource.physicalAccess => 'Catraca',
      CheckInSource.manual => 'Manual',
      CheckInSource.wellhub => 'Wellhub',
      CheckInSource.otherIntegration => 'Integração',
    };

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.10),
        borderRadius: BorderRadius.circular(30),
      ),
      child: Text(
        label,
        style: TextStyle(
          color: color,
          fontSize: 11,
          fontWeight: FontWeight.w700,
        ),
      ),
    );
  }
}

class _EmptyState extends StatelessWidget {
  const _EmptyState();

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 70),
      child: Column(
        children: [
          Icon(
            Icons.how_to_reg_outlined,
            color: colorScheme.onSurfaceVariant,
            size: 44,
          ),
          const SizedBox(height: 14),
          Text(
            'Nenhuma presença encontrada',
            style: TextStyle(
              color: colorScheme.onSurface,
              fontSize: 15,
              fontWeight: FontWeight.w700,
            ),
          ),
          const SizedBox(height: 5),
          Text(
            'Altere o período para consultar outros registros.',
            style: TextStyle(color: colorScheme.onSurfaceVariant, fontSize: 12),
          ),
        ],
      ),
    );
  }
}

class _ErrorState extends StatelessWidget {
  const _ErrorState({required this.message, required this.onRetry});

  final String message;
  final Future<void> Function() onRetry;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 60),
      child: Column(
        children: [
          const Icon(Icons.error_outline_rounded, size: 38),
          const SizedBox(height: 12),
          Text(message, textAlign: TextAlign.center),
          const SizedBox(height: 16),
          OutlinedButton(
            onPressed: onRetry,
            child: const Text('Tentar novamente'),
          ),
        ],
      ),
    );
  }
}
