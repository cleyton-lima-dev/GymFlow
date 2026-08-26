import 'package:flutter/material.dart';
import 'package:gymflow/features/workouts/models/workout_details.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_brand_header.dart';
import 'package:gymflow/features/home/presentation/widgets/student_bottom_navigation.dart';
import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/features/workouts/data/workouts_service.dart';
import 'package:gymflow/features/workouts/presentation/student_workout_day_view_model.dart';
import 'package:provider/provider.dart';
import 'package:go_router/go_router.dart';

class StudentWorkoutDayArguments {
  const StudentWorkoutDayArguments({
    required this.workout,
    required this.day,
    required this.position,
    required this.totalDays,
    this.onCompleted,
  });

  final WorkoutDetails workout;
  final WorkoutDayDetails day;
  final int position;
  final int totalDays;
  final Future<void> Function()? onCompleted;
}

class StudentWorkoutDayPage extends StatelessWidget {
  const StudentWorkoutDayPage({required this.arguments, super.key});

  final StudentWorkoutDayArguments arguments;

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) => StudentWorkoutDayViewModel(
        WorkoutsService(context.read<ApiClient>()),
        arguments.day,
      ),
      child: _StudentWorkoutDayView(arguments: arguments),
    );
  }
}

class _StudentWorkoutDayView extends StatelessWidget {
  const _StudentWorkoutDayView({required this.arguments});

  final StudentWorkoutDayArguments arguments;

  @override
  Widget build(BuildContext context) {
    final viewModel = context.watch<StudentWorkoutDayViewModel>();

    final day = arguments.day;

    final exercises = [...day.exercises]
      ..sort((a, b) => a.order.compareTo(b.order));

    final colorScheme = Theme.of(context).colorScheme;

    return Scaffold(
      body: SafeArea(
        bottom: false,
        child: ListView(
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
                              onPressed: viewModel.isCompleting
                                  ? null
                                  : () {
                                      Navigator.of(context).pop();
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

                    const SizedBox(height: 16),

                    Text(
                      day.name,
                      style: Theme.of(context).textTheme.headlineMedium
                          ?.copyWith(fontWeight: FontWeight.w800),
                    ),

                    const SizedBox(height: 6),

                    Text(
                      'Dia ${arguments.position} de '
                      '${arguments.totalDays}  •  '
                      '${exercises.length} '
                      '${exercises.length == 1 ? 'exercício' : 'exercícios'}',
                      style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                        color: colorScheme.onSurfaceVariant,
                        fontWeight: FontWeight.w500,
                      ),
                    ),

                    if (viewModel.completedToday) ...[
                      const SizedBox(height: 24),
                      _CompletedDayBanner(completedAt: viewModel.completedAt),
                    ],

                    const SizedBox(height: 24),

                    _DayInfoCard(
                      exerciseCount: exercises.length,
                      completedToday: viewModel.completedToday,
                    ),

                    const SizedBox(height: 28),

                    Row(
                      children: [
                        Icon(
                          Icons.fitness_center_rounded,
                          size: 21,
                          color: colorScheme.primary,
                        ),
                        const SizedBox(width: 8),
                        Expanded(
                          child: Text(
                            'Exercícios do dia',
                            style: Theme.of(context).textTheme.titleLarge
                                ?.copyWith(fontWeight: FontWeight.w800),
                          ),
                        ),
                        Container(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 10,
                            vertical: 6,
                          ),
                          decoration: BoxDecoration(
                            color: colorScheme.primary.withAlpha(18),
                            borderRadius: BorderRadius.circular(20),
                          ),
                          child: Text(
                            '${exercises.length} '
                            '${exercises.length == 1 ? 'exercício' : 'exercícios'}',
                            style: Theme.of(context).textTheme.labelMedium
                                ?.copyWith(
                                  color: colorScheme.primary,
                                  fontWeight: FontWeight.w700,
                                ),
                          ),
                        ),
                      ],
                    ),

                    const SizedBox(height: 16),

                    if (exercises.isEmpty)
                      const _EmptyExercisesCard()
                    else
                      for (
                        var index = 0;
                        index < exercises.length;
                        index++
                      ) ...[
                        _ExerciseRow(
                          exercise: exercises[index],
                          position: index + 1,
                        ),
                        if (index < exercises.length - 1)
                          const SizedBox(height: 10),
                      ],

                    const SizedBox(height: 24),

                    if (viewModel.completedToday)
                      const _CompletedDayFooter()
                    else if (exercises.isNotEmpty)
                      _CompleteDayCard(
                        isLoading: viewModel.isCompleting,
                        errorMessage: viewModel.errorMessage,
                        onPressed: () async {
                          final completed = await context
                              .read<StudentWorkoutDayViewModel>()
                              .complete();

                          if (!completed || !context.mounted) {
                            return;
                          }

                          await arguments.onCompleted?.call();
                        },
                      ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
      bottomNavigationBar: StudentBottomNavigation(
        currentItem: StudentNavItem.home,
        onHomeTap: viewModel.isCompleting
            ? null
            : () {
                context.go('/student');
              },
        onHistoryTap: viewModel.isCompleting
            ? null
            : () {
                context.go('/student/history');
              },
        onMoreTap: viewModel.isCompleting
            ? null
            : () {
                context.go('/student/more');
              },
      ),
    );
  }
}

class _DayInfoCard extends StatelessWidget {
  const _DayInfoCard({
    required this.exerciseCount,
    required this.completedToday,
  });

  final int exerciseCount;
  final bool completedToday;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    String message;

    if (exerciseCount == 0) {
      message = 'Este dia ainda não possui exercícios configurados.';
    } else if (completedToday) {
      message =
          'Você já concluiu este dia. '
          'O registro foi salvo no seu histórico.';
    } else {
      message =
          'Execute os exercícios na ordem apresentada. '
          'Ao finalizar todos, marque o dia como concluído.';
    }

    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: colorScheme.outlineVariant.withAlpha(90)),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 38,
            height: 38,
            decoration: BoxDecoration(
              color: colorScheme.primary.withAlpha(18),
              shape: BoxShape.circle,
            ),
            child: Icon(
              Icons.info_outline_rounded,
              color: colorScheme.primary,
              size: 21,
            ),
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Como funciona',
                  style: Theme.of(context).textTheme.titleSmall
                      ?.copyWith(fontWeight: FontWeight.w800),
                ),
                const SizedBox(height: 4),
                Text(
                  message,
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

class _ExerciseRow extends StatelessWidget {
  const _ExerciseRow({required this.exercise, required this.position});

  final WorkoutExerciseDetails exercise;
  final int position;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    final muscleGroup = exercise.muscleGroup.trim();
    final notes = exercise.notes?.trim();

    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: colorScheme.outlineVariant.withAlpha(100)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Container(
                width: 38,
                height: 38,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: colorScheme.primary.withAlpha(18),
                  borderRadius: BorderRadius.circular(11),
                ),
                child: Text(
                  '$position',
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
                      exercise.exerciseName,
                      style: theme.textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.w800,
                      ),
                    ),

                    if (muscleGroup.isNotEmpty) ...[
                      const SizedBox(height: 3),
                      Text(
                        muscleGroup,
                        style: theme.textTheme.bodySmall?.copyWith(
                          color: colorScheme.onSurfaceVariant,
                        ),
                      ),
                    ],
                  ],
                ),
              ),
            ],
          ),

          const SizedBox(height: 16),

          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: _PrescriptionItem(
                  label: 'Séries',
                  value: '${exercise.sets}',
                ),
              ),
              Expanded(
                child: _PrescriptionItem(
                  label: 'Repetições',
                  value: exercise.repetitions,
                ),
              ),
              Expanded(
                child: _PrescriptionItem(
                  label: 'Descanso',
                  value: _restText(exercise.restSeconds),
                ),
              ),
            ],
          ),

          if (notes != null && notes.isNotEmpty) ...[
            const SizedBox(height: 16),

            Container(
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: colorScheme.surface,
                borderRadius: BorderRadius.circular(12),
              ),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Icon(
                    Icons.notes_rounded,
                    size: 18,
                    color: colorScheme.primary,
                  ),

                  const SizedBox(width: 8),

                  Expanded(
                    child: Text.rich(
                      TextSpan(
                        children: [
                          const TextSpan(
                            text: 'Observação: ',
                            style: TextStyle(fontWeight: FontWeight.w700),
                          ),
                          TextSpan(text: notes),
                        ],
                      ),
                      style: theme.textTheme.bodySmall?.copyWith(
                        color: colorScheme.onSurfaceVariant,
                        height: 1.4,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ],
        ],
      ),
    );
  }

  String _restText(int? seconds) {
    if (seconds == null) {
      return '—';
    }

    if (seconds < 60) {
      return '${seconds}s';
    }

    if (seconds % 60 == 0) {
      return '${seconds ~/ 60}min';
    }

    final minutes = seconds ~/ 60;
    final remainingSeconds = seconds % 60;

    return '${minutes}m ${remainingSeconds}s';
  }
}

class _PrescriptionItem extends StatelessWidget {
  const _PrescriptionItem({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Padding(
      padding: const EdgeInsets.only(right: 6),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            label,
            style: theme.textTheme.labelSmall?.copyWith(
              color: colorScheme.primary,
              fontWeight: FontWeight.w700,
            ),
          ),

          const SizedBox(height: 4),

          Text(
            value,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: theme.textTheme.bodyLarge?.copyWith(
              fontWeight: FontWeight.w700,
            ),
          ),
        ],
      ),
    );
  }
}

class _CompletedDayBanner extends StatelessWidget {
  const _CompletedDayBanner({required this.completedAt});

  final DateTime? completedAt;

  @override
  Widget build(BuildContext context) {
    final completedAt = this.completedAt;

    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: Colors.green.withAlpha(14),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: Colors.green.withAlpha(55)),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 42,
            height: 42,
            decoration: BoxDecoration(
              color: Colors.green.withAlpha(28),
              shape: BoxShape.circle,
            ),
            child: const Icon(Icons.check_rounded, color: Colors.green),
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Dia concluído!',
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    color: Colors.green,
                    fontWeight: FontWeight.w800,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  completedAt == null
                      ? 'Este dia já foi concluído hoje.'
                      : 'Concluído em '
                            '${_formatCompletedAt(completedAt)}.',
                  style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                    color: Theme.of(context).colorScheme.onSurfaceVariant,
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

  String _formatCompletedAt(DateTime value) {
    final local = value.toLocal();

    final day = local.day.toString().padLeft(2, '0');
    final month = local.month.toString().padLeft(2, '0');
    final hour = local.hour.toString().padLeft(2, '0');
    final minute = local.minute.toString().padLeft(2, '0');

    return '$day/$month/${local.year} '
        'às $hour:$minute';
  }
}

class _CompleteDayCard extends StatelessWidget {
  const _CompleteDayCard({
    required this.isLoading,
    required this.errorMessage,
    required this.onPressed,
  });

  final bool isLoading;
  final String? errorMessage;
  final VoidCallback onPressed;

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
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Container(
                width: 42,
                height: 42,
                decoration: BoxDecoration(
                  color: colorScheme.primary.withAlpha(18),
                  shape: BoxShape.circle,
                ),
                child: Icon(Icons.flag_outlined, color: colorScheme.primary),
              ),
              const SizedBox(width: 14),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Finalize o dia',
                      style: Theme.of(context).textTheme.titleMedium
                          ?.copyWith(fontWeight: FontWeight.w800),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      'Ao concluir todos os exercícios, '
                      'marque o dia como concluído para '
                      'registrar seu progresso.',
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

          if (errorMessage != null) ...[
            const SizedBox(height: 14),
            Text(
              errorMessage!,
              style: Theme.of(context).textTheme.bodySmall?.copyWith(
                color: colorScheme.error,
                fontWeight: FontWeight.w600,
              ),
            ),
          ],

          const SizedBox(height: 18),

          FilledButton.icon(
            onPressed: isLoading ? null : onPressed,
            icon: isLoading
                ? SizedBox(
                    width: 18,
                    height: 18,
                    child: CircularProgressIndicator(
                      strokeWidth: 2,
                      color: colorScheme.onPrimary,
                    ),
                  )
                : const Icon(Icons.check_circle_outline_rounded),
            label: Text(
              isLoading ? 'Concluindo...' : 'Marcar dia como concluído',
            ),
          ),
        ],
      ),
    );
  }
}

class _CompletedDayFooter extends StatelessWidget {
  const _CompletedDayFooter();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: Colors.green.withAlpha(10),
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: Colors.green.withAlpha(40)),
      ),
      child: const Row(
        children: [
          Icon(Icons.flag_rounded, color: Colors.green),
          SizedBox(width: 12),
          Expanded(
            child: Text(
              'Dia concluído. O registro já foi salvo no seu histórico.',
              style: TextStyle(
                color: Colors.green,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _EmptyExercisesCard extends StatelessWidget {
  const _EmptyExercisesCard();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(24),
      decoration: BoxDecoration(
        color: Theme.of(context).colorScheme.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(
          color: Theme.of(context).colorScheme.outlineVariant.withAlpha(100),
        ),
      ),
      child: Text(
        'Nenhum exercício foi configurado neste dia.',
        textAlign: TextAlign.center,
        style: Theme.of(context).textTheme.bodyMedium
            ?.copyWith(color: Theme.of(context).colorScheme.onSurfaceVariant),
      ),
    );
  }
}
