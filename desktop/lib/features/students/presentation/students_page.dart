import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:go_router/go_router.dart';
import 'package:avelri_gestao/app/theme/branding_controller.dart';
import 'package:avelri_gestao/app/theme/semantic_colors.dart';
import 'package:avelri_gestao/app/session/erp_feature_controller.dart';
import 'package:avelri_gestao/app/session/session_controller.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/core/widgets/app_status_states.dart';
import 'package:avelri_gestao/features/students/data/students_service.dart';
import 'package:avelri_gestao/features/students/models/student_summary.dart';
import 'package:avelri_gestao/features/students/presentation/students_view_model.dart';
import 'package:avelri_gestao/features/enrollments/models/enrollment_summary.dart';

class StudentsPage extends StatelessWidget {
  const StudentsPage({this.assessmentFilter, super.key});

  final String? assessmentFilter;

  @override
  Widget build(BuildContext context) {
    final gymId = context.watch<SessionController>().user?.gymId;
    final erpEnabled =
        gymId != null &&
        context.watch<ErpFeatureController>().isEnabledForGym(gymId);
    return ChangeNotifierProvider(
      key: ValueKey(erpEnabled),
      create: (context) => StudentsViewModel(
        StudentsService(context.read<ApiClient>()),
        assessmentFilter: switch (assessmentFilter) {
          'missing' => StudentsAssessmentFilter.missing,
          'reassessmentDue' => StudentsAssessmentFilter.reassessmentDue,
          _ => null,
        },
      )..loadInitial(),
      child: const _StudentsView(),
    );
  }
}

class _StudentsView extends StatelessWidget {
  const _StudentsView();

  @override
  Widget build(BuildContext context) {
    final viewModel = context.watch<StudentsViewModel>();
    final branding = context.watch<BrandingController>().branding;
    final userGymId = context.watch<SessionController>().user?.gymId;
    final erpEnabled =
        userGymId != null &&
        context.watch<ErpFeatureController>().isEnabledForGym(userGymId);
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
            LayoutBuilder(
              builder: (context, constraints) {
                final title = Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Gestão de alunos',
                      style: TextStyle(
                        color: colorScheme.onSurface,
                        fontWeight: FontWeight.w800,
                        letterSpacing: -0.5,
                        fontSize: 28,
                      ),
                    ),
                    const SizedBox(height: 7),
                    Text(
                      'Consulte e gerencie os alunos cadastrados na academia.',
                      style: TextStyle(
                        color: colorScheme.onSurfaceVariant,
                        fontSize: 14,
                      ),
                    ),
                  ],
                );
                final actions = [
                  if (erpEnabled)
                    OutlinedButton.icon(
                      onPressed: () => context.push('/students/import'),
                      icon: const Icon(Icons.upload_file_outlined),
                      label: const Text('Importar CSV/XLSX'),
                    ),
                  if (erpEnabled)
                    OutlinedButton.icon(
                      onPressed: () => context.push('/students/migration'),
                      icon: const Icon(Icons.sync_alt),
                      label: const Text('Migração universal'),
                    ),
                  FilledButton.icon(
                    onPressed: () => context.go('/students/new'),
                    icon: const Icon(Icons.person_add_alt_1_rounded),
                    label: const Text('Cadastrar aluno'),
                  ),
                ];

                if (constraints.maxWidth < 1100) {
                  return Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      title,
                      const SizedBox(height: 14),
                      Wrap(spacing: 8, runSpacing: 8, children: actions),
                    ],
                  );
                }

                return Row(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: [
                    Expanded(child: title),
                    Wrap(spacing: 8, runSpacing: 8, children: actions),
                  ],
                );
              },
            ),
            const SizedBox(height: 26),
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
                  Row(
                    children: [
                      Expanded(
                        child: TextField(
                          onChanged: viewModel.updateSearch,
                          decoration: InputDecoration(
                            hintText: 'Buscar por nome, e-mail ou telefone...',
                            prefixIcon: const Icon(
                              Icons.search_rounded,
                              size: 21,
                            ),
                            filled: true,
                            fillColor: colorScheme.surfaceContainerHighest,
                            contentPadding: const EdgeInsets.symmetric(
                              horizontal: 16,
                              vertical: 15,
                            ),
                            border: OutlineInputBorder(
                              borderRadius: BorderRadius.circular(11),
                              borderSide: BorderSide(color: theme.dividerColor),
                            ),
                            enabledBorder: OutlineInputBorder(
                              borderRadius: BorderRadius.circular(11),
                              borderSide: BorderSide(color: theme.dividerColor),
                            ),
                          ),
                        ),
                      ),
                      const SizedBox(width: 12),
                      OutlinedButton.icon(
                        onPressed: viewModel.isLoading
                            ? null
                            : () => _showStudentsFilters(
                                context,
                                viewModel,
                                erpEnabled: erpEnabled,
                              ),
                        icon: Icon(
                          viewModel.hasActiveFilter
                              ? Icons.filter_alt_rounded
                              : Icons.filter_alt_outlined,
                          size: 19,
                        ),
                        label: const Text('Filtros'),
                      ),
                      const SizedBox(width: 6),
                      IconButton(
                        tooltip: 'Atualizar',
                        onPressed: viewModel.isLoading
                            ? null
                            : viewModel.refresh,
                        icon: const Icon(Icons.refresh_rounded),
                      ),
                    ],
                  ),
                  if (viewModel.assessmentFilterLabel case final label?) ...[
                    const SizedBox(height: 12),
                    Align(
                      alignment: Alignment.centerLeft,
                      child: InputChip(
                        key: const ValueKey('student-assessment-filter'),
                        label: Text(label),
                        onDeleted: viewModel.isLoading
                            ? null
                            : viewModel.clearAssessmentFilter,
                      ),
                    ),
                    const SizedBox(height: 8),
                    Container(
                      key: const ValueKey('students-assessment-guidance'),
                      width: double.infinity,
                      padding: const EdgeInsets.symmetric(
                        horizontal: 12,
                        vertical: 10,
                      ),
                      decoration: BoxDecoration(
                        color: colorScheme.surfaceContainerHighest.withValues(
                          alpha: 0.45,
                        ),
                        borderRadius: BorderRadius.circular(10),
                      ),
                      child: Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Icon(
                            Icons.info_outline_rounded,
                            size: 17,
                            color: colorScheme.onSurfaceVariant,
                          ),
                          const SizedBox(width: 9),
                          Expanded(
                            child: Text(
                              'Para registrar ou atualizar avaliações físicas, use o Avelri Mobile no perfil Admin ou Professor.',
                              style: TextStyle(
                                color: colorScheme.onSurfaceVariant,
                                fontSize: 12,
                                height: 1.35,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],
                  const SizedBox(height: 20),
                  if (viewModel.isLoading && !viewModel.hasStudents)
                    const Padding(
                      padding: EdgeInsets.symmetric(vertical: 70),
                      child: CircularProgressIndicator(),
                    )
                  else if (viewModel.errorMessage != null)
                    _ErrorState(
                      message: viewModel.errorMessage!,
                      onRetry: viewModel.retry,
                    )
                  else if (!viewModel.hasStudents)
                    const _EmptyState()
                  else
                    _StudentsTable(
                      students: viewModel.students,
                      primaryColor: primaryColor,
                      erpEnabled: erpEnabled,
                    ),
                  if (viewModel.hasStudents) ...[
                    const SizedBox(height: 18),
                    Divider(color: theme.dividerColor, height: 1),
                    const SizedBox(height: 16),
                    LayoutBuilder(
                      builder: (context, constraints) {
                        final count = Text(
                          '${viewModel.totalCount} aluno${viewModel.totalCount == 1 ? '' : 's'} encontrado${viewModel.totalCount == 1 ? '' : 's'}',
                          style: TextStyle(
                            color: colorScheme.onSurfaceVariant,
                            fontSize: 12,
                          ),
                        );
                        final controls = Row(
                          mainAxisSize: MainAxisSize.min,
                          children: [
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
                              padding: const EdgeInsets.symmetric(
                                horizontal: 8,
                              ),
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
                        );

                        if (constraints.maxWidth < 600) {
                          return Column(
                            crossAxisAlignment: CrossAxisAlignment.stretch,
                            children: [
                              count,
                              const SizedBox(height: 4),
                              Align(
                                alignment: Alignment.centerRight,
                                child: controls,
                              ),
                            ],
                          );
                        }

                        return Row(
                          children: [
                            Expanded(child: count),
                            controls,
                          ],
                        );
                      },
                    ),
                  ],
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _StudentsTable extends StatelessWidget {
  const _StudentsTable({
    required this.students,
    required this.primaryColor,
    required this.erpEnabled,
  });

  final List<StudentSummary> students;
  final Color primaryColor;
  final bool erpEnabled;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;
    return LayoutBuilder(
      builder: (context, constraints) => constraints.maxWidth < 860
          ? Column(
              key: const ValueKey('students-compact-list'),
              children: [
                for (final student in students)
                  _buildCompactCard(context, student, colorScheme),
              ],
            )
          : ClipRRect(
              borderRadius: BorderRadius.circular(12),
              child: Container(
                key: const ValueKey('students-responsive-table'),
                decoration: BoxDecoration(
                  border: Border.all(color: theme.dividerColor),
                  borderRadius: BorderRadius.circular(12),
                ),
                child: Column(
                  children: [
                    _buildTableRow(
                      context,
                      isHeader: true,
                      student: const Text('ALUNO'),
                      email: const Text('E-MAIL'),
                      phone: const Text('TELEFONE'),
                      account: const Text('CONTA'),
                      enrollment: const Text('MATRÍCULA'),
                      plan: const Text('PLANO'),
                      endDate: const Text('VENCIMENTO'),
                    ),
                    for (final student in students)
                      _buildTableRow(
                        context,
                        student: _studentName(context, student, colorScheme),
                        email: _textCell(student.email),
                        phone: _textCell(student.phone ?? '—'),
                        account: Align(
                          alignment: Alignment.centerLeft,
                          child: _StatusBadge(isActive: student.isActive),
                        ),
                        enrollment: erpEnabled
                            ? _textCell(
                                _enrollmentStatusLabel(
                                  student.enrollmentStatus,
                                ),
                              )
                            : const SizedBox.shrink(),
                        plan: erpEnabled
                            ? _textCell(student.planName ?? '—')
                            : const SizedBox.shrink(),
                        endDate: erpEnabled
                            ? _textCell(
                                student.enrollmentEndDate == null
                                    ? '—'
                                    : _formatDate(student.enrollmentEndDate!),
                              )
                            : const SizedBox.shrink(),
                        rowKey: ValueKey('students-table-row-${student.id}'),
                        onTap: () => context.go('/students/${student.id}'),
                      ),
                  ],
                ),
              ),
            ),
    );
  }

  Widget _buildTableRow(
    BuildContext context, {
    required Widget student,
    required Widget email,
    required Widget phone,
    required Widget account,
    required Widget enrollment,
    required Widget plan,
    required Widget endDate,
    bool isHeader = false,
    Key? rowKey,
    VoidCallback? onTap,
  }) {
    final colorScheme = Theme.of(context).colorScheme;
    final cells = erpEnabled
        ? [
            (18, student),
            (20, email),
            (11, phone),
            (11, account),
            (13, enrollment),
            (16, plan),
            (11, endDate),
          ]
        : [(30, student), (34, email), (20, phone), (16, account)];
    final row = Container(
      key: isHeader ? const ValueKey('students-table-header') : rowKey,
      constraints: BoxConstraints(minHeight: isHeader ? 40 : 62),
      padding: const EdgeInsets.symmetric(vertical: 8),
      decoration: BoxDecoration(
        color: isHeader ? colorScheme.surfaceContainerHighest : null,
        border: isHeader
            ? null
            : Border(bottom: BorderSide(color: Theme.of(context).dividerColor)),
      ),
      child: Row(
        children: [
          for (final (flex, child) in cells)
            Expanded(
              flex: flex,
              child: Padding(
                padding: const EdgeInsets.symmetric(horizontal: 5),
                child: DefaultTextStyle.merge(
                  style: TextStyle(
                    color: isHeader
                        ? colorScheme.onSurfaceVariant
                        : colorScheme.onSurface,
                    fontSize: isHeader ? 10 : 12,
                    fontWeight: isHeader ? FontWeight.w800 : FontWeight.w400,
                  ),
                  maxLines: isHeader ? 1 : 2,
                  overflow: TextOverflow.ellipsis,
                  child: child,
                ),
              ),
            ),
        ],
      ),
    );
    if (isHeader) return row;
    return Material(
      color: Colors.transparent,
      child: InkWell(onTap: onTap, child: row),
    );
  }

  Widget _studentName(
    BuildContext context,
    StudentSummary student,
    ColorScheme colorScheme,
  ) => Tooltip(
    message: student.name,
    child: Row(
      children: [
        CircleAvatar(
          radius: 15,
          backgroundColor: primaryColor.withValues(alpha: 0.10),
          child: Text(
            _initials(student.name),
            style: TextStyle(
              color: primaryColor,
              fontSize: 10,
              fontWeight: FontWeight.w800,
            ),
          ),
        ),
        const SizedBox(width: 7),
        Expanded(
          child: Text(
            student.name,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(
              color: colorScheme.onSurface,
              fontSize: 12,
              fontWeight: FontWeight.w700,
            ),
          ),
        ),
      ],
    ),
  );

  Widget _textCell(String value) => Tooltip(
    message: value,
    child: Text(value, maxLines: 2, overflow: TextOverflow.ellipsis),
  );

  Widget _buildCompactCard(
    BuildContext context,
    StudentSummary student,
    ColorScheme colorScheme,
  ) => Container(
    key: ValueKey('students-compact-card-${student.id}'),
    width: double.infinity,
    margin: const EdgeInsets.only(bottom: 10),
    decoration: BoxDecoration(
      color: colorScheme.surface,
      border: Border.all(color: Theme.of(context).dividerColor),
      borderRadius: BorderRadius.circular(12),
    ),
    child: Material(
      color: Colors.transparent,
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: () => context.go('/students/${student.id}'),
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(child: _studentName(context, student, colorScheme)),
                  const SizedBox(width: 8),
                  _StatusBadge(isActive: student.isActive),
                ],
              ),
              const SizedBox(height: 8),
              Wrap(
                spacing: 14,
                runSpacing: 6,
                children: [
                  Text('E-mail: ${student.email}'),
                  Text('Telefone: ${student.phone ?? '—'}'),
                  if (erpEnabled) ...[
                    Text(
                      'Matrícula: ${_enrollmentStatusLabel(student.enrollmentStatus)}',
                    ),
                    Text('Plano: ${student.planName ?? '—'}'),
                    Text(
                      'Vencimento: ${student.enrollmentEndDate == null ? '—' : _formatDate(student.enrollmentEndDate!)}',
                    ),
                  ],
                ],
              ),
            ],
          ),
        ),
      ),
    ),
  );

  static String _enrollmentStatusLabel(EnrollmentStatus? status) {
    return switch (status) {
      EnrollmentStatus.active => 'Ativa',
      EnrollmentStatus.cancelled => 'Cancelada',
      EnrollmentStatus.expired => 'Vencida',
      EnrollmentStatus.pendingPayment => 'Aguardando pagamento',
      null => 'Sem matrícula',
    };
  }

  static String _initials(String name) {
    final parts = name.trim().split(RegExp(r'\s+'));

    if (parts.isEmpty || parts.first.isEmpty) {
      return 'A';
    }

    if (parts.length == 1) {
      return parts.first[0].toUpperCase();
    }

    return '${parts.first[0]}${parts.last[0]}'.toUpperCase();
  }

  static String _formatDate(DateTime date) {
    final day = date.day.toString().padLeft(2, '0');
    final month = date.month.toString().padLeft(2, '0');

    return '$day/$month/${date.year}';
  }
}

class _StatusBadge extends StatelessWidget {
  const _StatusBadge({required this.isActive});

  final bool isActive;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    final foreground = isActive
        ? AvelriSemanticColors.success
        : AvelriSemanticColors.error;

    final background = foreground.withValues(
      alpha: Theme.of(context).brightness == Brightness.dark ? 0.16 : 0.10,
    );

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(
        color: background,
        borderRadius: BorderRadius.circular(30),
      ),
      child: Text(
        isActive ? 'Ativo' : 'Inativo',
        style: TextStyle(
          color: colorScheme.onSurface,
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
    return const AppEmptyState(
      icon: Icons.people_outline_rounded,
      title: 'Nenhum aluno encontrado',
      message: 'Tente alterar a busca ou os filtros.',
    );
  }
}

class _ErrorState extends StatelessWidget {
  const _ErrorState({required this.message, required this.onRetry});

  final String message;
  final Future<void> Function() onRetry;

  @override
  Widget build(BuildContext context) {
    return AppErrorState(message: message, onRetry: () => onRetry());
  }
}

Future<void> _showStudentsFilters(
  BuildContext context,
  StudentsViewModel viewModel, {
  required bool erpEnabled,
}) async {
  var statusFilter = viewModel.statusFilter;
  var enrollmentFilter = viewModel.enrollmentFilter;
  var archiveFilter = viewModel.archiveFilter;

  final apply = await showDialog<bool>(
    context: context,
    builder: (context) {
      return StatefulBuilder(
        builder: (context, setState) {
          return AlertDialog(
            title: const Text('Filtros'),
            content: SizedBox(
              width: 360,
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  DropdownButtonFormField<StudentsStatusFilter>(
                    initialValue: statusFilter,
                    decoration: const InputDecoration(labelText: 'Conta'),
                    items: const [
                      DropdownMenuItem(
                        value: StudentsStatusFilter.all,
                        child: Text('Todas'),
                      ),
                      DropdownMenuItem(
                        value: StudentsStatusFilter.active,
                        child: Text('Ativa'),
                      ),
                      DropdownMenuItem(
                        value: StudentsStatusFilter.inactive,
                        child: Text('Inativa'),
                      ),
                    ],
                    onChanged: (value) {
                      if (value == null) return;

                      setState(() {
                        statusFilter = value;
                      });
                    },
                  ),
                  if (erpEnabled) ...[
                    const SizedBox(height: 18),
                    DropdownButtonFormField<StudentsEnrollmentFilter>(
                      initialValue: enrollmentFilter,
                      decoration: const InputDecoration(labelText: 'Matrícula'),
                      items: const [
                        DropdownMenuItem(
                          value: StudentsEnrollmentFilter.all,
                          child: Text('Todas'),
                        ),
                        DropdownMenuItem(
                          value: StudentsEnrollmentFilter.active,
                          child: Text('Ativa'),
                        ),
                        DropdownMenuItem(
                          value: StudentsEnrollmentFilter.expired,
                          child: Text('Vencida'),
                        ),
                        DropdownMenuItem(
                          value: StudentsEnrollmentFilter.cancelled,
                          child: Text('Cancelada'),
                        ),
                        DropdownMenuItem(
                          value: StudentsEnrollmentFilter.none,
                          child: Text('Sem matrícula'),
                        ),
                      ],
                      onChanged: (value) {
                        if (value == null) return;

                        setState(() {
                          enrollmentFilter = value;
                        });
                      },
                    ),
                  ],
                  const SizedBox(height: 18),

                  DropdownButtonFormField<StudentsArchiveFilter>(
                    initialValue: archiveFilter,
                    decoration: const InputDecoration(
                      labelText: 'Arquivamento',
                    ),
                    items: const [
                      DropdownMenuItem(
                        value: StudentsArchiveFilter.notArchived,
                        child: Text('Não arquivados'),
                      ),
                      DropdownMenuItem(
                        value: StudentsArchiveFilter.archived,
                        child: Text('Arquivados'),
                      ),
                      DropdownMenuItem(
                        value: StudentsArchiveFilter.all,
                        child: Text('Todos'),
                      ),
                    ],
                    onChanged: (value) {
                      if (value == null) return;

                      setState(() {
                        archiveFilter = value;
                      });
                    },
                  ),
                ],
              ),
            ),
            actions: [
              TextButton(
                onPressed: () {
                  statusFilter = StudentsStatusFilter.all;
                  enrollmentFilter = StudentsEnrollmentFilter.all;

                  Navigator.of(context).pop(true);
                },
                child: const Text('Limpar'),
              ),
              TextButton(
                onPressed: () => Navigator.of(context).pop(false),
                child: const Text('Cancelar'),
              ),
              FilledButton(
                onPressed: () => Navigator.of(context).pop(true),
                child: const Text('Aplicar'),
              ),
            ],
          );
        },
      );
    },
  );

  if (apply != true || !context.mounted) return;

  await viewModel.updateFilters(
    statusFilter: statusFilter,
    enrollmentFilter: erpEnabled
        ? enrollmentFilter
        : StudentsEnrollmentFilter.all,
    archiveFilter: archiveFilter,
  );
}
