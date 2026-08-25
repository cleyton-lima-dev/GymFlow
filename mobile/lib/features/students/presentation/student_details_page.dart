import 'package:flutter/material.dart';
import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_bottom_navigation.dart';
import 'package:gymflow/features/students/data/students_service.dart';
import 'package:gymflow/features/students/models/student_summary.dart';
import 'package:gymflow/features/students/presentation/student_details_view_model.dart';
import 'package:provider/provider.dart';
import 'package:go_router/go_router.dart';
import 'package:gymflow/app/session/app_role.dart';
import 'package:gymflow/app/session/session_controller.dart';
import 'package:gymflow/features/physical_assessments/data/physical_assessments_service.dart';
import 'package:gymflow/features/physical_assessments/presentation/physical_assessment_summary_view_model.dart';
import 'package:gymflow/features/physical_assessments/presentation/widgets/physical_assessment_summary_card.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_page_header.dart';
import 'package:gymflow/features/workouts/data/workouts_service.dart';
import 'package:gymflow/features/workouts/presentation/current_workout_view_model.dart';
import 'package:gymflow/features/workouts/presentation/widgets/current_workout_summary_card.dart';

class StudentDetailsPage extends StatelessWidget {
  const StudentDetailsPage({
    required this.studentId,
    super.key,
  });

  final String studentId;

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        ChangeNotifierProvider(
          create: (context) => StudentDetailsViewModel(
            StudentsService(
              context.read<ApiClient>(),
            ),
            studentId,
          )..load(),
        ),

        ChangeNotifierProvider(
          create: (context) =>
          PhysicalAssessmentSummaryViewModel(
            PhysicalAssessmentsService(
              context.read<ApiClient>(),
            ),
            studentId,
          )..load(),
        ),

        ChangeNotifierProvider(
          create: (context) =>
          CurrentWorkoutViewModel(
            WorkoutsService(
              context.read<ApiClient>(),
            ),
            studentId,
          )..load(),
        ),
      ],
      child: const _StudentDetailsView(),
    );
  }
}

class _StudentDetailsView extends StatelessWidget {
  const _StudentDetailsView();

  @override
  Widget build(BuildContext context) {
    final viewModel = context.watch<StudentDetailsViewModel>();

    final isAdmin = context.select<SessionController, bool>(
          (controller) => controller.user?.role == AppRole.admin,
    );

    return Scaffold(
      body: SafeArea(
        bottom: false,
        child: switch (
        (viewModel.isLoading, viewModel.student, viewModel.errorMessage)
        ) {
          (true, null, _) => const Center(
            child: CircularProgressIndicator(),
          ),
          (_, null, final String error) => _ErrorState(
            message: error,
            onRetry: viewModel.load,
          ),
          (_, final StudentSummary student, _) =>
              _StudentDetailsContent(
                student: student,
                isAdmin: isAdmin,
                isUpdatingStatus: viewModel.isUpdatingStatus,
                onEdit: () async {
                  final updated = await context.push<bool>(
                    '/admin/students/${student.id}/edit',
                  );

                  if (!context.mounted || updated != true) {
                    return;
                  }

                  await context
                      .read<StudentDetailsViewModel>()
                      .load();
                },
                onStatusChanged: (isActive) async {
                  final updated = await context
                      .read<StudentDetailsViewModel>()
                      .updateStatus(isActive);

                  if (!context.mounted || !updated) {
                    return;
                  }

                  ScaffoldMessenger.of(context).showSnackBar(
                    SnackBar(
                      content: Text(
                        isActive
                            ? 'Aluno ativado com sucesso.'
                            : 'Aluno desativado com sucesso.',
                      ),
                    ),
                  );
                },
              ),
          _ => const SizedBox.shrink(),
        },
      ),
      bottomNavigationBar:
      const ProfessorAdminBottomNavigation(
        currentItem: ProfessorAdminNavItem.students,
      ),
    );
  }
}

class _StudentDetailsContent extends StatelessWidget {
  const _StudentDetailsContent({
    required this.student,
    required this.isAdmin,
    required this.isUpdatingStatus,
    required this.onEdit,
    required this.onStatusChanged,
  });

  final StudentSummary student;
  final bool isAdmin;
  final bool isUpdatingStatus;
  final VoidCallback onEdit;
  final ValueChanged<bool> onStatusChanged;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return SingleChildScrollView(
      padding: const EdgeInsets.fromLTRB(
        24,
        18,
        24,
        32,
      ),
      child: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(
            maxWidth: 680,
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const ProfessorAdminPageHeader(
                height: 82,
              ),

              const SizedBox(height: 26),

              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Container(
                    width: 88,
                    height: 88,
                    alignment: Alignment.center,
                    decoration: BoxDecoration(
                      shape: BoxShape.circle,
                      color: colorScheme.primary.withAlpha(24),
                    ),
                    child: Text(
                      _initials(student.name),
                      style: theme.textTheme.headlineMedium?.copyWith(
                        color: colorScheme.primary,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                  ),

                  const SizedBox(width: 18),

                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          student.name,
                          style: theme.textTheme.headlineSmall?.copyWith(
                            fontWeight: FontWeight.w800,
                          ),
                        ),

                        const SizedBox(height: 8),

                        _StatusBadge(
                          isActive: student.isActive,
                        ),

                        const SizedBox(height: 14),

                        _InfoLine(
                          icon: Icons.mail_outline_rounded,
                          text: student.email,
                        ),

                        const SizedBox(height: 8),

                        _InfoLine(
                          icon: Icons.phone_outlined,
                          text: _phoneText(student.phone),
                        ),
                      ],
                    ),
                  ),
                ],
              ),

              const SizedBox(height: 24),

              if (isAdmin) ...[
                const SizedBox(height: 24),

                Row(
                  children: [
                    Expanded(
                      child: OutlinedButton.icon(
                        onPressed: isUpdatingStatus ? null : onEdit,
                        icon: const Icon(
                          Icons.edit_outlined,
                          size: 19,
                        ),
                        label: const Text('Editar aluno'),
                      ),
                    ),

                    const SizedBox(width: 12),

                    Expanded(
                      child: OutlinedButton.icon(
                        onPressed: isUpdatingStatus
                            ? null
                            : () => _confirmStatusChange(context),
                        icon: isUpdatingStatus
                            ? const SizedBox(
                          width: 18,
                          height: 18,
                          child: CircularProgressIndicator(
                            strokeWidth: 2,
                          ),
                        )
                            : Icon(
                          student.isActive
                              ? Icons.person_off_outlined
                              : Icons.person_add_alt_outlined,
                        ),
                        label: Text(
                          student.isActive
                              ? 'Desativar'
                              : 'Ativar',
                        ),
                        style: student.isActive
                            ? OutlinedButton.styleFrom(
                          foregroundColor:
                          Theme.of(context).colorScheme.error,
                        )
                            : null,
                      ),
                    ),
                  ],
                ),

                const SizedBox(height: 20),
              ],

              CurrentWorkoutSummaryCard(
                onCreateWorkoutTap: () async {
                  final basePath =
                  isAdmin ? '/admin' : '/professor';

                  final created = await context.push<bool>(
                    '$basePath/students/${student.id}/'
                        'workouts/create',
                    extra: student.name,
                  );

                  if (!context.mounted || created != true) {
                    return;
                  }

                  await context
                      .read<CurrentWorkoutViewModel>()
                      .load();
                },
                onEditWorkoutTap: () async {
                  final workoutViewModel =
                  context.read<CurrentWorkoutViewModel>();

                  final workout = workoutViewModel.workout;

                  if (workout == null) {
                    return;
                  }

                  final basePath =
                  isAdmin ? '/admin' : '/professor';

                  final updated = await context.push<bool>(
                    '$basePath/students/${student.id}/'
                        'workouts/edit',
                    extra: {
                      'workout': workout,
                      'studentName': student.name,
                    },
                  );

                  if (!context.mounted || updated != true) {
                    return;
                  }

                  await workoutViewModel.load();
                },
              ),

              const SizedBox(height: 12),

              OutlinedButton.icon(
                onPressed: () {
                  final basePath =
                  isAdmin ? '/admin' : '/professor';

                  context.push(
                    '$basePath/students/${student.id}/'
                        'workouts/history',
                    extra: student.name,
                  );
                },
                icon: const Icon(
                  Icons.history_rounded,
                ),
                label: const Text(
                  'Ver histórico de treinos',
                ),
              ),

              const SizedBox(height: 20),

              PhysicalAssessmentSummaryCard(
                onHistoryTap: () {
                  final basePath = isAdmin ? '/admin' : '/professor';

                  context.push(
                    '$basePath/students/${student.id}/physical-assessments',
                    extra: student.name,
                  );
                },
                onNewAssessmentTap: () async {
                  final basePath =
                  isAdmin ? '/admin' : '/professor';

                  final created = await context.push<bool>(
                    '$basePath/students/${student.id}/'
                        'physical-assessments/new',
                    extra: student.name,
                  );

                  if (created == true && context.mounted) {
                    await context
                        .read<PhysicalAssessmentSummaryViewModel>()
                        .load();
                  }
                },
              ),

              const SizedBox(height: 20),
              _SectionCard(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Row(
                      children: [
                        Icon(
                          Icons.badge_outlined,
                          color: colorScheme.primary,
                        ),
                        const SizedBox(width: 10),
                        Text(
                          'Dados pessoais',
                          style: theme.textTheme.titleLarge?.copyWith(
                            fontWeight: FontWeight.w800,
                          ),
                        ),
                      ],
                    ),

                    const SizedBox(height: 20),

                    _DataRow(
                      label: 'Data de nascimento',
                      value: student.birthDate == null
                          ? 'Não informada'
                          : _formatDate(student.birthDate!),
                    ),

                    const Divider(height: 28),

                    _DataRow(
                      label: 'Idade',
                      value: student.birthDate == null
                          ? 'Não informada'
                          : '${_calculateAge(student.birthDate!)} anos',
                    ),

                    const Divider(height: 28),

                    _DataRow(
                      label: 'Status',
                      value: student.isActive
                          ? 'Ativo'
                          : 'Inativo',
                    ),
                  ],
                ),
              ),

              const SizedBox(height: 20),

            ],
          ),
        ),
      ),
    );
  }

  Future<void> _confirmStatusChange(
      BuildContext context,
      ) async {
    final willActivate = !student.isActive;

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) {
        return AlertDialog(
          title: Text(
            willActivate
                ? 'Ativar aluno?'
                : 'Desativar aluno?',
          ),
          content: Text(
            willActivate
                ? 'O aluno voltará a ficar ativo no GymFlow.'
                : 'O aluno ficará inativo no GymFlow. '
                'Os dados cadastrados não serão excluídos.',
          ),
          actions: [
            TextButton(
              onPressed: () {
                Navigator.of(dialogContext).pop(false);
              },
              child: const Text('Cancelar'),
            ),
            FilledButton(
              onPressed: () {
                Navigator.of(dialogContext).pop(true);
              },
              style: !willActivate
                  ? FilledButton.styleFrom(
                backgroundColor:
                Theme.of(context).colorScheme.error,
              )
                  : null,
              child: Text(
                willActivate
                    ? 'Ativar aluno'
                    : 'Desativar aluno',
              ),
            ),
          ],
        );
      },
    );

    if (confirmed == true) {
      onStatusChanged(willActivate);
    }
  }

  String _initials(String name) {
    final parts = name
        .trim()
        .split(RegExp(r'\s+'))
        .where((part) => part.isNotEmpty)
        .toList();

    if (parts.isEmpty) {
      return '?';
    }

    if (parts.length == 1) {
      return parts.first.substring(0, 1).toUpperCase();
    }

    return '${parts.first.substring(0, 1)}'
        '${parts.last.substring(0, 1)}'
        .toUpperCase();
  }

  String _phoneText(String? phone) {
    final value = phone?.trim();

    if (value == null || value.isEmpty) {
      return 'Telefone não informado';
    }

    return value;
  }

  String _formatDate(DateTime date) {
    final day = date.day.toString().padLeft(2, '0');
    final month = date.month.toString().padLeft(2, '0');

    return '$day/$month/${date.year}';
  }

  int _calculateAge(DateTime birthDate) {
    final today = DateTime.now();

    var age = today.year - birthDate.year;

    final birthdayHasNotOccurred =
        today.month < birthDate.month ||
            (today.month == birthDate.month &&
                today.day < birthDate.day);

    if (birthdayHasNotOccurred) {
      age--;
    }

    return age;
  }
}

class _StatusBadge extends StatelessWidget {
  const _StatusBadge({
    required this.isActive,
  });

  final bool isActive;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    final color = isActive
        ? const Color(0xFF22C55E)
        : colorScheme.error;

    return Align(
      alignment: Alignment.centerLeft,
      child: Container(
        padding: const EdgeInsets.symmetric(
          horizontal: 10,
          vertical: 6,
        ),
        decoration: BoxDecoration(
          color: color.withAlpha(24),
          borderRadius: BorderRadius.circular(9),
        ),
        child: Text(
          isActive ? 'Ativo' : 'Inativo',
          style: TextStyle(
            color: color,
            fontWeight: FontWeight.w800,
          ),
        ),
      ),
    );
  }
}

class _InfoLine extends StatelessWidget {
  const _InfoLine({
    required this.icon,
    required this.text,
  });

  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Row(
      children: [
        Icon(
          icon,
          size: 19,
          color: colorScheme.primary,
        ),
        const SizedBox(width: 9),
        Expanded(
          child: Text(
            text,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(
              color: colorScheme.onSurfaceVariant,
            ),
          ),
        ),
      ],
    );
  }
}

class _SectionCard extends StatelessWidget {
  const _SectionCard({
    required this.child,
  });

  final Widget child;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(
          color: colorScheme.outlineVariant.withAlpha(120),
        ),
      ),
      child: child,
    );
  }
}

class _DataRow extends StatelessWidget {
  const _DataRow({
    required this.label,
    required this.value,
  });

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Row(
      children: [
        Expanded(
          child: Text(
            label,
            style: theme.textTheme.bodyMedium?.copyWith(
              color: colorScheme.onSurfaceVariant,
            ),
          ),
        ),
        const SizedBox(width: 16),
        Text(
          value,
          textAlign: TextAlign.end,
          style: theme.textTheme.bodyMedium?.copyWith(
            fontWeight: FontWeight.w700,
          ),
        ),
      ],
    );
  }
}

class _ErrorState extends StatelessWidget {
  const _ErrorState({
    required this.message,
    required this.onRetry,
  });

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Center(
      child: Padding(
        padding: const EdgeInsets.all(32),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(
              Icons.error_outline_rounded,
              size: 48,
              color: colorScheme.onSurfaceVariant,
            ),
            const SizedBox(height: 14),
            Text(
              message,
              textAlign: TextAlign.center,
              style: theme.textTheme.bodyLarge,
            ),
            const SizedBox(height: 18),
            FilledButton(
              onPressed: onRetry,
              child: const Text('Tentar novamente'),
            ),
          ],
        ),
      ),
    );
  }
}
