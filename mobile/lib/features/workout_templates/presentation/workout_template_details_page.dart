import 'package:flutter/material.dart';
import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_bottom_navigation.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_page_header.dart';
import 'package:gymflow/features/workout_templates/data/workout_templates_service.dart';
import 'package:gymflow/features/workout_templates/models/workout_template_details.dart';
import 'package:gymflow/features/workout_templates/presentation/workout_template_details_view_model.dart';
import 'package:provider/provider.dart';

class WorkoutTemplateDetailsPage extends StatelessWidget {
  const WorkoutTemplateDetailsPage({
    required this.templateId,
    this.onEditTap,
    this.onDayTap,
    super.key,
  });

  final String templateId;
  final Future<bool> Function()? onEditTap;
  final ValueChanged<WorkoutTemplateDay>? onDayTap;

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) => WorkoutTemplateDetailsViewModel(
        WorkoutTemplatesService(
          context.read<ApiClient>(),
        ),
        templateId,
      )..load(),
      child: _WorkoutTemplateDetailsView(
        onEditTap: onEditTap,
        onDayTap: onDayTap,
      ),
    );
  }
}

class _WorkoutTemplateDetailsView extends StatelessWidget {
  const _WorkoutTemplateDetailsView({
    required this.onEditTap,
    required this.onDayTap,
  });

  final Future<bool> Function()? onEditTap;
  final ValueChanged<WorkoutTemplateDay>? onDayTap;

  @override
  Widget build(BuildContext context) {
    final viewModel =
    context.watch<WorkoutTemplateDetailsViewModel>();

    return Scaffold(
      bottomNavigationBar:
      const ProfessorAdminBottomNavigation(
        currentItem: ProfessorAdminNavItem.templates,
      ),
      body: SafeArea(
        child: RefreshIndicator(
          onRefresh: viewModel.refresh,
          child: CustomScrollView(
            physics:
            const AlwaysScrollableScrollPhysics(),
            slivers: [
              const SliverPadding(
                padding: EdgeInsets.fromLTRB(
                  20,
                  12,
                  20,
                  0,
                ),
                sliver: SliverToBoxAdapter(
                  child: ProfessorAdminPageHeader(),
                ),
              ),

              if (viewModel.isLoading)
                const SliverFillRemaining(
                  hasScrollBody: false,
                  child: Center(
                    child: CircularProgressIndicator(),
                  ),
                )
              else if (viewModel.errorMessage != null)
                SliverFillRemaining(
                  hasScrollBody: false,
                  child: _ErrorState(
                    message: viewModel.errorMessage!,
                    onRetry: viewModel.load,
                  ),
                )
              else if (viewModel.template != null)
                  SliverToBoxAdapter(
                    child: _Content(
                      template: viewModel.template!,
                      onEditTap: onEditTap == null
                          ? null
                          : () async {
                        final updated = await onEditTap!();

                        if (!updated) {
                          return;
                        }

                        await viewModel.refresh();
                      },
                      onDayTap: onDayTap,
                    ),
                  ),
            ],
          ),
        ),
      ),
    );
  }
}

class _Content extends StatelessWidget {
  const _Content({
    required this.template,
    required this.onEditTap,
    required this.onDayTap,
  });

  final WorkoutTemplateDetails template;
  final VoidCallback? onEditTap;
  final ValueChanged<WorkoutTemplateDay>? onDayTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    final days = [...template.days]
      ..sort(
            (a, b) => a.order.compareTo(b.order),
      );

    final totalExercises = days.fold<int>(
      0,
          (total, day) => total + day.exercises.length,
    );

    return Padding(
      padding: const EdgeInsets.fromLTRB(
        20,
        18,
        20,
        32,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment:
                  CrossAxisAlignment.start,
                  children: [
                    Text(
                      template.name,
                      style:
                      theme.textTheme.headlineSmall
                          ?.copyWith(
                        fontWeight: FontWeight.w800,
                      ),
                    ),

                    const SizedBox(height: 8),

                    _StatusBadge(
                      isActive: template.isActive,
                    ),
                  ],
                ),
              ),

              if (onEditTap != null) ...[
                const SizedBox(width: 12),
                OutlinedButton.icon(
                  onPressed: onEditTap,
                  icon: const Icon(
                    Icons.edit_outlined,
                    size: 19,
                  ),
                  label: const Text('Editar'),
                ),
              ],
            ],
          ),

          if (template.description != null &&
              template.description!
                  .trim()
                  .isNotEmpty) ...[
            const SizedBox(height: 16),
            Text(
              template.description!.trim(),
              style: theme.textTheme.bodyLarge?.copyWith(
                color: colorScheme.onSurfaceVariant,
                height: 1.45,
              ),
            ),
          ],

          const SizedBox(height: 22),

          _SummaryCard(
            daysCount: days.length,
            exercisesCount: totalExercises,
            createdAt: template.createdAt,
            updatedAt: template.updatedAt,
          ),

          const SizedBox(height: 28),

          Row(
            children: [
              Expanded(
                child: Text(
                  'Dias do modelo',
                  style:
                  theme.textTheme.titleLarge?.copyWith(
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),
              Text(
                days.length == 1
                    ? '1 dia'
                    : '${days.length} dias',
                style:
                theme.textTheme.bodyMedium?.copyWith(
                  color: colorScheme.onSurfaceVariant,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ],
          ),

          const SizedBox(height: 14),

          if (days.isEmpty)
            const _EmptyDaysState()
          else
            for (var index = 0;
            index < days.length;
            index++) ...[
              _DayCard(
                day: days[index],
                position: index + 1,
                onTap: onDayTap == null
                    ? null
                    : () => onDayTap!(days[index]),
              ),

              if (index < days.length - 1)
                const SizedBox(height: 12),
            ],

          const SizedBox(height: 22),

          Container(
            padding: const EdgeInsets.all(16),
            decoration: BoxDecoration(
              color: colorScheme.primary.withAlpha(16),
              borderRadius: BorderRadius.circular(14),
            ),
            child: Row(
              crossAxisAlignment:
              CrossAxisAlignment.start,
              children: [
                Icon(
                  Icons.info_outline_rounded,
                  color: colorScheme.primary,
                ),
                const SizedBox(width: 12),
                const Expanded(
                  child: Text(
                    'Este modelo pode ser reutilizado para '
                        'criar treinos de diferentes alunos.',
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

class _SummaryCard extends StatelessWidget {
  const _SummaryCard({
    required this.daysCount,
    required this.exercisesCount,
    required this.createdAt,
    required this.updatedAt,
  });

  final int daysCount;
  final int exercisesCount;
  final DateTime createdAt;
  final DateTime? updatedAt;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(
          color:
          colorScheme.outlineVariant.withAlpha(120),
        ),
      ),
      child: Column(
        children: [
          Row(
            children: [
              Expanded(
                child: _SummaryMetric(
                  icon:
                  Icons.calendar_today_outlined,
                  label: 'Dias',
                  value: '$daysCount',
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: _SummaryMetric(
                  icon:
                  Icons.fitness_center_rounded,
                  label: 'Exercícios',
                  value: '$exercisesCount',
                ),
              ),
            ],
          ),

          const SizedBox(height: 18),
          const Divider(height: 1),
          const SizedBox(height: 16),

          Row(
            children: [
              Expanded(
                child: _DateInformation(
                  label: 'Criado em',
                  value: _formatDate(createdAt),
                ),
              ),

              if (updatedAt != null) ...[
                const SizedBox(width: 12),
                Expanded(
                  child: _DateInformation(
                    label: 'Atualizado em',
                    value:
                    _formatDate(updatedAt!),
                  ),
                ),
              ],
            ],
          ),
        ],
      ),
    );
  }
}

class _SummaryMetric extends StatelessWidget {
  const _SummaryMetric({
    required this.icon,
    required this.label,
    required this.value,
  });

  final IconData icon;
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Row(
      children: [
        Container(
          width: 42,
          height: 42,
          decoration: BoxDecoration(
            shape: BoxShape.circle,
            color: colorScheme.primary.withAlpha(18),
          ),
          child: Icon(
            icon,
            size: 20,
            color: colorScheme.primary,
          ),
        ),
        const SizedBox(width: 10),
        Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              value,
              style:
              theme.textTheme.titleLarge?.copyWith(
                fontWeight: FontWeight.w800,
              ),
            ),
            Text(
              label,
              style:
              theme.textTheme.bodySmall?.copyWith(
                color:
                colorScheme.onSurfaceVariant,
              ),
            ),
          ],
        ),
      ],
    );
  }
}

class _DateInformation extends StatelessWidget {
  const _DateInformation({
    required this.label,
    required this.value,
  });

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          label,
          style: theme.textTheme.bodySmall?.copyWith(
            color: colorScheme.onSurfaceVariant,
          ),
        ),
        const SizedBox(height: 4),
        Text(
          value,
          style: theme.textTheme.bodyMedium?.copyWith(
            fontWeight: FontWeight.w700,
          ),
        ),
      ],
    );
  }
}

class _DayCard extends StatelessWidget {
  const _DayCard({
    required this.day,
    required this.position,
    required this.onTap,
  });

  final WorkoutTemplateDay day;
  final int position;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    final exercises = [...day.exercises]
      ..sort(
            (a, b) => a.order.compareTo(b.order),
      );

    return Material(
      color: colorScheme.surface,
      borderRadius: BorderRadius.circular(16),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(16),
        child: Container(
          padding: const EdgeInsets.all(16),
          decoration: BoxDecoration(
            borderRadius:
            BorderRadius.circular(16),
            border: Border.all(
              color: colorScheme
                  .outlineVariant
                  .withAlpha(120),
            ),
          ),
          child: Column(
            crossAxisAlignment:
            CrossAxisAlignment.stretch,
            children: [
              Row(
                children: [
                  Container(
                    width: 38,
                    height: 38,
                    alignment: Alignment.center,
                    decoration: BoxDecoration(
                      shape: BoxShape.circle,
                      color: colorScheme.primary
                          .withAlpha(20),
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
                      crossAxisAlignment:
                      CrossAxisAlignment.start,
                      children: [
                        Text(
                          day.name,
                          style: theme
                              .textTheme
                              .titleMedium
                              ?.copyWith(
                            fontWeight:
                            FontWeight.w800,
                          ),
                        ),
                        const SizedBox(height: 3),
                        Text(
                          exercises.length == 1
                              ? '1 exercício'
                              : '${exercises.length} exercícios',
                          style: theme
                              .textTheme
                              .bodySmall
                              ?.copyWith(
                            color: colorScheme
                                .onSurfaceVariant,
                          ),
                        ),
                      ],
                    ),
                  ),

                  if (onTap != null)
                    Icon(
                      Icons.chevron_right_rounded,
                      color: colorScheme
                          .onSurfaceVariant,
                    ),
                ],
              ),

              if (exercises.isNotEmpty) ...[
                const SizedBox(height: 14),
                const Divider(height: 1),
                const SizedBox(height: 12),

                for (var index = 0;
                index < exercises.length;
                index++) ...[
                  _ExercisePreview(
                    exercise: exercises[index],
                  ),

                  if (index <
                      exercises.length - 1)
                    const SizedBox(height: 10),
                ],
              ],
            ],
          ),
        ),
      ),
    );
  }
}

class _ExercisePreview extends StatelessWidget {
  const _ExercisePreview({
    required this.exercise,
  });

  final WorkoutTemplateExercise exercise;

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
                style:
                theme.textTheme.bodyMedium?.copyWith(
                  fontWeight: FontWeight.w700,
                ),
              ),

              const SizedBox(height: 2),

              Text(
                '${exercise.sets} séries • '
                    '${exercise.repetitions} repetições • '
                    '${_formatRest(exercise.restSeconds)}',
                style: theme.textTheme.bodySmall?.copyWith(
                  color:
                  colorScheme.onSurfaceVariant,
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class _StatusBadge extends StatelessWidget {
  const _StatusBadge({
    required this.isActive,
  });

  final bool isActive;

  @override
  Widget build(BuildContext context) {
    final color = isActive
        ? const Color(0xFF16A34A)
        : Theme.of(context)
        .colorScheme
        .onSurfaceVariant;

    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: 10,
        vertical: 5,
      ),
      decoration: BoxDecoration(
        color: color.withAlpha(18),
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

class _EmptyDaysState extends StatelessWidget {
  const _EmptyDaysState();

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Container(
      padding: const EdgeInsets.all(24),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(
          color:
          colorScheme.outlineVariant.withAlpha(120),
        ),
      ),
      child: Column(
        children: [
          Icon(
            Icons.calendar_view_day_outlined,
            color: colorScheme.primary,
            size: 36,
          ),
          const SizedBox(height: 12),
          Text(
            'Nenhum dia configurado',
            style: theme.textTheme.titleMedium?.copyWith(
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 6),
          Text(
            'Este modelo ainda não possui dias de treino.',
            textAlign: TextAlign.center,
            style: theme.textTheme.bodyMedium?.copyWith(
              color: colorScheme.onSurfaceVariant,
            ),
          ),
        ],
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
    final colorScheme =
        Theme.of(context).colorScheme;

    return Padding(
      padding: const EdgeInsets.all(32),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Icon(
            Icons.error_outline_rounded,
            color: colorScheme.error,
            size: 42,
          ),
          const SizedBox(height: 14),
          Text(
            message,
            textAlign: TextAlign.center,
          ),
          const SizedBox(height: 18),
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

String _formatDate(DateTime date) {
  final day =
  date.day.toString().padLeft(2, '0');

  final month =
  date.month.toString().padLeft(2, '0');

  return '$day/$month/${date.year}';
}

String _formatRest(int seconds) {
  if (seconds < 60) {
    return '${seconds}s descanso';
  }

  if (seconds % 60 == 0) {
    final minutes = seconds ~/ 60;

    return minutes == 1
        ? '1 min descanso'
        : '$minutes min descanso';
  }

  final minutes = seconds ~/ 60;
  final remainingSeconds = seconds % 60;

  return '${minutes}min ${remainingSeconds}s descanso';
}
