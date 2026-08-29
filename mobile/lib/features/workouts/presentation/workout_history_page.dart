import 'package:flutter/material.dart';
import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_bottom_navigation.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_page_header.dart';
import 'package:gymflow/features/workouts/data/workouts_service.dart';
import 'package:gymflow/features/workouts/models/workout_history_item.dart';
import 'package:gymflow/features/workouts/presentation/workout_history_view_model.dart';
import 'package:provider/provider.dart';

class WorkoutHistoryPage extends StatelessWidget {
  const WorkoutHistoryPage({
    required this.studentId,
    required this.studentName,
    super.key,
  });

  final String studentId;
  final String studentName;

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) => WorkoutHistoryViewModel(
        WorkoutsService(context.read<ApiClient>()),
        studentId,
      )..load(),
      child: _WorkoutHistoryView(studentName: studentName),
    );
  }
}

class _WorkoutHistoryView extends StatelessWidget {
  const _WorkoutHistoryView({required this.studentName});

  final String studentName;

  @override
  Widget build(BuildContext context) {
    final viewModel = context.watch<WorkoutHistoryViewModel>();

    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Scaffold(
      bottomNavigationBar: const ProfessorAdminBottomNavigation(
        currentItem: ProfessorAdminNavItem.students,
      ),
      body: SafeArea(
        bottom: false,
        child: RefreshIndicator(
          onRefresh: viewModel.refresh,
          child: ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.fromLTRB(20, 12, 20, 32),
            children: [
              const ProfessorAdminPageHeader(),

              const SizedBox(height: 24),

              _StudentCard(studentName: studentName),

              const SizedBox(height: 28),

              Row(
                children: [
                  Icon(
                    Icons.history_rounded,
                    color: colorScheme.primary,
                    size: 28,
                  ),

                  const SizedBox(width: 10),

                  Expanded(
                    child: Text(
                      'Histórico de treinos',
                      style: theme.textTheme.headlineSmall?.copyWith(
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                  ),
                ],
              ),

              const SizedBox(height: 6),

              Text(
                'Dias de treino concluídos pelo aluno.',
                style: theme.textTheme.bodyMedium?.copyWith(
                  color: colorScheme.onSurfaceVariant,
                  height: 1.4,
                ),
              ),

              if (viewModel.hasLoaded && viewModel.errorMessage == null) ...[
                const SizedBox(height: 8),

                Text(
                  _historyCountLabel(viewModel.totalCount),
                  style: theme.textTheme.bodySmall?.copyWith(
                    color: colorScheme.onSurfaceVariant,
                  ),
                ),
              ],

              const SizedBox(height: 22),

              if (viewModel.isLoading && !viewModel.hasLoaded)
                const _LoadingState()
              else if (viewModel.errorMessage != null &&
                  viewModel.items.isEmpty)
                _ErrorState(
                  message: viewModel.errorMessage!,
                  onRetry: viewModel.load,
                )
              else if (viewModel.isEmpty)
                const _EmptyState()
              else ...[
                for (
                  var index = 0;
                  index < viewModel.items.length;
                  index++
                ) ...[
                  _HistoryCard(item: viewModel.items[index]),

                  if (index < viewModel.items.length - 1)
                    const SizedBox(height: 12),
                ],

                if (viewModel.errorMessage != null) ...[
                  const SizedBox(height: 16),

                  _InlineError(message: viewModel.errorMessage!),
                ],

                if (viewModel.hasNextPage) ...[
                  const SizedBox(height: 20),

                  OutlinedButton.icon(
                    onPressed: viewModel.isLoadingMore
                        ? null
                        : viewModel.loadMore,
                    icon: viewModel.isLoadingMore
                        ? const SizedBox(
                            width: 18,
                            height: 18,
                            child: CircularProgressIndicator(strokeWidth: 2),
                          )
                        : const Icon(Icons.expand_more_rounded),
                    label: Text(
                      viewModel.isLoadingMore
                          ? 'Carregando...'
                          : 'Carregar mais',
                    ),
                  ),
                ],
              ],
            ],
          ),
        ),
      ),
    );
  }
}

class _StudentCard extends StatelessWidget {
  const _StudentCard({required this.studentName});

  final String studentName;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: colorScheme.outlineVariant.withAlpha(120)),
      ),
      child: Row(
        children: [
          Container(
            width: 50,
            height: 50,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: colorScheme.primary.withAlpha(18),
            ),
            child: Text(
              _initials(studentName),
              style: theme.textTheme.titleMedium?.copyWith(
                color: colorScheme.primary,
                fontWeight: FontWeight.w800,
              ),
            ),
          ),

          const SizedBox(width: 14),

          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  studentName,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: theme.textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.w800,
                  ),
                ),

                const SizedBox(height: 3),

                Text(
                  'Aluno',
                  style: theme.textTheme.bodySmall?.copyWith(
                    color: colorScheme.onSurfaceVariant,
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

class _HistoryCard extends StatelessWidget {
  const _HistoryCard({required this.item});

  final WorkoutHistoryItem item;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    final completedAt = item.completedAt.toUtc().add(
      Duration(minutes: item.completedAtUtcOffsetMinutes),
    );

    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: colorScheme.outlineVariant.withAlpha(120)),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.center,
        children: [
          Container(
            width: 50,
            height: 50,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: colorScheme.primary.withAlpha(18),
            ),
            child: Icon(
              Icons.fitness_center_rounded,
              color: colorScheme.primary,
            ),
          ),

          const SizedBox(width: 14),

          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  item.workoutName,
                  style: theme.textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.w800,
                  ),
                ),

                const SizedBox(height: 5),

                Text(
                  item.workoutDayName,
                  style: theme.textTheme.bodyMedium?.copyWith(
                    color: colorScheme.primary,
                    fontWeight: FontWeight.w700,
                  ),
                ),

                const SizedBox(height: 8),

                Row(
                  children: [
                    Icon(
                      Icons.calendar_today_outlined,
                      size: 16,
                      color: colorScheme.onSurfaceVariant,
                    ),

                    const SizedBox(width: 7),

                    Flexible(
                      child: Text(
                        '${_formatDate(completedAt)}'
                        ' • '
                        '${_formatTime(completedAt)}',
                        style: theme.textTheme.bodySmall?.copyWith(
                          color: colorScheme.onSurfaceVariant,
                          fontWeight: FontWeight.w600,
                        ),
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
}

class _LoadingState extends StatelessWidget {
  const _LoadingState();

  @override
  Widget build(BuildContext context) {
    return const Padding(
      padding: EdgeInsets.symmetric(vertical: 48),
      child: Center(child: CircularProgressIndicator()),
    );
  }
}

class _EmptyState extends StatelessWidget {
  const _EmptyState();

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 32),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: colorScheme.outlineVariant.withAlpha(120)),
      ),
      child: Column(
        children: [
          Container(
            width: 56,
            height: 56,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: colorScheme.primary.withAlpha(18),
            ),
            child: Icon(
              Icons.history_rounded,
              color: colorScheme.primary,
              size: 28,
            ),
          ),

          const SizedBox(height: 14),

          Text(
            'Nenhum treino concluído ainda',
            textAlign: TextAlign.center,
            style: theme.textTheme.titleMedium?.copyWith(
              fontWeight: FontWeight.w800,
            ),
          ),

          const SizedBox(height: 6),

          Text(
            'As divisões concluídas pelo aluno aparecerão aqui.',
            textAlign: TextAlign.center,
            style: theme.textTheme.bodyMedium?.copyWith(
              color: colorScheme.onSurfaceVariant,
              height: 1.4,
            ),
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
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: colorScheme.error.withAlpha(70)),
      ),
      child: Column(
        children: [
          Icon(Icons.error_outline_rounded, color: colorScheme.error, size: 32),

          const SizedBox(height: 12),

          Text(
            message,
            textAlign: TextAlign.center,
            style: theme.textTheme.bodyMedium?.copyWith(
              color: colorScheme.error,
            ),
          ),

          const SizedBox(height: 16),

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
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: colorScheme.error.withAlpha(15),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: colorScheme.error.withAlpha(70)),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(Icons.error_outline_rounded, size: 20, color: colorScheme.error),

          const SizedBox(width: 10),

          Expanded(
            child: Text(message, style: TextStyle(color: colorScheme.error)),
          ),
        ],
      ),
    );
  }
}

String _historyCountLabel(int count) {
  if (count == 0) {
    return 'Nenhuma conclusão registrada';
  }

  if (count == 1) {
    return '1 conclusão registrada';
  }

  return '$count conclusões registradas';
}

String _formatDate(DateTime value) {
  return '${_twoDigits(value.day)}/'
      '${_twoDigits(value.month)}/'
      '${value.year}';
}

String _formatTime(DateTime value) {
  return '${_twoDigits(value.hour)}:'
      '${_twoDigits(value.minute)}';
}

String _twoDigits(int value) {
  return value.toString().padLeft(2, '0');
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
