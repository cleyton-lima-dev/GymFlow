import 'package:avelri_gestao/app/theme/branding_controller.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/enrollments/data/enrollments_service.dart';
import 'package:avelri_gestao/features/enrollments/presentation/enrollments_view_model.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:avelri_gestao/features/enrollments/models/enrollment_summary.dart';
import 'package:intl/intl.dart';
import 'package:go_router/go_router.dart';
import 'package:avelri_gestao/features/plans/data/plans_service.dart';

class EnrollmentsPage extends StatelessWidget {
  const EnrollmentsPage({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) => EnrollmentsViewModel(
        EnrollmentsService(
          context.read<ApiClient>(),
        ),
      )..loadInitial(),
      child: const _EnrollmentsView(),
    );
  }
}

class _EnrollmentsView extends StatelessWidget {
  const _EnrollmentsView();

  @override
  Widget build(BuildContext context) {
    final viewModel =
    context.watch<EnrollmentsViewModel>();

    final branding =
        context.watch<BrandingController>().branding;

    final primaryColor = branding.primaryColor;
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return SingleChildScrollView(
      padding: const EdgeInsets.all(30),
      child: ConstrainedBox(
        constraints:
        const BoxConstraints(maxWidth: 1250),
        child: Column(
          crossAxisAlignment:
          CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment:
                    CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Matrículas',
                        style: TextStyle(
                          color: colorScheme.onSurface,
                          fontSize: 28,
                          fontWeight: FontWeight.w800,
                        ),
                      ),
                      SizedBox(height: 7),
                      Text(
                        'Gerencie os vínculos dos alunos com os planos da academia.',
                        style: TextStyle(
                          color: colorScheme.onSurfaceVariant,
                          fontSize: 14,
                        ),
                      ),
                    ],
                  ),
                ),
                FilledButton.icon(
                  onPressed: () => context.go('/enrollments/new'),
                  icon: const Icon(
                    Icons.add_rounded,
                  ),
                  label: const Text(
                    'Nova matrícula',
                  ),
                  style: FilledButton.styleFrom(
                    backgroundColor: primaryColor,
                    foregroundColor: Colors.white,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 26),
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(22),
              decoration: BoxDecoration(
                color: colorScheme.surface,
                borderRadius:
                BorderRadius.circular(18),
                border: Border.all(
                  color: theme.dividerColor,
                ),
              ),
              child: Column(
                children: [
                  Row(
                    children: [
                      _FilterButton(
                        label: 'Todas',
                        selected:
                        viewModel.statusFilter ==
                            EnrollmentsStatusFilter.all,
                        color: primaryColor,
                        onTap: () {
                          viewModel.updateStatusFilter(
                            EnrollmentsStatusFilter.all,
                          );
                        },
                      ),
                      const SizedBox(width: 8),
                      _FilterButton(
                        label: 'Ativas',
                        selected:
                        viewModel.statusFilter ==
                            EnrollmentsStatusFilter.active,
                        color: primaryColor,
                        onTap: () {
                          viewModel.updateStatusFilter(
                            EnrollmentsStatusFilter.active,
                          );
                        },
                      ),
                      const SizedBox(width: 8),
                      _FilterButton(
                        label: 'Canceladas',
                        selected:
                        viewModel.statusFilter ==
                            EnrollmentsStatusFilter.cancelled,
                        color: primaryColor,
                        onTap: () {
                          viewModel.updateStatusFilter(
                            EnrollmentsStatusFilter.cancelled,
                          );
                        },
                      ),
                      const SizedBox(width: 8),
                      _FilterButton(
                        label: 'Vencidas',
                        selected:
                        viewModel.statusFilter ==
                            EnrollmentsStatusFilter.expired,
                        color: primaryColor,
                        onTap: () {
                          viewModel.updateStatusFilter(
                            EnrollmentsStatusFilter.expired,
                          );
                        },
                      ),
                      const Spacer(),
                      IconButton(
                        tooltip: 'Atualizar',
                        onPressed:
                        viewModel.isLoading
                            ? null
                            : viewModel.refresh,
                        icon: const Icon(
                          Icons.refresh_rounded,
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 20),
                  if (viewModel.isLoading &&
                      !viewModel.hasEnrollments)
                    const Padding(
                      padding:
                      EdgeInsets.symmetric(vertical: 70),
                      child: Center(
                        child: CircularProgressIndicator(),
                      ),
                    )
                  else if (viewModel.errorMessage != null)
                    _ErrorState(
                      message: viewModel.errorMessage!,
                      onRetry: viewModel.retry,
                    )
                  else if (!viewModel.hasEnrollments)
                      const _EmptyState()
                    else
                      _EnrollmentsTable(
                        enrollments: viewModel.enrollments,
                      ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
class _EnrollmentsTable extends StatelessWidget {
  const _EnrollmentsTable({
    required this.enrollments,
  });

  final List<EnrollmentSummary> enrollments;

  @override
  Widget build(BuildContext context) {
    final currency = NumberFormat.currency(
      locale: 'pt_BR',
      symbol: 'R\$',
    );

    final dateFormat = DateFormat('dd/MM/yyyy');
    final colorScheme = Theme.of(context).colorScheme;

    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      child: DataTable(
        columns: const [
          DataColumn(label: Text('Aluno')),
          DataColumn(label: Text('Plano')),
          DataColumn(label: Text('Período')),
          DataColumn(label: Text('Status')),
          DataColumn(label: Text('Ações')),
        ],
        rows: enrollments.map((enrollment) {
          return DataRow(
            cells: [
              DataCell(
                Text(enrollment.studentName),
              ),
              DataCell(
                Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      enrollment.planName,
                      style: const TextStyle(
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      currency.format(
                        enrollment.planPrice,
                      ),
                      style: TextStyle(
                        color: colorScheme.onSurfaceVariant,
                        fontSize: 12,
                      ),
                    ),
                  ],
                ),
              ),
              DataCell(
                Text(
                  '${dateFormat.format(enrollment.startDate)} → '
                      '${dateFormat.format(enrollment.endDate)}',
                ),
              ),
              DataCell(
                Text(
                  _statusLabel(
                    enrollment.status,
                  ),
                ),
              ),
              DataCell(
                PopupMenuButton<String>(
                  tooltip: 'Ações',
                  icon: const Icon(
                    Icons.more_vert_rounded,
                  ),
                  onSelected: (action) async {
                    if (action == 'renew') {
                      await _renewEnrollment(
                        context,
                        enrollment,
                      );
                      return;
                    }

                    if (action == 'cancel') {
                      await _confirmCancellation(
                        context,
                        enrollment,
                      );
                    }
                  },
                  itemBuilder: (context) => [
                    const PopupMenuItem(
                      value: 'renew',
                      child: ListTile(
                        leading: Icon(
                          Icons.autorenew_rounded,
                        ),
                        title: Text('Renovar'),
                        contentPadding: EdgeInsets.zero,
                      ),
                    ),
                    if (enrollment.status ==
                        EnrollmentStatus.active ||
                        enrollment.status ==
                            EnrollmentStatus.pendingPayment)
                      const PopupMenuItem(
                        value: 'cancel',
                        child: ListTile(
                          leading: Icon(
                            Icons.cancel_outlined,
                          ),
                          title: Text(
                            'Cancelar matrícula',
                          ),
                          contentPadding: EdgeInsets.zero,
                        ),
                      ),
                  ],
                ),
              ),
            ],
          );
        }).toList(),
      ),
    );
  }

  Future<void> _renewEnrollment(
      BuildContext context,
      EnrollmentSummary enrollment,
      ) async {
    final plansService = PlansService(
      context.read<ApiClient>(),
    );

    final plans = await plansService.getPlans(
      isActive: true,
    );

    if (!context.mounted) {
      return;
    }

    String? selectedPlanId;

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) {
        return StatefulBuilder(
          builder: (context, setState) {
            return AlertDialog(
              title: const Text(
                'Renovar matrícula',
              ),
              content: SizedBox(
                width: 420,
                child: DropdownButtonFormField<String>(
                  decoration: const InputDecoration(
                    labelText: 'Plano',
                  ),
                  items: plans
                      .map(
                        (plan) => DropdownMenuItem(
                      value: plan.id,
                      child: Text(plan.name),
                    ),
                  )
                      .toList(),
                  onChanged: (value) {
                    setState(() {
                      selectedPlanId = value;
                    });
                  },
                ),
              ),
              actions: [
                TextButton(
                  onPressed: () =>
                      Navigator.of(dialogContext).pop(false),
                  child: const Text('Voltar'),
                ),
                FilledButton(
                  onPressed: selectedPlanId == null
                      ? null
                      : () =>
                      Navigator.of(dialogContext).pop(true),
                  child: const Text('Renovar'),
                ),
              ],
            );
          },
        );
      },
    );

    if (confirmed != true ||
        selectedPlanId == null ||
        !context.mounted) {
      return;
    }

    await context
        .read<EnrollmentsViewModel>()
        .renewEnrollment(
      enrollment,
      selectedPlanId!,
    );
  }

  Future<void> _confirmCancellation(
      BuildContext context,
      EnrollmentSummary enrollment,
      ) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) {
        return AlertDialog(
          title: const Text(
            'Cancelar matrícula',
          ),
          content: Text(
            'Deseja cancelar a matrícula de '
                '${enrollment.studentName}?',
          ),
          actions: [
            TextButton(
              onPressed: () =>
                  Navigator.of(dialogContext).pop(false),
              child: const Text('Voltar'),
            ),
            FilledButton(
              onPressed: () =>
                  Navigator.of(dialogContext).pop(true),
              child: const Text('Cancelar matrícula'),
            ),
          ],
        );
      },
    );

    if (confirmed != true || !context.mounted) {
      return;
    }

    await context
        .read<EnrollmentsViewModel>()
        .cancelEnrollment(enrollment);
  }

  String _statusLabel(
      EnrollmentStatus status,
      ) {
    return switch (status) {
      EnrollmentStatus.active => 'Ativa',
      EnrollmentStatus.cancelled => 'Cancelada',
      EnrollmentStatus.expired => 'Vencida',
      EnrollmentStatus.pendingPayment => 'Aguardando pagamento',
    };
  }
}

class _EmptyState extends StatelessWidget {
  const _EmptyState();

  @override
  Widget build(BuildContext context) {
    return const Padding(
      padding: EdgeInsets.symmetric(vertical: 70),
      child: Center(
        child: Text(
          'Nenhuma matrícula encontrada.',
          style: TextStyle(
            color: Color(0xFF74798D),
            fontWeight: FontWeight.w600,
          ),
        ),
      ),
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
    return Padding(
      padding: const EdgeInsets.symmetric(
        vertical: 60,
      ),
      child: Column(
        children: [
          Text(
            message,
            textAlign: TextAlign.center,
          ),
          const SizedBox(height: 14),
          OutlinedButton(
            onPressed: onRetry,
            child: const Text(
              'Tentar novamente',
            ),
          ),
        ],
      ),
    );
  }
}
class _FilterButton extends StatelessWidget {
  const _FilterButton({
    required this.label,
    required this.selected,
    required this.color,
    required this.onTap,
  });

  final String label;
  final bool selected;
  final Color color;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return OutlinedButton(
      onPressed: onTap,
      style: OutlinedButton.styleFrom(
        foregroundColor:
        selected ? Colors.white : colorScheme.onSurfaceVariant,
        backgroundColor:
        selected ? color : colorScheme.surface,
        side: BorderSide(
          color: selected ? color : theme.dividerColor,
        ),
      ),
      child: Text(label),
    );
  }
}
