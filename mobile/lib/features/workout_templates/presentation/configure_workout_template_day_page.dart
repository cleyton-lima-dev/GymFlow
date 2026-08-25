import 'package:flutter/material.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_page_header.dart';
import 'package:gymflow/features/workout_templates/models/workout_template_draft.dart';
import 'package:gymflow/features/exercises/models/exercise_summary.dart';
import 'package:gymflow/features/workout_templates/presentation/configure_workout_template_exercise_page.dart';
import 'package:gymflow/features/workout_templates/presentation/select_workout_template_exercise_page.dart';

class ConfigureWorkoutTemplateDayPage extends StatefulWidget {
  const ConfigureWorkoutTemplateDayPage({
    required this.dayNumber,
    this.initialDay,
    this.onAddExerciseTap,
    super.key,
  });

  final int dayNumber;
  final WorkoutTemplateDraftDay? initialDay;
  final VoidCallback? onAddExerciseTap;

  @override
  State<ConfigureWorkoutTemplateDayPage> createState() =>
      _ConfigureWorkoutTemplateDayPageState();
}

class _ConfigureWorkoutTemplateDayPageState
    extends State<ConfigureWorkoutTemplateDayPage> {
  late final TextEditingController _nameController;
  late final List<WorkoutTemplateDraftExercise> _exercises;

  String? _errorMessage;

  @override
  void initState() {
    super.initState();

    _nameController = TextEditingController(
      text: widget.initialDay?.name ?? '',
    );

    _exercises = widget.initialDay?.exercises
        .map(_copyExercise)
        .toList() ??
        [];
  }

  @override
  void dispose() {
    _nameController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Scaffold(
      body: SafeArea(
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
                    widget.initialDay == null
                        ? 'Configurar dia'
                        : 'Editar dia',
                    style:
                    theme.textTheme.headlineSmall?.copyWith(
                      fontWeight: FontWeight.w800,
                    ),
                  ),

                  const SizedBox(height: 6),

                  Text(
                    'Dia ${widget.dayNumber} do modelo',
                    style: theme.textTheme.bodyMedium?.copyWith(
                      color: colorScheme.onSurfaceVariant,
                    ),
                  ),

                  const SizedBox(height: 26),

                  TextField(
                    controller: _nameController,
                    textCapitalization:
                    TextCapitalization.sentences,
                    onChanged: (_) {
                      if (_errorMessage != null) {
                        setState(() {
                          _errorMessage = null;
                        });
                      }
                    },
                    decoration: const InputDecoration(
                      labelText: 'Nome do dia',
                      hintText: 'Ex.: Treino A - Peito',
                    ),
                  ),

                  const SizedBox(height: 30),

                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          'Exercícios',
                          style:
                          theme.textTheme.titleLarge?.copyWith(
                            fontWeight: FontWeight.w800,
                          ),
                        ),
                      ),
                      Text(
                        _exerciseLabel(_exercises.length),
                        style:
                        theme.textTheme.bodyMedium?.copyWith(
                          color:
                          colorScheme.onSurfaceVariant,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                    ],
                  ),

                  const SizedBox(height: 6),

                  Text(
                    'Defina os exercícios e suas configurações para este dia.',
                    style: theme.textTheme.bodyMedium?.copyWith(
                      color: colorScheme.onSurfaceVariant,
                    ),
                  ),

                  const SizedBox(height: 16),

                  if (_exercises.isEmpty)
                    const _EmptyExercisesCard()
                  else
                    _ExercisesCard(
                      exercises: _exercises,
                      onEdit: _editExercise,
                      onRemove: _removeExercise,
                    ),

                  const SizedBox(height: 14),

                  OutlinedButton.icon(
                    onPressed: _addExercise,
                    icon: const Icon(
                      Icons.add_rounded,
                    ),
                    label: const Text(
                      'Adicionar exercício',
                    ),
                  ),

                  if (_errorMessage != null) ...[
                    const SizedBox(height: 18),

                    Container(
                      padding: const EdgeInsets.all(14),
                      decoration: BoxDecoration(
                        color:
                        colorScheme.error.withAlpha(15),
                        borderRadius:
                        BorderRadius.circular(12),
                        border: Border.all(
                          color:
                          colorScheme.error.withAlpha(70),
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
                              _errorMessage!,
                              style: TextStyle(
                                color: colorScheme.error,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],

                  const SizedBox(height: 30),

                  FilledButton.icon(
                    onPressed: _save,
                    icon: const Icon(
                      Icons.check_rounded,
                    ),
                    label: const Text(
                      'Salvar dia',
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

  Future<void> _addExercise() async {
    final selectedExercise =
    await Navigator.of(context).push<ExerciseSummary>(
      MaterialPageRoute(
        builder: (_) =>
        const SelectWorkoutTemplateExercisePage(),
      ),
    );

    if (!mounted || selectedExercise == null) {
      return;
    }

    final configuredExercise =
    await Navigator.of(context)
        .push<WorkoutTemplateDraftExercise>(
      MaterialPageRoute(
        builder: (_) =>
            ConfigureWorkoutTemplateExercisePage(
              exerciseId: selectedExercise.id,
              exerciseName: selectedExercise.name,
              muscleGroup: selectedExercise.muscleGroup,
            ),
      ),
    );

    if (!mounted || configuredExercise == null) {
      return;
    }

    setState(() {
      _exercises.add(configuredExercise);
    });
  }

  Future<void> _editExercise(int index) async {
    final currentExercise = _exercises[index];

    final editedExercise =
    await Navigator.of(context)
        .push<WorkoutTemplateDraftExercise>(
      MaterialPageRoute(
        builder: (_) =>
            ConfigureWorkoutTemplateExercisePage(
              exerciseId: currentExercise.exerciseId,
              exerciseName:
              currentExercise.exerciseName,
              muscleGroup:
              currentExercise.muscleGroup,
              initialExercise: currentExercise,
            ),
      ),
    );

    if (!mounted || editedExercise == null) {
      return;
    }

    setState(() {
      _exercises[index] = editedExercise;
    });
  }

  void _removeExercise(int index) {
    setState(() {
      _exercises.removeAt(index);
    });
  }

  void _save() {
    final name = _nameController.text.trim();

    if (name.isEmpty) {
      setState(() {
        _errorMessage =
        'Informe o nome do dia de treino.';
      });

      return;
    }

    Navigator.of(context).pop(
      WorkoutTemplateDraftDay(
        name: name,
        exercises: _exercises,
      ),
    );
  }

  WorkoutTemplateDraftExercise _copyExercise(
      WorkoutTemplateDraftExercise exercise,
      ) {
    return WorkoutTemplateDraftExercise(
      exerciseId: exercise.exerciseId,
      exerciseName: exercise.exerciseName,
      muscleGroup: exercise.muscleGroup,
      sets: exercise.sets,
      repetitions: exercise.repetitions,
      restSeconds: exercise.restSeconds,
      notes: exercise.notes,
    );
  }
}

class _ExercisesCard extends StatelessWidget {
  const _ExercisesCard({
    required this.exercises,
    required this.onEdit,
    required this.onRemove,
  });

  final List<WorkoutTemplateDraftExercise> exercises;
  final ValueChanged<int> onEdit;
  final ValueChanged<int> onRemove;

  @override
  Widget build(BuildContext context) {
    final colorScheme =
        Theme.of(context).colorScheme;

    return Container(
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
          for (var index = 0;
          index < exercises.length;
          index++) ...[
            _ExerciseRow(
              position: index + 1,
              exercise: exercises[index],
              onTap: () => onEdit(index),
              onRemove: () => onRemove(index),
            ),
            if (index < exercises.length - 1)
              Divider(
                height: 1,
                color: colorScheme.outlineVariant
                    .withAlpha(100),
              ),
          ],
        ],
      ),
    );
  }
}

class _ExerciseRow extends StatelessWidget {
  const _ExerciseRow({
    required this.position,
    required this.exercise,
    required this.onTap,
    required this.onRemove,
  });

  final int position;
  final WorkoutTemplateDraftExercise exercise;
  final VoidCallback onTap;
  final VoidCallback onRemove;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return InkWell(
        onTap: onTap,
        child: Padding(
      padding: const EdgeInsets.fromLTRB(
        14,
        12,
        8,
        12,
      ),
      child: Row(
        crossAxisAlignment:
        CrossAxisAlignment.start,
        children: [
          Container(
            width: 30,
            height: 30,
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
                  exercise.exerciseName,
                  style:
                  theme.textTheme.bodyLarge?.copyWith(
                    fontWeight: FontWeight.w800,
                  ),
                ),

                const SizedBox(height: 3),

                Text(
                  exercise.muscleGroup,
                  style:
                  theme.textTheme.bodySmall?.copyWith(
                    color:
                    colorScheme.onSurfaceVariant,
                  ),
                ),

                const SizedBox(height: 6),

                Text(
                  '${exercise.sets} séries  •  '
                      '${exercise.repetitions} reps  •  '
                      '${_restText(exercise.restSeconds)}',
                  style:
                  theme.textTheme.bodySmall?.copyWith(
                    color:
                    colorScheme.onSurfaceVariant,
                    fontWeight: FontWeight.w600,
                  ),
                ),

                if (exercise.notes != null &&
                    exercise.notes!
                        .trim()
                        .isNotEmpty) ...[
                  const SizedBox(height: 5),
                  Text(
                    exercise.notes!.trim(),
                    style: theme.textTheme.bodySmall
                        ?.copyWith(
                      color:
                      colorScheme.onSurfaceVariant,
                    ),
                  ),
                ],
              ],
            ),
          ),

          IconButton(
            tooltip: 'Remover exercício',
            onPressed: onRemove,
            icon: const Icon(
              Icons.delete_outline_rounded,
            ),
          ),
        ],
      ),
        ),
    );
  }
}

class _EmptyExercisesCard extends StatelessWidget {
  const _EmptyExercisesCard();

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
          Container(
            width: 48,
            height: 48,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color:
              colorScheme.primary.withAlpha(18),
            ),
            child: Icon(
              Icons.fitness_center_rounded,
              color: colorScheme.primary,
            ),
          ),

          const SizedBox(height: 12),

          Text(
            'Nenhum exercício adicionado',
            style: theme.textTheme.titleMedium?.copyWith(
              fontWeight: FontWeight.w800,
            ),
          ),

          const SizedBox(height: 5),

          Text(
            'Adicione exercícios para montar este dia de treino.',
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

String _exerciseLabel(int count) {
  return count == 1
      ? '1 exercício'
      : '$count exercícios';
}

String _restText(int seconds) {
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
  final remaining = seconds % 60;

  return '${minutes}min ${remaining}s descanso';
}
