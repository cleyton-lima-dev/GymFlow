import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:go_router/go_router.dart';
import 'package:avelri_gestao/app/theme/branding_controller.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/students/data/students_service.dart';
import 'package:avelri_gestao/features/students/models/student_summary.dart';
import 'package:avelri_gestao/features/students/presentation/students_view_model.dart';
import 'package:avelri_gestao/features/enrollments/models/enrollment_summary.dart';

class StudentsPage extends StatelessWidget {
  const StudentsPage({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) =>
          StudentsViewModel(StudentsService(context.read<ApiClient>()))
            ..loadInitial(),
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
    final primaryColor = branding.primaryColor;

    return SingleChildScrollView(
      padding: const EdgeInsets.all(30),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 1250),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                const Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Gestão de alunos',
                        style: TextStyle(
                          color: Color(0xFF171A2C),
                          fontSize: 28,
                          fontWeight: FontWeight.w800,
                          letterSpacing: -0.5,
                        ),
                      ),
                      SizedBox(height: 7),
                      Text(
                        'Consulte e gerencie os alunos cadastrados na academia.',
                        style: TextStyle(
                          color: Color(0xFF74798D),
                          fontSize: 14,
                        ),
                      ),
                    ],
                  ),
                ),
                FilledButton.icon(
                  onPressed: () => context.go('/students/new'),
                  icon: const Icon(Icons.person_add_alt_1_rounded),
                  label: const Text('Cadastrar aluno'),
                  style: FilledButton.styleFrom(
                    backgroundColor: primaryColor,
                    foregroundColor: Colors.white,
                    padding: const EdgeInsets.symmetric(
                      horizontal: 18,
                      vertical: 17,
                    ),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(11),
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 26),
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(22),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(18),
                border: Border.all(color: const Color(0xFFE5E7EF)),
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
                            fillColor: const Color(0xFFF7F8FB),
                            contentPadding: const EdgeInsets.symmetric(
                              horizontal: 16,
                              vertical: 15,
                            ),
                            border: OutlineInputBorder(
                              borderRadius: BorderRadius.circular(11),
                              borderSide: const BorderSide(
                                color: Color(0xFFE3E5ED),
                              ),
                            ),
                            enabledBorder: OutlineInputBorder(
                              borderRadius: BorderRadius.circular(11),
                              borderSide: const BorderSide(
                                color: Color(0xFFE3E5ED),
                              ),
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
                        ),
                        icon: Icon(
                          viewModel.hasActiveFilter
                              ? Icons.filter_alt_rounded
                              : Icons.filter_alt_outlined,
                          size: 19,
                        ),
                        label: const Text('Filtros'),
                        style: OutlinedButton.styleFrom(
                          minimumSize: const Size(0, 50),
                          padding: const EdgeInsets.symmetric(
                            horizontal: 18,
                          ),
                        ),
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
                    ),
                  if (viewModel.hasStudents) ...[
                    const SizedBox(height: 18),
                    const Divider(color: Color(0xFFE8EAF1), height: 1),
                    const SizedBox(height: 16),
                    Row(
                      children: [
                        Text(
                          '${viewModel.totalCount} aluno${viewModel.totalCount == 1 ? '' : 's'} encontrado${viewModel.totalCount == 1 ? '' : 's'}',
                          style: const TextStyle(
                            color: Color(0xFF74798D),
                            fontSize: 12,
                          ),
                        ),
                        const Spacer(),
                        IconButton(
                          tooltip: 'Página anterior',
                          onPressed:
                              viewModel.hasPreviousPage && !viewModel.isLoading
                              ? viewModel.goToPreviousPage
                              : null,
                          icon: const Icon(Icons.chevron_left_rounded),
                        ),
                        Padding(
                          padding: const EdgeInsets.symmetric(horizontal: 8),
                          child: Text(
                            'Página ${viewModel.page} de ${viewModel.totalPages}',
                            style: const TextStyle(
                              color: Color(0xFF3B3F52),
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
          ],
        ),
      ),
    );
  }
}

class _StudentsTable extends StatelessWidget {
  const _StudentsTable({required this.students, required this.primaryColor});

  final List<StudentSummary> students;
  final Color primaryColor;

  @override
  Widget build(BuildContext context) {
    return ClipRRect(
      borderRadius: BorderRadius.circular(12),
      child: SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: DataTable(
          showCheckboxColumn: false,
          columnSpacing: 28,
          horizontalMargin: 16,
          headingRowColor: WidgetStateProperty.all(const Color(0xFFF7F8FB)),
          dataRowMinHeight: 64,
          dataRowMaxHeight: 64,
          headingTextStyle: const TextStyle(
            color: Color(0xFF6F7487),
            fontSize: 11,
            fontWeight: FontWeight.w800,
          ),
          columns: const [
            DataColumn(label: Text('ALUNO')),
            DataColumn(label: Text('E-MAIL')),
            DataColumn(label: Text('TELEFONE')),
            DataColumn(label: Text('CONTA')),
            DataColumn(label: Text('MATRÍCULA')),
            DataColumn(label: Text('PLANO')),
            DataColumn(label: Text('VENCIMENTO')),
          ],
          rows: students.map((student) {
            return DataRow(
              onSelectChanged: (_) {
                context.go('/students/${student.id}');
              },
              cells: [
                DataCell(
                  SizedBox(
                    width: 190,
                    child: Row(
                      children: [
                        CircleAvatar(
                          radius: 18,
                          backgroundColor: primaryColor.withValues(alpha: 0.10),
                          child: Text(
                            _initials(student.name),
                            style: TextStyle(
                              color: primaryColor,
                              fontSize: 11,
                              fontWeight: FontWeight.w800,
                            ),
                          ),
                        ),
                        const SizedBox(width: 11),
                        Expanded(
                          child: Text(
                            student.name,
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(
                              color: Color(0xFF222536),
                              fontSize: 13,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
                DataCell(
                  SizedBox(
                    width: 200,
                    child: Text(student.email, overflow: TextOverflow.ellipsis),
                  ),
                ),
                DataCell(
                  SizedBox(width: 110, child: Text(student.phone ?? '—')),
                ),
                DataCell(
                  _StatusBadge(
                    isActive: student.isActive,
                  ),
                ),
                DataCell(
                  Text(
                    _enrollmentStatusLabel(
                      student.enrollmentStatus,
                    ),
                  ),
                ),
                DataCell(
                  SizedBox(
                    width: 120,
                    child: Text(
                      student.planName ?? '—',
                      overflow: TextOverflow.ellipsis,
                    ),
                  ),
                ),
                DataCell(
                  SizedBox(
                    width: 90,
                    child: Text(
                      student.enrollmentEndDate == null
                          ? '—'
                          : _formatDate(
                        student.enrollmentEndDate!,
                      ),
                    ),
                  ),
                ),
              ],
            );
          }).toList(),
        ),
      ),
    );
  }

  static String _enrollmentStatusLabel(
      EnrollmentStatus? status,
      ) {
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
    final background = isActive
        ? const Color(0xFFEAF8F2)
        : const Color(0xFFFCEEEF);

    final foreground = isActive
        ? const Color(0xFF218C63)
        : const Color(0xFFB54752);

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(
        color: background,
        borderRadius: BorderRadius.circular(30),
      ),
      child: Text(
        isActive ? 'Ativo' : 'Inativo',
        style: TextStyle(
          color: foreground,
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
    return const Padding(
      padding: EdgeInsets.symmetric(vertical: 70),
      child: Column(
        children: [
          Icon(
            Icons.people_outline_rounded,
            color: Color(0xFFAFB3C2),
            size: 42,
          ),
          SizedBox(height: 14),
          Text(
            'Nenhum aluno encontrado',
            style: TextStyle(
              color: Color(0xFF343748),
              fontSize: 15,
              fontWeight: FontWeight.w700,
            ),
          ),
          SizedBox(height: 5),
          Text(
            'Tente alterar a busca ou os filtros.',
            style: TextStyle(color: Color(0xFF838799), fontSize: 12),
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
      padding: const EdgeInsets.symmetric(vertical: 55),
      child: Column(
        children: [
          const Icon(
            Icons.error_outline_rounded,
            color: Color(0xFFB54752),
            size: 38,
          ),
          const SizedBox(height: 12),
          Text(
            message,
            textAlign: TextAlign.center,
            style: const TextStyle(color: Color(0xFF656A7E), fontSize: 13),
          ),
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
Future<void> _showStudentsFilters(
    BuildContext context,
    StudentsViewModel viewModel,
    ) async {
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
                    decoration: const InputDecoration(
                      labelText: 'Conta',
                    ),
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
                  const SizedBox(height: 18),
                  DropdownButtonFormField<StudentsEnrollmentFilter>(
                    initialValue: enrollmentFilter,
                    decoration: const InputDecoration(
                      labelText: 'Matrícula',
                    ),
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
                  enrollmentFilter =
                      StudentsEnrollmentFilter.all;

                  Navigator.of(context).pop(true);
                },
                child: const Text('Limpar'),
              ),
              TextButton(
                onPressed: () =>
                    Navigator.of(context).pop(false),
                child: const Text('Cancelar'),
              ),
              FilledButton(
                onPressed: () =>
                    Navigator.of(context).pop(true),
                child: const Text('Aplicar'),
              ),
            ],
          );
        },
      );
    },
  );

  if (apply != true) return;

  await viewModel.updateFilters(
    statusFilter: statusFilter,
    enrollmentFilter: enrollmentFilter,
    archiveFilter: archiveFilter,
  );
}
