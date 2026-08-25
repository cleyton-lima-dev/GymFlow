import 'package:flutter/material.dart';
import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_bottom_navigation.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_page_header.dart';
import 'package:gymflow/features/workouts/data/workouts_service.dart';
import 'package:gymflow/features/workouts/models/workout_details.dart';
import 'package:gymflow/features/workouts/models/workout_draft.dart';
import 'package:gymflow/features/workouts/presentation/configure_workout_day_page.dart';
import 'package:gymflow/features/workouts/presentation/edit_workout_view_model.dart';
import 'package:provider/provider.dart';

class EditWorkoutPage extends StatelessWidget {
  const EditWorkoutPage({
    required this.workout,
    required this.studentName,
    this.onUpdated,
    super.key,
  });

  final WorkoutDetails workout;
  final String studentName;
  final VoidCallback? onUpdated;

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) => EditWorkoutViewModel(
        WorkoutsService(
          context.read<ApiClient>(),
        ),
        workout,
      ),
      child: _EditWorkoutView(
        studentName: studentName,
        onUpdated: onUpdated,
      ),
    );
  }
}

class _EditWorkoutView extends StatelessWidget {
  const _EditWorkoutView({
    required this.studentName,
    required this.onUpdated,
  });

  final String studentName;
  final VoidCallback? onUpdated;

  @override
  Widget build(BuildContext context) {
    final viewModel =
    context.watch<EditWorkoutViewModel>();

    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Scaffold(
      bottomNavigationBar:
      const ProfessorAdminBottomNavigation(
        currentItem: ProfessorAdminNavItem.students,
      ),
      body: SafeArea(
        bottom: false,
        child: SingleChildScrollView(
          padding: const EdgeInsets.fromLTRB(
            20,
            12,
            20,
            32,
          ),
          child: Center(
            child: ConstrainedBox(
              constraints: const BoxConstraints(
                maxWidth: 680,
              ),
              child: Column(
                crossAxisAlignment:
                CrossAxisAlignment.stretch,
                children: [
                  const ProfessorAdminPageHeader(),

                  const SizedBox(height: 24),

                  Text(
                    'Editar treino',
                    style:
                    theme.textTheme.headlineSmall?.copyWith(
                      fontWeight: FontWeight.w800,
                    ),
                  ),

                  const SizedBox(height: 6),

                  Text(
                    'Atualize a prescrição atual deste aluno.',
                    style:
                    theme.textTheme.bodyMedium?.copyWith(
                      color:
                      colorScheme.onSurfaceVariant,
                      height: 1.4,
                    ),
                  ),

                  const SizedBox(height: 20),

                  _StudentCard(
                    studentName: studentName,
                  ),

                  const SizedBox(height: 28),

                  Text(
                    'Dados do treino',
                    style:
                    theme.textTheme.titleLarge?.copyWith(
                      fontWeight: FontWeight.w800,
                    ),
                  ),

                  const SizedBox(height: 16),

                  TextFormField(
                    initialValue: viewModel.name,
                    textCapitalization:
                    TextCapitalization.sentences,
                    onChanged: viewModel.setName,
                    decoration: const InputDecoration(
                      labelText: 'Nome do treino',
                      hintText:
                      'Ex.: Hipertrofia Upper / Lower',
                    ),
                  ),

                  const SizedBox(height: 16),

                  TextFormField(
                    initialValue: viewModel.description,
                    minLines: 3,
                    maxLines: 5,
                    textCapitalization:
                    TextCapitalization.sentences,
                    onChanged:
                    viewModel.setDescription,
                    decoration: const InputDecoration(
                      labelText: 'Descrição',
                      hintText:
                      'Adicione uma descrição opcional',
                      alignLabelWithHint: true,
                    ),
                  ),

                  const SizedBox(height: 30),

                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          'Divisões do treino',
                          style: theme.textTheme.titleLarge
                              ?.copyWith(
                            fontWeight:
                            FontWeight.w800,
                          ),
                        ),
                      ),
                      Text(
                        _daysLabel(
                          viewModel.days.length,
                        ),
                        style: theme.textTheme.bodyMedium
                            ?.copyWith(
                          color: colorScheme
                              .onSurfaceVariant,
                          fontWeight:
                          FontWeight.w600,
                        ),
                      ),
                    ],
                  ),

                  const SizedBox(height: 6),

                  Text(
                    'Edite as divisões e exercícios que compõem este treino.',
                    style:
                    theme.textTheme.bodyMedium?.copyWith(
                      color:
                      colorScheme.onSurfaceVariant,
                      height: 1.4,
                    ),
                  ),

                  const SizedBox(height: 16),

                  if (viewModel.days.isEmpty)
                    const _EmptyDaysCard()
                  else
                    for (var index = 0;
                    index < viewModel.days.length;
                    index++) ...[
                      _DraftDayCard(
                        day: viewModel.days[index],
                        position: index + 1,
                        onTap: () => _editDay(
                          context,
                          viewModel,
                          index,
                        ),
                        onRemove: () =>
                            viewModel.removeDay(index),
                      ),
                      if (index <
                          viewModel.days.length - 1)
                        const SizedBox(height: 12),
                    ],

                  const SizedBox(height: 14),

                  OutlinedButton.icon(
                    onPressed: () => _addDay(
                      context,
                      viewModel,
                    ),
                    icon: const Icon(
                      Icons.add_rounded,
                    ),
                    label: const Text(
                      'Adicionar divisão',
                    ),
                  ),

                  if (viewModel.errorMessage != null) ...[
                    const SizedBox(height: 18),
                    _InlineError(
                      message:
                      viewModel.errorMessage!,
                    ),
                  ],

                  const SizedBox(height: 28),

                  _SummaryCard(
                    daysCount: viewModel.days.length,
                    exercisesCount:
                    _exerciseCount(viewModel.days),
                  ),

                  const SizedBox(height: 28),

                  FilledButton.icon(
                    onPressed: viewModel.isSubmitting
                        ? null
                        : () => _submit(
                      context,
                      viewModel,
                    ),
                    icon: viewModel.isSubmitting
                        ? const SizedBox(
                      width: 18,
                      height: 18,
                      child:
                      CircularProgressIndicator(
                        strokeWidth: 2,
                      ),
                    )
                        : const Icon(
                      Icons.check_rounded,
                    ),
                    label: Text(
                      viewModel.isSubmitting
                          ? 'Salvando...'
                          : 'Salvar alterações',
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }

  Future<void> _addDay(
      BuildContext context,
      EditWorkoutViewModel viewModel,
      ) async {
    final day =
    await Navigator.of(context).push<WorkoutDraftDay>(
      MaterialPageRoute(
        builder: (_) => ConfigureWorkoutDayPage(
          position: viewModel.days.length + 1,
        ),
      ),
    );

    if (!context.mounted || day == null) {
      return;
    }

    viewModel.addDay(day);
  }

  Future<void> _editDay(
      BuildContext context,
      EditWorkoutViewModel viewModel,
      int index,
      ) async {
    final currentDay = viewModel.days[index];

    final editedDay =
    await Navigator.of(context).push<WorkoutDraftDay>(
      MaterialPageRoute(
        builder: (_) => ConfigureWorkoutDayPage(
          position: index + 1,
          initialDay: currentDay,
        ),
      ),
    );

    if (!context.mounted || editedDay == null) {
      return;
    }

    viewModel.replaceDay(
      index,
      editedDay,
    );
  }

  Future<void> _submit(
      BuildContext context,
      EditWorkoutViewModel viewModel,
      ) async {
    final updated = await viewModel.submit();

    if (!context.mounted || !updated) {
      return;
    }

    onUpdated?.call();
  }
}

class _StudentCard extends StatelessWidget {
  const _StudentCard({
    required this.studentName,
  });

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
        border: Border.all(
          color:
          colorScheme.outlineVariant.withAlpha(120),
        ),
      ),
      child: Row(
        children: [
          Container(
            width: 48,
            height: 48,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color:
              colorScheme.primary.withAlpha(18),
            ),
            child: Text(
              _initials(studentName),
              style:
              theme.textTheme.titleMedium?.copyWith(
                color: colorScheme.primary,
                fontWeight: FontWeight.w800,
              ),
            ),
          ),

          const SizedBox(width: 14),

          Expanded(
            child: Column(
              crossAxisAlignment:
              CrossAxisAlignment.start,
              children: [
                Text(
                  studentName,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style:
                  theme.textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.w800,
                  ),
                ),
                const SizedBox(height: 3),
                Text(
                  'Aluno',
                  style:
                  theme.textTheme.bodySmall?.copyWith(
                    color:
                    colorScheme.onSurfaceVariant,
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

class _DraftDayCard extends StatelessWidget {
  const _DraftDayCard({
    required this.day,
    required this.position,
    required this.onTap,
    required this.onRemove,
  });

  final WorkoutDraftDay day;
  final int position;
  final VoidCallback onTap;
  final VoidCallback onRemove;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

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
              color:
              colorScheme.outlineVariant.withAlpha(
                120,
              ),
            ),
          ),
          child: Row(
            children: [
              Container(
                width: 40,
                height: 40,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  color:
                  colorScheme.primary.withAlpha(18),
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
                          .textTheme.titleMedium
                          ?.copyWith(
                        fontWeight:
                        FontWeight.w800,
                      ),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      _exerciseLabel(
                        day.exercises.length,
                      ),
                      style: theme
                          .textTheme.bodySmall
                          ?.copyWith(
                        color: colorScheme
                            .onSurfaceVariant,
                      ),
                    ),
                  ],
                ),
              ),

              IconButton(
                tooltip: 'Remover divisão',
                onPressed: onRemove,
                icon: const Icon(
                  Icons.delete_outline_rounded,
                ),
              ),

              Icon(
                Icons.chevron_right_rounded,
                color:
                colorScheme.onSurfaceVariant,
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _EmptyDaysCard extends StatelessWidget {
  const _EmptyDaysCard();

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: 20,
        vertical: 24,
      ),
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
            Icons.view_day_outlined,
            color: colorScheme.primary,
            size: 36,
          ),
          const SizedBox(height: 12),
          Text(
            'Nenhuma divisão',
            style:
            theme.textTheme.titleMedium?.copyWith(
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 5),
          Text(
            'Adicione pelo menos uma divisão para salvar o treino.',
            textAlign: TextAlign.center,
            style:
            theme.textTheme.bodyMedium?.copyWith(
              color:
              colorScheme.onSurfaceVariant,
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
  });

  final int daysCount;
  final int exercisesCount;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: colorScheme.primary.withAlpha(12),
        borderRadius: BorderRadius.circular(16),
      ),
      child: Row(
        children: [
          Expanded(
            child: _Metric(
              value: '$daysCount',
              label: daysCount == 1
                  ? 'Divisão'
                  : 'Divisões',
            ),
          ),
          Expanded(
            child: _Metric(
              value: '$exercisesCount',
              label: exercisesCount == 1
                  ? 'Exercício'
                  : 'Exercícios',
            ),
          ),
        ],
      ),
    );
  }
}

class _Metric extends StatelessWidget {
  const _Metric({
    required this.value,
    required this.label,
  });

  final String value;
  final String label;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Column(
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
    );
  }
}

class _InlineError extends StatelessWidget {
  const _InlineError({
    required this.message,
  });

  final String message;

  @override
  Widget build(BuildContext context) {
    final colorScheme =
        Theme.of(context).colorScheme;

    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: colorScheme.error.withAlpha(15),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(
          color: colorScheme.error.withAlpha(70),
        ),
      ),
      child: Row(
        crossAxisAlignment:
        CrossAxisAlignment.start,
        children: [
          Icon(
            Icons.error_outline_rounded,
            size: 20,
            color: colorScheme.error,
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              message,
              style: TextStyle(
                color: colorScheme.error,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

int _exerciseCount(
    List<WorkoutDraftDay> days,
    ) {
  return days.fold<int>(
    0,
        (total, day) =>
    total + day.exercises.length,
  );
}

String _daysLabel(int count) {
  return count == 1
      ? '1 divisão'
      : '$count divisões';
}

String _exerciseLabel(int count) {
  return count == 1
      ? '1 exercício'
      : '$count exercícios';
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
    return parts.first
        .substring(0, 1)
        .toUpperCase();
  }

  return '${parts.first.substring(0, 1)}'
      '${parts.last.substring(0, 1)}'
      .toUpperCase();
}
