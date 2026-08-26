import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_brand_header.dart';
import 'package:gymflow/features/home/presentation/widgets/student_bottom_navigation.dart';
import 'package:gymflow/features/workouts/data/workouts_service.dart';
import 'package:gymflow/features/workouts/models/workout_history_item.dart';
import 'package:gymflow/features/workouts/presentation/workout_history_view_model.dart';
import 'package:provider/provider.dart';

class StudentWorkoutHistoryPage extends StatelessWidget {
  const StudentWorkoutHistoryPage({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) => WorkoutHistoryViewModel.forCurrentUser(
        WorkoutsService(context.read<ApiClient>()),
      )..load(),
      child: const _StudentWorkoutHistoryView(),
    );
  }
}

class _StudentWorkoutHistoryView extends StatelessWidget {
  const _StudentWorkoutHistoryView();

  @override
  Widget build(BuildContext context) {
    final viewModel = context.watch<WorkoutHistoryViewModel>();

    return Scaffold(
      body: SafeArea(
        bottom: false,
        child: RefreshIndicator(
          onRefresh: viewModel.refresh,
          child: ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.fromLTRB(24, 16, 24, 32),
            children: [
              Center(
                child: ConstrainedBox(
                  constraints: const BoxConstraints(maxWidth: 680),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      const ProfessorAdminBrandHeader(height: 88),

                      const SizedBox(height: 24),

                      Text(
                        'Histórico',
                        style: Theme.of(context).textTheme.headlineMedium
                            ?.copyWith(fontWeight: FontWeight.w800),
                      ),

                      const SizedBox(height: 6),

                      Text(
                        'Acompanhe os dias de treino que você concluiu.',
                        style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                          color: Theme.of(context).colorScheme.onSurfaceVariant,
                          height: 1.4,
                        ),
                      ),

                      const SizedBox(height: 24),

                      if (viewModel.isLoading && !viewModel.hasLoaded)
                        const _LoadingState()
                      else if (viewModel.errorMessage != null &&
                          viewModel.items.isEmpty)
                        _ErrorState(
                          message: viewModel.errorMessage!,
                          onRetry: viewModel.load,
                        )
                      else if (viewModel.isEmpty)
                        const _EmptyHistoryState()
                      else ...[
                        _HistorySummaryCard(totalCount: viewModel.totalCount),

                        const SizedBox(height: 30),

                        Text(
                          'Últimas conclusões',
                          style: Theme.of(context).textTheme.titleLarge
                              ?.copyWith(fontWeight: FontWeight.w800),
                        ),

                        const SizedBox(height: 14),

                        for (
                          var index = 0;
                          index < viewModel.items.length;
                          index++
                        ) ...[
                          _HistoryItemCard(item: viewModel.items[index]),
                          if (index < viewModel.items.length - 1)
                            const SizedBox(height: 10),
                        ],

                        if (viewModel.errorMessage != null) ...[
                          const SizedBox(height: 14),
                          _InlineError(message: viewModel.errorMessage!),
                        ],

                        const SizedBox(height: 22),

                        _PaginationCard(
                          page: viewModel.page,
                          totalPages: viewModel.totalPages,
                          isLoading: viewModel.isLoading,
                          hasPrevious: viewModel.hasPreviousPage,
                          hasNext: viewModel.hasNextPage,
                          onPrevious: viewModel.previousPage,
                          onNext: viewModel.nextPage,
                        ),

                        const SizedBox(height: 20),

                        const _HistoryInfoCard(),
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
        currentItem: StudentNavItem.history,
        onHomeTap: () {
          context.go('/student');
        },
        onMoreTap: () {
          context.go('/student/more');
        },
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

class _HistorySummaryCard extends StatelessWidget {
  const _HistorySummaryCard({required this.totalCount});

  final int totalCount;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: colorScheme.outlineVariant.withAlpha(120)),
      ),
      child: Row(
        children: [
          Container(
            width: 52,
            height: 52,
            decoration: BoxDecoration(
              color: colorScheme.primary.withAlpha(18),
              shape: BoxShape.circle,
            ),
            child: Icon(
              Icons.calendar_month_rounded,
              color: colorScheme.primary,
            ),
          ),

          const SizedBox(width: 18),

          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Resumo do seu histórico',
                  style: Theme.of(context).textTheme.titleMedium
                      ?.copyWith(fontWeight: FontWeight.w800),
                ),

                const SizedBox(height: 10),

                Text(
                  '$totalCount',
                  style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                    color: colorScheme.primary,
                    fontWeight: FontWeight.w800,
                  ),
                ),

                Text(
                  totalCount == 1 ? 'dia concluído' : 'dias concluídos',
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

class _HistoryItemCard extends StatelessWidget {
  const _HistoryItemCard({required this.item});

  final WorkoutHistoryItem item;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: colorScheme.outlineVariant.withAlpha(100)),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 44,
            height: 44,
            decoration: BoxDecoration(
              color: Colors.green.withAlpha(18),
              shape: BoxShape.circle,
            ),
            child: const Icon(
              Icons.check_circle_outline_rounded,
              color: Colors.green,
            ),
          ),

          const SizedBox(width: 14),

          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  item.workoutDayName,
                  style: Theme.of(context).textTheme.titleMedium
                      ?.copyWith(fontWeight: FontWeight.w800),
                ),

                const SizedBox(height: 4),

                Text(
                  item.workoutName,
                  style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                    color: colorScheme.primary,
                    fontWeight: FontWeight.w600,
                  ),
                ),

                const SizedBox(height: 6),

                Row(
                  children: [
                    Icon(
                      Icons.calendar_today_rounded,
                      size: 15,
                      color: colorScheme.onSurfaceVariant,
                    ),
                    const SizedBox(width: 6),
                    Expanded(
                      child: Text(
                        _formatDateTime(item.completedAt),
                        style: Theme.of(context).textTheme.bodySmall
                            ?.copyWith(color: colorScheme.onSurfaceVariant),
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  String _formatDateTime(DateTime value) {
    final local = value.toLocal();

    final day = local.day.toString().padLeft(2, '0');
    final month = local.month.toString().padLeft(2, '0');
    final hour = local.hour.toString().padLeft(2, '0');
    final minute = local.minute.toString().padLeft(2, '0');

    return '$day/$month/${local.year} '
        'às $hour:$minute';
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
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: colorScheme.outlineVariant.withAlpha(100)),
      ),
      child: Row(
        children: [
          Expanded(
            child: OutlinedButton(
              onPressed: !isLoading && hasPrevious ? onPrevious : null,
              child: const Text('Anterior'),
            ),
          ),

          const SizedBox(width: 14),

          Text(
            'Página $page de $totalPages',
            style: Theme.of(context).textTheme.bodyMedium
                ?.copyWith(fontWeight: FontWeight.w700),
          ),

          const SizedBox(width: 14),

          Expanded(
            child: OutlinedButton(
              onPressed: !isLoading && hasNext ? onNext : null,
              child: const Text('Próxima'),
            ),
          ),
        ],
      ),
    );
  }
}

class _HistoryInfoCard extends StatelessWidget {
  const _HistoryInfoCard();

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: colorScheme.outlineVariant.withAlpha(100)),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(Icons.info_outline_rounded, color: colorScheme.primary),

          const SizedBox(width: 12),

          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Sobre o histórico',
                  style: Theme.of(context).textTheme.titleSmall
                      ?.copyWith(fontWeight: FontWeight.w800),
                ),

                const SizedBox(height: 4),

                Text(
                  'O histórico mostra os dias de treino que você concluiu. '
                  'Os registros são salvos por dia, não por exercício.',
                  style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                    color: colorScheme.onSurfaceVariant,
                    height: 1.4,
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

class _EmptyHistoryState extends StatelessWidget {
  const _EmptyHistoryState();

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 56),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: colorScheme.outlineVariant.withAlpha(100)),
      ),
      child: Column(
        children: [
          Container(
            width: 72,
            height: 72,
            decoration: BoxDecoration(
              color: colorScheme.primary.withAlpha(18),
              shape: BoxShape.circle,
            ),
            child: Icon(
              Icons.history_rounded,
              size: 36,
              color: colorScheme.primary,
            ),
          ),

          const SizedBox(height: 22),

          Text(
            'Nenhum treino concluído ainda',
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.titleLarge
                ?.copyWith(fontWeight: FontWeight.w800),
          ),

          const SizedBox(height: 10),

          Text(
            'Quando você concluir um dia de treino, '
            'o registro aparecerá aqui.',
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.bodyMedium
                ?.copyWith(color: colorScheme.onSurfaceVariant, height: 1.4),
          ),
        ],
      ),
    );
  }
}

class _ErrorState extends StatelessWidget {
  const _ErrorState({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      padding: const EdgeInsets.all(24),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: colorScheme.error.withAlpha(80)),
      ),
      child: Column(
        children: [
          Icon(Icons.error_outline_rounded, size: 40, color: colorScheme.error),

          const SizedBox(height: 14),

          Text(message, textAlign: TextAlign.center),

          const SizedBox(height: 18),

          OutlinedButton.icon(
            onPressed: onRetry,
            icon: const Icon(Icons.refresh_rounded),
            label: const Text('Tentar novamente'),
          ),
        ],
      ),
    );
  }
}

class _InlineError extends StatelessWidget {
  const _InlineError({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    return Text(
      message,
      textAlign: TextAlign.center,
      style: Theme.of(context).textTheme.bodySmall?.copyWith(
        color: Theme.of(context).colorScheme.error,
        fontWeight: FontWeight.w600,
      ),
    );
  }
}
