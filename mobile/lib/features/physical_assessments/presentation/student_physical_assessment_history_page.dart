import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_brand_header.dart';
import 'package:gymflow/features/home/presentation/widgets/student_bottom_navigation.dart';
import 'package:gymflow/features/physical_assessments/data/physical_assessments_service.dart';
import 'package:gymflow/features/physical_assessments/models/physical_assessment_history_item.dart';
import 'package:gymflow/features/physical_assessments/presentation/physical_assessment_history_view_model.dart';
import 'package:provider/provider.dart';

class StudentPhysicalAssessmentHistoryPage extends StatelessWidget {
  const StudentPhysicalAssessmentHistoryPage({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) => PhysicalAssessmentHistoryViewModel.forCurrentUser(
        PhysicalAssessmentsService(context.read<ApiClient>()),
      )..loadInitial(),
      child: const _StudentPhysicalAssessmentHistoryView(),
    );
  }
}

class _StudentPhysicalAssessmentHistoryView extends StatelessWidget {
  const _StudentPhysicalAssessmentHistoryView();

  @override
  Widget build(BuildContext context) {
    final viewModel = context.watch<PhysicalAssessmentHistoryViewModel>();

    final latestId = viewModel.latest?.id;

    return Scaffold(
      body: SafeArea(
        bottom: false,
        child: RefreshIndicator(
          onRefresh: viewModel.refresh,
          child: ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.fromLTRB(24, 12, 24, 32),
            children: [
              Center(
                child: ConstrainedBox(
                  constraints: const BoxConstraints(maxWidth: 680),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      SizedBox(
                        height: 82,
                        child: Stack(
                          alignment: Alignment.center,
                          children: [
                            const ProfessorAdminBrandHeader(height: 72),
                            Align(
                              alignment: Alignment.centerLeft,
                              child: TextButton.icon(
                                onPressed: () {
                                  context.pop();
                                },
                                icon: const Icon(Icons.chevron_left_rounded),
                                label: const Text('Voltar'),
                                style: TextButton.styleFrom(
                                  padding: EdgeInsets.zero,
                                ),
                              ),
                            ),
                          ],
                        ),
                      ),

                      const SizedBox(height: 18),

                      Text(
                        'Meu Histórico de Avaliações',
                        style: Theme.of(context).textTheme.headlineMedium
                            ?.copyWith(fontWeight: FontWeight.w800),
                      ),

                      const SizedBox(height: 6),

                      Text(
                        'Consulte suas avaliações físicas anteriores e acompanhe sua evolução.',
                        style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                          color: Theme.of(context).colorScheme.onSurfaceVariant,
                          height: 1.4,
                        ),
                      ),

                      const SizedBox(height: 26),

                      if (viewModel.isLoading)
                        const _LoadingState()
                      else if (viewModel.errorMessage != null &&
                          viewModel.items.isEmpty)
                        _ErrorState(
                          message: viewModel.errorMessage!,
                          onRetry: viewModel.loadInitial,
                        )
                      else if (viewModel.items.isEmpty)
                        const _EmptyHistoryState()
                      else ...[
                        _HistorySummary(totalCount: viewModel.totalCount),

                        const SizedBox(height: 24),

                        for (
                          var index = 0;
                          index < viewModel.items.length;
                          index++
                        ) ...[
                          _AssessmentHistoryCard(
                            item: viewModel.items[index],
                            isLatest: viewModel.items[index].id == latestId,
                            onTap: () {
                              context.push(
                                '/student/physical-assessment/'
                                '${viewModel.items[index].id}',
                              );
                            },
                          ),

                          if (index < viewModel.items.length - 1)
                            const SizedBox(height: 10),
                        ],

                        if (viewModel.errorMessage != null) ...[
                          const SizedBox(height: 14),
                          Text(
                            viewModel.errorMessage!,
                            textAlign: TextAlign.center,
                            style: TextStyle(
                              color: Theme.of(context).colorScheme.error,
                            ),
                          ),
                        ],

                        const SizedBox(height: 22),

                        _PaginationCard(
                          page: viewModel.page,
                          totalPages: viewModel.totalPages,
                          isLoading: viewModel.isChangingPage,
                          hasPrevious: viewModel.hasPreviousPage,
                          hasNext: viewModel.hasNextPage,
                          onPrevious: viewModel.previousPage,
                          onNext: viewModel.nextPage,
                        ),
                      ],
                    ],
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
      bottomNavigationBar: StudentBottomNavigation(
        currentItem: StudentNavItem.more,
        onHomeTap: () {
          context.go('/student');
        },
        onHistoryTap: () {
          context.go('/student/history');
        },
        onMoreTap: () {
          context.go('/student/more');
        },
      ),
    );
  }
}

class _HistorySummary extends StatelessWidget {
  const _HistorySummary({required this.totalCount});

  final int totalCount;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: colorScheme.outlineVariant.withAlpha(120)),
      ),
      child: Row(
        children: [
          Container(
            width: 48,
            height: 48,
            decoration: BoxDecoration(
              color: colorScheme.primary.withAlpha(18),
              shape: BoxShape.circle,
            ),
            child: Icon(
              Icons.monitor_weight_outlined,
              color: colorScheme.primary,
            ),
          ),

          const SizedBox(width: 14),

          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  '$totalCount',
                  style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                    color: colorScheme.primary,
                    fontWeight: FontWeight.w800,
                  ),
                ),
                Text(
                  totalCount == 1
                      ? 'avaliação registrada'
                      : 'avaliações registradas',
                  style: Theme.of(context).textTheme.bodyMedium
                      ?.copyWith(color: colorScheme.onSurfaceVariant),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _AssessmentHistoryCard extends StatelessWidget {
  const _AssessmentHistoryCard({
    required this.item,
    required this.isLatest,
    required this.onTap,
  });

  final PhysicalAssessmentHistoryItem item;
  final bool isLatest;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Material(
      color: colorScheme.surface,
      borderRadius: BorderRadius.circular(16),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(16),
        child: Container(
          padding: const EdgeInsets.all(16),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(16),
            border: Border.all(
              color: colorScheme.outlineVariant.withAlpha(100),
            ),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Row(
                children: [
                  Container(
                    width: 42,
                    height: 42,
                    decoration: BoxDecoration(
                      color: colorScheme.primary.withAlpha(18),
                      shape: BoxShape.circle,
                    ),
                    child: Icon(
                      Icons.calendar_today_outlined,
                      color: colorScheme.primary,
                      size: 20,
                    ),
                  ),

                  const SizedBox(width: 12),

                  Expanded(
                    child: Text(
                      _formatDate(item.assessmentDate),
                      style: Theme.of(context).textTheme.titleMedium
                          ?.copyWith(fontWeight: FontWeight.w800),
                    ),
                  ),

                  if (isLatest)
                    Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 9,
                        vertical: 5,
                      ),
                      decoration: BoxDecoration(
                        color: Colors.green.withAlpha(18),
                        borderRadius: BorderRadius.circular(10),
                      ),
                      child: const Text(
                        'Mais recente',
                        style: TextStyle(
                          color: Colors.green,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                    ),

                  const SizedBox(width: 4),

                  Icon(
                    Icons.chevron_right_rounded,
                    color: colorScheme.onSurfaceVariant,
                  ),
                ],
              ),

              const SizedBox(height: 16),

              Row(
                children: [
                  Expanded(
                    child: _Metric(
                      label: 'Peso',
                      value: '${_formatNumber(item.weightKg)} kg',
                    ),
                  ),
                  Expanded(
                    child: _Metric(
                      label: 'Altura',
                      value: '${_formatNumber(item.heightCm)} cm',
                    ),
                  ),
                  Expanded(
                    child: _Metric(
                      label: '% Gordura',
                      value: item.bodyFatPercentage == null
                          ? '—'
                          : '${_formatNumber(item.bodyFatPercentage!)}%',
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _Metric extends StatelessWidget {
  const _Metric({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          label,
          style: Theme.of(context).textTheme.bodySmall
              ?.copyWith(color: colorScheme.onSurfaceVariant),
        ),
        const SizedBox(height: 4),
        Text(
          value,
          style: Theme.of(context).textTheme.bodyLarge
              ?.copyWith(fontWeight: FontWeight.w700),
        ),
      ],
    );
  }
}

class _PaginationCard extends StatelessWidget {
  const _PaginationCard({
    required this.page,
    required this.totalPages,
    required this.isLoading,
    required this.hasPrevious,
    required this.hasNext,
    required this.onPrevious,
    required this.onNext,
  });

  final int page;
  final int totalPages;
  final bool isLoading;
  final bool hasPrevious;
  final bool hasNext;
  final Future<void> Function() onPrevious;
  final Future<void> Function() onNext;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Expanded(
          child: OutlinedButton(
            onPressed: !isLoading && hasPrevious ? onPrevious : null,
            child: const Text('Anterior'),
          ),
        ),

        const SizedBox(width: 12),

        Text(
          'Página $page de $totalPages',
          style: Theme.of(context).textTheme.bodyMedium
              ?.copyWith(fontWeight: FontWeight.w700),
        ),

        const SizedBox(width: 12),

        Expanded(
          child: OutlinedButton(
            onPressed: !isLoading && hasNext ? onNext : null,
            child: const Text('Próxima'),
          ),
        ),
      ],
    );
  }
}

class _EmptyHistoryState extends StatelessWidget {
  const _EmptyHistoryState();

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 54),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: colorScheme.outlineVariant.withAlpha(100)),
      ),
      child: Column(
        children: [
          Icon(Icons.history_rounded, size: 52, color: colorScheme.primary),
          const SizedBox(height: 18),
          Text(
            'Nenhuma avaliação registrada',
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.titleLarge
                ?.copyWith(fontWeight: FontWeight.w800),
          ),
          const SizedBox(height: 8),
          Text(
            'Seu histórico de avaliações físicas aparecerá aqui.',
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.bodyMedium
                ?.copyWith(color: colorScheme.onSurfaceVariant),
          ),
        ],
      ),
    );
  }
}

class _LoadingState extends StatelessWidget {
  const _LoadingState();

  @override
  Widget build(BuildContext context) {
    return const Padding(
      padding: EdgeInsets.symmetric(vertical: 80),
      child: Center(child: CircularProgressIndicator()),
    );
  }
}

class _ErrorState extends StatelessWidget {
  const _ErrorState({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Text(message, textAlign: TextAlign.center),
        const SizedBox(height: 16),
        OutlinedButton.icon(
          onPressed: onRetry,
          icon: const Icon(Icons.refresh_rounded),
          label: const Text('Tentar novamente'),
        ),
      ],
    );
  }
}

String _formatDate(DateTime value) {
  return '${value.day.toString().padLeft(2, '0')}/'
      '${value.month.toString().padLeft(2, '0')}/'
      '${value.year}';
}

String _formatNumber(double value) {
  final formatted = value % 1 == 0
      ? value.toStringAsFixed(0)
      : value.toStringAsFixed(1);

  return formatted.replaceAll('.', ',');
}
