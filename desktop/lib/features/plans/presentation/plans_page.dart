import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:provider/provider.dart';
import 'package:avelri_gestao/app/theme/branding_controller.dart';
import 'package:avelri_gestao/app/theme/semantic_colors.dart';
import 'package:avelri_gestao/core/widgets/app_status_states.dart';
import 'package:avelri_gestao/core/widgets/app_filter_button.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/plans/data/plans_service.dart';
import 'package:avelri_gestao/features/plans/models/plan_summary.dart';
import 'package:avelri_gestao/features/plans/presentation/plans_view_model.dart';
import 'package:go_router/go_router.dart';

class PlansPage extends StatelessWidget {
  const PlansPage({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) =>
          PlansViewModel(PlansService(context.read<ApiClient>()))
            ..loadInitial(),
      child: const _PlansView(),
    );
  }
}

class _PlansView extends StatelessWidget {
  const _PlansView();

  @override
  Widget build(BuildContext context) {
    final viewModel = context.watch<PlansViewModel>();
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
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Planos',
                        style: TextStyle(
                          color: colorScheme.onSurface,
                          fontSize: 28,
                          fontWeight: FontWeight.w800,
                          letterSpacing: -0.5,
                        ),
                      ),
                      SizedBox(height: 7),
                      Text(
                        'Gerencie os planos oferecidos pela academia.',
                        style: TextStyle(
                          color: colorScheme.onSurfaceVariant,
                          fontSize: 14,
                        ),
                      ),
                    ],
                  ),
                ),
                FilledButton.icon(
                  onPressed: () => context.go('/plans/new'),
                  icon: const Icon(Icons.add_rounded),
                  label: const Text('Cadastrar plano'),
                ),
              ],
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
                      AppFilterButton(
                        label: 'Todos',
                        selected:
                            viewModel.statusFilter == PlansStatusFilter.all,
                        color: primaryColor,
                        onPressed: () {
                          viewModel.updateStatusFilter(PlansStatusFilter.all);
                        },
                      ),
                      const SizedBox(width: 8),
                      AppFilterButton(
                        label: 'Ativos',
                        selected:
                            viewModel.statusFilter == PlansStatusFilter.active,
                        color: primaryColor,
                        onPressed: () {
                          viewModel.updateStatusFilter(
                            PlansStatusFilter.active,
                          );
                        },
                      ),
                      const SizedBox(width: 8),
                      AppFilterButton(
                        label: 'Inativos',
                        selected:
                            viewModel.statusFilter ==
                            PlansStatusFilter.inactive,
                        color: primaryColor,
                        onPressed: () {
                          viewModel.updateStatusFilter(
                            PlansStatusFilter.inactive,
                          );
                        },
                      ),
                      const Spacer(),
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
                  if (viewModel.isLoading && !viewModel.hasPlans)
                    const Padding(
                      padding: EdgeInsets.symmetric(vertical: 70),
                      child: CircularProgressIndicator(),
                    )
                  else if (viewModel.errorMessage != null)
                    _ErrorState(
                      message: viewModel.errorMessage!,
                      onRetry: viewModel.retry,
                    )
                  else if (!viewModel.hasPlans)
                    const _EmptyState()
                  else
                    _PlansTable(
                      plans: viewModel.plans,
                      primaryColor: primaryColor,
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

class _PlansTable extends StatelessWidget {
  const _PlansTable({required this.plans, required this.primaryColor});

  final List<PlanSummary> plans;
  final Color primaryColor;

  @override
  Widget build(BuildContext context) {
    final currency = NumberFormat.currency(locale: 'pt_BR', symbol: 'R\$');
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return SizedBox(
      width: double.infinity,
      child: DataTable(
        columns: const [
          DataColumn(label: Text('Plano')),
          DataColumn(label: Text('Valor')),
          DataColumn(label: Text('Duração')),
          DataColumn(label: Text('Cobrança')),
          DataColumn(label: Text('Status')),
          DataColumn(label: Text('Ações')),
        ],
        rows: plans.map((plan) {
          return DataRow(
            cells: [
              DataCell(Text(plan.name)),
              DataCell(Text(currency.format(plan.price))),
              DataCell(
                Text(
                  '${plan.durationMonths} '
                  '${plan.durationMonths == 1 ? 'mês' : 'meses'}',
                ),
              ),
              DataCell(Text(_billingCycleLabel(plan.billingCycle))),
              DataCell(
                Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 12,
                    vertical: 6,
                  ),
                  decoration: BoxDecoration(
                    color: plan.isActive
                        ? AvelriSemanticColors.success.withValues(
                            alpha: theme.brightness == Brightness.dark
                                ? 0.16
                                : 0.10,
                          )
                        : colorScheme.onSurfaceVariant.withValues(alpha: 0.10),
                    borderRadius: BorderRadius.circular(20),
                  ),
                  child: Text(
                    plan.isActive ? 'Ativo' : 'Inativo',
                    style: TextStyle(
                      color: plan.isActive
                          ? colorScheme.onSurface
                          : colorScheme.onSurfaceVariant,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
              ),
              DataCell(
                Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    IconButton(
                      tooltip: 'Editar plano',
                      onPressed: () => context.go('/plans/edit', extra: plan),
                      icon: const Icon(Icons.edit_outlined),
                    ),
                    IconButton(
                      tooltip: plan.isActive
                          ? 'Inativar plano'
                          : 'Ativar plano',
                      onPressed: () async {
                        await context.read<PlansViewModel>().updatePlanStatus(
                          plan,
                        );
                      },
                      icon: Icon(
                        plan.isActive
                            ? Icons.block_outlined
                            : Icons.check_circle_outline,
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

  String _billingCycleLabel(PlanBillingCycle cycle) {
    return switch (cycle) {
      PlanBillingCycle.monthly => 'Mensal',
      PlanBillingCycle.quarterly => 'Trimestral',
      PlanBillingCycle.semiannual => 'Semestral',
      PlanBillingCycle.annual => 'Anual',
    };
  }
}

class _EmptyState extends StatelessWidget {
  const _EmptyState();

  @override
  Widget build(BuildContext context) {
    return const AppEmptyState(
      icon: Icons.credit_card_rounded,
      title: 'Nenhum plano encontrado.',
    );
  }
}

class _ErrorState extends StatelessWidget {
  const _ErrorState({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return AppErrorState(
      message: message,
      onRetry: onRetry,
      padding: const EdgeInsets.symmetric(vertical: 60),
    );
  }
}
