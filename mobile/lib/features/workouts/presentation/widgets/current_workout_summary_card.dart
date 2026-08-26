import 'package:flutter/material.dart';
import 'package:gymflow/features/workouts/models/workout_details.dart';
import 'package:gymflow/features/workouts/presentation/current_workout_view_model.dart';
import 'package:provider/provider.dart';

class CurrentWorkoutSummaryCard extends StatelessWidget {
  const CurrentWorkoutSummaryCard({
    this.onCreateWorkoutTap,
    this.onEditWorkoutTap,
    super.key,
  });

  final VoidCallback? onCreateWorkoutTap;
  final VoidCallback? onEditWorkoutTap;

  @override
  Widget build(BuildContext context) {
    final viewModel = context.watch<CurrentWorkoutViewModel>();

    if (viewModel.isLoading && !viewModel.hasLoaded) {
      return const _LoadingCard();
    }

    if (viewModel.errorMessage != null && !viewModel.hasWorkout) {
      return _ErrorCard(
        message: viewModel.errorMessage!,
        onRetry: viewModel.load,
      );
    }

    final workout = viewModel.workout;

    if (workout == null) {
      return _EmptyWorkoutCard(onCreateWorkoutTap: onCreateWorkoutTap);
    }

    return _WorkoutCard(workout: workout, onEditWorkoutTap: onEditWorkoutTap);
  }
}

class _WorkoutCard extends StatelessWidget {
  const _WorkoutCard({required this.workout, required this.onEditWorkoutTap});

  final WorkoutDetails workout;
  final VoidCallback? onEditWorkoutTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    final days = [...workout.days]..sort((a, b) => a.order.compareTo(b.order));

    final totalExercises = days.fold<int>(
      0,
      (total, day) => total + day.exercises.length,
    );

    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: colorScheme.outlineVariant.withAlpha(120)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Icon(Icons.fitness_center_rounded, color: colorScheme.primary),

              const SizedBox(width: 10),

              Expanded(
                child: Text(
                  'Treino atual',
                  style: theme.textTheme.titleLarge?.copyWith(
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),

              _StatusBadge(isActive: workout.isActive),
            ],
          ),

          const SizedBox(height: 20),

          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      workout.name,
                      style: theme.textTheme.titleLarge?.copyWith(
                        fontWeight: FontWeight.w800,
                      ),
                    ),

                    if (workout.description != null &&
                        workout.description!.trim().isNotEmpty) ...[
                      const SizedBox(height: 6),

                      Text(
                        workout.description!.trim(),
                        style: theme.textTheme.bodyMedium?.copyWith(
                          color: colorScheme.onSurfaceVariant,
                          height: 1.4,
                        ),
                      ),
                    ],
                  ],
                ),
              ),

              if (onEditWorkoutTap != null) ...[
                const SizedBox(width: 12),

                IconButton(
                  tooltip: 'Editar treino',
                  onPressed: onEditWorkoutTap,
                  icon: const Icon(Icons.edit_outlined),
                ),
              ],
            ],
          ),

          const SizedBox(height: 18),

          Row(
            children: [
              Expanded(
                child: _Metric(
                  icon: Icons.view_day_outlined,
                  value: '${days.length}',
                  label: days.length == 1 ? 'Dia' : 'Dias',
                ),
              ),

              const SizedBox(width: 12),

              Expanded(
                child: _Metric(
                  icon: Icons.fitness_center_rounded,
                  value: '$totalExercises',
                  label: totalExercises == 1 ? 'Exercício' : 'Exercícios',
                ),
              ),
            ],
          ),

          const SizedBox(height: 22),

          if (days.isEmpty)
            Text(
              'Nenhuma divisão foi configurada neste treino.',
              style: theme.textTheme.bodyMedium?.copyWith(
                color: colorScheme.onSurfaceVariant,
              ),
            )
          else
            for (var index = 0; index < days.length; index++) ...[
              _WorkoutDayCard(day: days[index], position: index + 1),

              if (index < days.length - 1) const SizedBox(height: 12),
            ],
        ],
      ),
    );
  }
}

class _WorkoutDayCard extends StatelessWidget {
  const _WorkoutDayCard({required this.day, required this.position});

  final WorkoutDayDetails day;
  final int position;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    final exercises = [...day.exercises]
      ..sort((a, b) => a.order.compareTo(b.order));

    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: colorScheme.surfaceContainerLow,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: colorScheme.outlineVariant.withAlpha(90)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Container(
                width: 36,
                height: 36,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  color: colorScheme.primary.withAlpha(18),
                ),
                child: Text(
                  '$position',
                  style: TextStyle(
                    color: colorScheme.primary,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),

              const SizedBox(width: 12),

              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      day.name,
                      style: theme.textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.w800,
                      ),
                    ),

                    const SizedBox(height: 3),

                    Text(
                      _exerciseCountLabel(exercises.length),
                      style: theme.textTheme.bodySmall?.copyWith(
                        color: colorScheme.onSurfaceVariant,
                      ),
                    ),
                  ],
                ),
              ),

              if (day.completedToday) const _CompletedTodayBadge(),
            ],
          ),

          if (day.lastCompletedAt != null) ...[
            const SizedBox(height: 10),

            Text(
              'Última conclusão: '
              '${_formatDateTime(day.lastCompletedAt!)}',
              style: theme.textTheme.bodySmall?.copyWith(
                color: colorScheme.onSurfaceVariant,
              ),
            ),
          ],

          if (exercises.isNotEmpty) ...[
            const SizedBox(height: 14),
            const Divider(height: 1),
            const SizedBox(height: 12),

            for (var index = 0; index < exercises.length; index++) ...[
              _ExerciseRow(exercise: exercises[index]),

              if (index < exercises.length - 1) const SizedBox(height: 12),
            ],
          ],
        ],
      ),
    );
  }
}

class _ExerciseRow extends StatelessWidget {
  const _ExerciseRow({required this.exercise});

  final WorkoutExerciseDetails exercise;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(
          Icons.fitness_center_rounded,
          size: 17,
          color: colorScheme.primary,
        ),

        const SizedBox(width: 9),

        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                exercise.exerciseName,
                style: theme.textTheme.bodyMedium?.copyWith(
                  fontWeight: FontWeight.w700,
                ),
              ),

              const SizedBox(height: 2),

              Text(
                exercise.muscleGroup,
                style: theme.textTheme.bodySmall?.copyWith(
                  color: colorScheme.onSurfaceVariant,
                ),
              ),

              const SizedBox(height: 4),

              Text(
                '${exercise.sets} séries • '
                '${exercise.repetitions} reps • '
                '${_restText(exercise.restSeconds)}',
                style: theme.textTheme.bodySmall?.copyWith(
                  color: colorScheme.onSurfaceVariant,
                  fontWeight: FontWeight.w600,
                ),
              ),

              if (exercise.notes != null &&
                  exercise.notes!.trim().isNotEmpty) ...[
                const SizedBox(height: 4),

                Text(
                  exercise.notes!.trim(),
                  style: theme.textTheme.bodySmall?.copyWith(
                    color: colorScheme.onSurfaceVariant,
                  ),
                ),
              ],
            ],
          ),
        ),
      ],
    );
  }
}

class _Metric extends StatelessWidget {
  const _Metric({required this.icon, required this.value, required this.label});

  final IconData icon;
  final String value;
  final String label;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: colorScheme.primary.withAlpha(12),
        borderRadius: BorderRadius.circular(14),
      ),
      child: Row(
        children: [
          Icon(icon, size: 20, color: colorScheme.primary),

          const SizedBox(width: 10),

          Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                value,
                style: theme.textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.w800,
                ),
              ),
              Text(
                label,
                style: theme.textTheme.bodySmall?.copyWith(
                  color: colorScheme.onSurfaceVariant,
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _StatusBadge extends StatelessWidget {
  const _StatusBadge({required this.isActive});

  final bool isActive;

  @override
  Widget build(BuildContext context) {
    final color = isActive
        ? const Color(0xFF22C55E)
        : Theme.of(context).colorScheme.onSurfaceVariant;

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
      decoration: BoxDecoration(
        color: color.withAlpha(20),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        isActive ? 'Ativo' : 'Inativo',
        style: TextStyle(
          color: color,
          fontSize: 12,
          fontWeight: FontWeight.w800,
        ),
      ),
    );
  }
}

class _CompletedTodayBadge extends StatelessWidget {
  const _CompletedTodayBadge();

  @override
  Widget build(BuildContext context) {
    const color = Color(0xFF22C55E);

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 5),
      decoration: BoxDecoration(
        color: color.withAlpha(20),
        borderRadius: BorderRadius.circular(999),
      ),
      child: const Text(
        'Concluído hoje',
        style: TextStyle(
          color: color,
          fontSize: 11,
          fontWeight: FontWeight.w800,
        ),
      ),
    );
  }
}

class _EmptyWorkoutCard extends StatelessWidget {
  const _EmptyWorkoutCard({required this.onCreateWorkoutTap});

  final VoidCallback? onCreateWorkoutTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: colorScheme.outlineVariant.withAlpha(120)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Icon(Icons.fitness_center_rounded, color: colorScheme.primary),

              const SizedBox(width: 10),

              Text(
                'Treino atual',
                style: theme.textTheme.titleLarge?.copyWith(
                  fontWeight: FontWeight.w800,
                ),
              ),
            ],
          ),

          const SizedBox(height: 20),

          Icon(Icons.assignment_outlined, size: 38, color: colorScheme.primary),

          const SizedBox(height: 12),

          Text(
            'Nenhum treino ativo',
            textAlign: TextAlign.center,
            style: theme.textTheme.titleMedium?.copyWith(
              fontWeight: FontWeight.w800,
            ),
          ),

          const SizedBox(height: 6),

          Text(
            'Este aluno ainda não possui um treino ativo.',
            textAlign: TextAlign.center,
            style: theme.textTheme.bodyMedium?.copyWith(
              color: colorScheme.onSurfaceVariant,
            ),
          ),

          if (onCreateWorkoutTap != null) ...[
            const SizedBox(height: 18),

            FilledButton.icon(
              onPressed: onCreateWorkoutTap,
              icon: const Icon(Icons.add_rounded),
              label: const Text('Criar ou atribuir treino'),
            ),
          ],
        ],
      ),
    );
  }
}

class _LoadingCard extends StatelessWidget {
  const _LoadingCard();

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      height: 150,
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: colorScheme.outlineVariant.withAlpha(120)),
      ),
      alignment: Alignment.center,
      child: const CircularProgressIndicator(),
    );
  }
}

class _ErrorCard extends StatelessWidget {
  const _ErrorCard({required this.message, required this.onRetry});

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
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: colorScheme.outlineVariant.withAlpha(120)),
      ),
      child: Column(
        children: [
          Icon(Icons.error_outline_rounded, size: 36, color: colorScheme.error),

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

String _exerciseCountLabel(int count) {
  return count == 1 ? '1 exercício' : '$count exercícios';
}

String _restText(int? seconds) {
  if (seconds == null) {
    return 'Descanso não informado';
  }

  if (seconds < 60) {
    return '${seconds}s descanso';
  }

  if (seconds % 60 == 0) {
    final minutes = seconds ~/ 60;

    return minutes == 1 ? '1 min descanso' : '$minutes min descanso';
  }

  final minutes = seconds ~/ 60;
  final remaining = seconds % 60;

  return '${minutes}min ${remaining}s descanso';
}

String _formatDateTime(DateTime date) {
  final local = date.toLocal();

  final day = local.day.toString().padLeft(2, '0');
  final month = local.month.toString().padLeft(2, '0');

  final hour = local.hour.toString().padLeft(2, '0');
  final minute = local.minute.toString().padLeft(2, '0');

  return '$day/$month/${local.year} às $hour:$minute';
}
