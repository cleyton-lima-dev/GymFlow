import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';
import 'package:avelri_gestao/features/plans/data/plans_service.dart';
import 'package:avelri_gestao/features/plans/models/plan_summary.dart';
import 'package:avelri_gestao/app/theme/branding_controller.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/students/data/students_service.dart';
import 'package:avelri_gestao/features/students/presentation/student_details_view_model.dart';

class StudentDetailsPage extends StatelessWidget {
  const StudentDetailsPage({required this.studentId, super.key});

  final String studentId;

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) => StudentDetailsViewModel(
        StudentsService(context.read<ApiClient>()),
        studentId,
      )..load(),
      child: const _StudentDetailsView(),
    );
  }
}

class _StudentDetailsView extends StatelessWidget {
  const _StudentDetailsView();

  @override
  Widget build(BuildContext context) {
    final viewModel = context.watch<StudentDetailsViewModel>();
    final primaryColor = context
        .watch<BrandingController>()
        .branding
        .primaryColor;

    if (viewModel.isLoading && viewModel.student == null) {
      return const Center(child: CircularProgressIndicator());
    }

    if (viewModel.errorMessage != null && viewModel.student == null) {
      return _ErrorState(
        message: viewModel.errorMessage!,
        onRetry: viewModel.load,
      );
    }

    final student = viewModel.student;

    if (student == null) {
      return const SizedBox.shrink();
    }

    return SingleChildScrollView(
      padding: const EdgeInsets.all(30),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 1050),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                IconButton(
                  tooltip: 'Voltar',
                  onPressed: () => context.go('/students'),
                  icon: const Icon(Icons.arrow_back_rounded),
                ),
                const SizedBox(width: 8),
                const Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Detalhes do aluno',
                        style: TextStyle(
                          color: Color(0xFF171A2C),
                          fontSize: 28,
                          fontWeight: FontWeight.w800,
                          letterSpacing: -0.5,
                        ),
                      ),
                      SizedBox(height: 7),
                      Text(
                        'Consulte os dados cadastrais e o status do aluno.',
                        style: TextStyle(
                          color: Color(0xFF74798D),
                          fontSize: 14,
                        ),
                      ),
                    ],
                  ),
                ),
                Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    student.archivedAt != null
                        ? FilledButton.icon(
                      onPressed: viewModel.isReactivating
                          ? null
                          : () => _showReactivateArchivedStudentDialog(
                        context,
                        viewModel,
                      ),
                      icon: viewModel.isReactivating
                          ? const SizedBox(
                        width: 16,
                        height: 16,
                        child: CircularProgressIndicator(
                          strokeWidth: 2,
                        ),
                      )
                          : const Icon(Icons.restore_rounded),
                      label: const Text('Reativar aluno'),
                    )
                        : OutlinedButton.icon(
                      style: OutlinedButton.styleFrom(
                        foregroundColor: student.isActive
                            ? const Color(0xFFB54752)
                            : primaryColor,
                        side: BorderSide(
                          color: student.isActive
                              ? const Color(0xFFB54752)
                              : primaryColor,
                        ),
                        padding: const EdgeInsets.symmetric(
                          horizontal: 18,
                          vertical: 14,
                        ),
                      ),
                      onPressed: viewModel.isUpdatingStatus
                          ? null
                          : () async {
                        final newStatus = !student.isActive;

                        final confirmed = await showDialog<bool>(
                          context: context,
                          builder: (dialogContext) {
                            return AlertDialog(
                              title: Text(
                                newStatus
                                    ? 'Ativar aluno?'
                                    : 'Inativar aluno?',
                              ),
                              content: Text(
                                newStatus
                                    ? 'O aluno voltará a ficar ativo no Avelri.'
                                    : 'O aluno ficará inativo até ser reativado.',
                              ),
                              actions: [
                                TextButton(
                                  onPressed: () {
                                    Navigator.of(dialogContext)
                                        .pop(false);
                                  },
                                  child: const Text('Cancelar'),
                                ),
                                FilledButton(
                                  onPressed: () {
                                    Navigator.of(dialogContext)
                                        .pop(true);
                                  },
                                  child: Text(
                                    newStatus
                                        ? 'Ativar'
                                        : 'Inativar',
                                  ),
                                ),
                              ],
                            );
                          },
                        );

                        if (confirmed != true ||
                            !context.mounted) {
                          return;
                        }

                        final success =
                        await viewModel.updateStatus(
                          newStatus,
                        );

                        if (!context.mounted || !success) {
                          return;
                        }

                        ScaffoldMessenger.of(context)
                            .showSnackBar(
                          SnackBar(
                            content: Text(
                              newStatus
                                  ? 'Aluno ativado com sucesso.'
                                  : 'Aluno inativado com sucesso.',
                            ),
                          ),
                        );
                      },
                      icon: viewModel.isUpdatingStatus
                          ? const SizedBox(
                        width: 16,
                        height: 16,
                        child: CircularProgressIndicator(
                          strokeWidth: 2,
                        ),
                      )
                          : Icon(
                        student.isActive
                            ? Icons.block_outlined
                            : Icons.check_circle_outline,
                      ),
                      label: Text(
                        student.isActive
                            ? 'Inativar aluno'
                            : 'Ativar aluno',
                      ),
                    ),
                    const SizedBox(width: 12),
                    FilledButton.icon(
                      style: FilledButton.styleFrom(
                        backgroundColor: primaryColor,
                        padding: const EdgeInsets.symmetric(
                          horizontal: 18,
                          vertical: 14,
                        ),
                      ),
                      onPressed: () {
                        context.go('/students/${student.id}/edit');
                      },
                      icon: const Icon(Icons.edit_outlined),
                      label: const Text('Editar aluno'),
                    ),
                  ],
                ),
              ],
            ),
            const SizedBox(height: 26),
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(26),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(18),
                border: Border.all(color: const Color(0xFFE5E7EF)),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      CircleAvatar(
                        radius: 30,
                        backgroundColor: primaryColor.withValues(alpha: 0.10),
                        child: Text(
                          _initials(student.name),
                          style: TextStyle(
                            color: primaryColor,
                            fontSize: 16,
                            fontWeight: FontWeight.w800,
                          ),
                        ),
                      ),
                      const SizedBox(width: 16),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              student.name,
                              style: const TextStyle(
                                color: Color(0xFF202336),
                                fontSize: 20,
                                fontWeight: FontWeight.w800,
                              ),
                            ),
                            const SizedBox(height: 5),
                            Text(
                              student.email,
                              style: const TextStyle(
                                color: Color(0xFF74798D),
                                fontSize: 13,
                              ),
                            ),
                          ],
                        ),
                      ),
                      _StatusBadge(isActive: student.isActive),
                    ],
                  ),
                  const SizedBox(height: 26),
                  const Divider(color: Color(0xFFE8EAF1)),
                  const SizedBox(height: 24),
                  _SectionTitle(
                    icon: Icons.badge_outlined,
                    title: 'Dados cadastrais',
                    color: primaryColor,
                  ),
                  const SizedBox(height: 22),
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Expanded(
                        child: _InfoItem(
                          label: 'Nome completo',
                          value: student.name,
                          icon: Icons.person_outline_rounded,
                        ),
                      ),
                      const SizedBox(width: 18),
                      Expanded(
                        child: _InfoItem(
                          label: 'E-mail',
                          value: student.email,
                          icon: Icons.mail_outline_rounded,
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 18),
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Expanded(
                        child: _InfoItem(
                          label: 'Telefone',
                          value: student.phone ?? 'Não informado',
                          icon: Icons.phone_outlined,
                        ),
                      ),
                      const SizedBox(width: 18),
                      Expanded(
                        child: _InfoItem(
                          label: 'Data de nascimento',
                          value: student.birthDate == null
                              ? 'Não informada'
                              : _formatDate(student.birthDate!),
                          icon: Icons.calendar_month_outlined,
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 18),
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Expanded(
                        child: _InfoItem(
                          label: 'Status',
                          value: student.isActive
                              ? 'Aluno ativo'
                              : 'Aluno inativo',
                          icon: student.isActive
                              ? Icons.check_circle_outline_rounded
                              : Icons.block_rounded,
                        ),
                      ),
                      const SizedBox(width: 18),
                      Expanded(
                        child: _InfoItem(
                          label: 'Cadastrado em',
                          value: _formatDate(student.createdAt),
                          icon: Icons.event_available_outlined,
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
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

class _InfoItem extends StatelessWidget {
  const _InfoItem({
    required this.label,
    required this.value,
    required this.icon,
  });

  final String label;
  final String value;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(17),
      decoration: BoxDecoration(
        color: const Color(0xFFF7F8FB),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: const Color(0xFFE8EAF1)),
      ),
      child: Row(
        children: [
          Icon(icon, size: 20, color: const Color(0xFF74798D)),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  label,
                  style: const TextStyle(
                    color: Color(0xFF7B8093),
                    fontSize: 11,
                    fontWeight: FontWeight.w600,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  value,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    color: Color(0xFF292C3E),
                    fontSize: 13,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _SectionTitle extends StatelessWidget {
  const _SectionTitle({
    required this.icon,
    required this.title,
    required this.color,
  });

  final IconData icon;
  final String title;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Container(
          width: 38,
          height: 38,
          decoration: BoxDecoration(
            color: color.withValues(alpha: 0.10),
            borderRadius: BorderRadius.circular(10),
          ),
          child: Icon(icon, color: color, size: 20),
        ),
        const SizedBox(width: 12),
        Text(
          title,
          style: const TextStyle(
            color: Color(0xFF24273A),
            fontSize: 16,
            fontWeight: FontWeight.w800,
          ),
        ),
      ],
    );
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
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 7),
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

class _ErrorState extends StatelessWidget {
  const _ErrorState({required this.message, required this.onRetry});

  final String message;
  final Future<void> Function() onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(40),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(
              Icons.error_outline_rounded,
              color: Color(0xFFB54752),
              size: 40,
            ),
            const SizedBox(height: 14),
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: 16),
            OutlinedButton(
              onPressed: onRetry,
              child: const Text('Tentar novamente'),
            ),
          ],
        ),
      ),
    );
  }
}
Future<void> _showReactivateArchivedStudentDialog(
    BuildContext context,
    StudentDetailsViewModel viewModel,
    ) async {
  final plansService = PlansService(
    context.read<ApiClient>(),
  );

  final plans = await plansService.getPlans(
    isActive: true,
  );

  if (!context.mounted) return;

  if (plans.isEmpty) {
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(
        content: Text(
          'Nenhum plano ativo disponível.',
        ),
      ),
    );
    return;
  }

  PlanSummary selectedPlan = plans.first;
  DateTime startDate = DateTime.now();

  final confirmed = await showDialog<bool>(
    context: context,
    builder: (dialogContext) {
      return StatefulBuilder(
        builder: (context, setState) {
          return AlertDialog(
            title: const Text(
              'Reativar aluno arquivado',
            ),
            content: SizedBox(
              width: 380,
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  DropdownButtonFormField<PlanSummary>(
                    initialValue: selectedPlan,
                    decoration: const InputDecoration(
                      labelText: 'Plano',
                    ),
                    items: plans
                        .map(
                          (plan) => DropdownMenuItem(
                        value: plan,
                        child: Text(plan.name),
                      ),
                    )
                        .toList(),
                    onChanged: (plan) {
                      if (plan == null) return;

                      setState(() {
                        selectedPlan = plan;
                      });
                    },
                  ),
                  const SizedBox(height: 18),
                  ListTile(
                    contentPadding: EdgeInsets.zero,
                    title: const Text(
                      'Data de início',
                    ),
                    subtitle: Text(
                      _formatDialogDate(startDate),
                    ),
                    trailing: const Icon(
                      Icons.calendar_month_outlined,
                    ),
                    onTap: () async {
                      final selectedDate =
                      await showDatePicker(
                        context: context,
                        initialDate: startDate,
                        firstDate: DateTime.now(),
                        lastDate: DateTime.now().add(
                          const Duration(days: 365),
                        ),
                      );

                      if (selectedDate == null) return;

                      setState(() {
                        startDate = selectedDate;
                      });
                    },
                  ),
                ],
              ),
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
                child: const Text('Reativar'),
              ),
            ],
          );
        },
      );
    },
  );

  if (confirmed != true || !context.mounted) {
    return;
  }

  final success =
  await viewModel.reactivateArchivedStudent(
    planId: selectedPlan.id,
    startDate: startDate,
  );

  if (!context.mounted) return;

  ScaffoldMessenger.of(context).showSnackBar(
    SnackBar(
      content: Text(
        success
            ? 'Aluno reativado com sucesso.'
            : 'Não foi possível reativar o aluno.',
      ),
    ),
  );
}

String _formatDialogDate(DateTime date) {
  final day = date.day.toString().padLeft(2, '0');
  final month = date.month.toString().padLeft(2, '0');

  return '$day/$month/${date.year}';
}
