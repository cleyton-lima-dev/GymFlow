import 'package:flutter/material.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_page_header.dart';
import 'package:gymflow/features/workouts/models/workout_draft.dart';

class ConfigureWorkoutExercisePage extends StatefulWidget {
  const ConfigureWorkoutExercisePage({
    required this.exerciseId,
    required this.exerciseName,
    required this.muscleGroup,
    this.initialExercise,
    super.key,
  });

  final String exerciseId;
  final String exerciseName;
  final String muscleGroup;

  final WorkoutDraftExercise? initialExercise;

  @override
  State<ConfigureWorkoutExercisePage> createState() =>
      _ConfigureWorkoutExercisePageState();
}

class _ConfigureWorkoutExercisePageState
    extends State<ConfigureWorkoutExercisePage> {
  late final TextEditingController _setsController;
  late final TextEditingController _repetitionsController;
  late final TextEditingController _restController;
  late final TextEditingController _notesController;

  String? _errorMessage;

  @override
  void initState() {
    super.initState();

    final initial = widget.initialExercise;

    _setsController = TextEditingController(
      text: initial == null
          ? ''
          : '${initial.sets}',
    );

    _repetitionsController = TextEditingController(
      text: initial?.repetitions ?? '',
    );

    _restController = TextEditingController(
      text: initial == null
          ? ''
          : '${initial.restSeconds}',
    );

    _notesController = TextEditingController(
      text: initial?.notes ?? '',
    );
  }

  @override
  void dispose() {
    _setsController.dispose();
    _repetitionsController.dispose();
    _restController.dispose();
    _notesController.dispose();

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
                    widget.initialExercise == null
                        ? 'Configurar exercício'
                        : 'Editar exercício',
                    style:
                    theme.textTheme.headlineSmall?.copyWith(
                      fontWeight: FontWeight.w800,
                    ),
                  ),

                  const SizedBox(height: 20),

                  Container(
                    padding: const EdgeInsets.all(16),
                    decoration: BoxDecoration(
                      color: colorScheme.surface,
                      borderRadius:
                      BorderRadius.circular(16),
                      border: Border.all(
                        color: colorScheme
                            .outlineVariant
                            .withAlpha(120),
                      ),
                    ),
                    child: Row(
                      children: [
                        Container(
                          width: 46,
                          height: 46,
                          decoration: BoxDecoration(
                            shape: BoxShape.circle,
                            color: colorScheme.primary
                                .withAlpha(18),
                          ),
                          child: Icon(
                            Icons.fitness_center_rounded,
                            color: colorScheme.primary,
                          ),
                        ),

                        const SizedBox(width: 14),

                        Expanded(
                          child: Column(
                            crossAxisAlignment:
                            CrossAxisAlignment.start,
                            children: [
                              Text(
                                widget.exerciseName,
                                style: theme
                                    .textTheme.titleMedium
                                    ?.copyWith(
                                  fontWeight:
                                  FontWeight.w800,
                                ),
                              ),

                              const SizedBox(height: 4),

                              Text(
                                widget.muscleGroup,
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
                      ],
                    ),
                  ),

                  const SizedBox(height: 26),

                  Row(
                    children: [
                      Expanded(
                        child: TextField(
                          controller:
                          _setsController,
                          keyboardType:
                          TextInputType.number,
                          onChanged: (_) =>
                              _clearError(),
                          decoration:
                          const InputDecoration(
                            labelText: 'Séries',
                            hintText: 'Ex.: 4',
                          ),
                        ),
                      ),

                      const SizedBox(width: 14),

                      Expanded(
                        flex: 2,
                        child: TextField(
                          controller:
                          _repetitionsController,
                          textInputAction:
                          TextInputAction.next,
                          onChanged: (_) =>
                              _clearError(),
                          decoration:
                          const InputDecoration(
                            labelText: 'Repetições',
                            hintText: 'Ex.: 8-12',
                          ),
                        ),
                      ),
                    ],
                  ),

                  const SizedBox(height: 16),

                  TextField(
                    controller: _restController,
                    keyboardType:
                    TextInputType.number,
                    onChanged: (_) => _clearError(),
                    decoration: const InputDecoration(
                      labelText:
                      'Descanso entre séries',
                      hintText: 'Ex.: 90',
                      suffixText: 'segundos',
                    ),
                  ),

                  const SizedBox(height: 16),

                  TextField(
                    controller: _notesController,
                    minLines: 3,
                    maxLines: 5,
                    textCapitalization:
                    TextCapitalization.sentences,
                    decoration: const InputDecoration(
                      labelText: 'Observações',
                      hintText:
                      'Ex.: Progressão de carga',
                      alignLabelWithHint: true,
                    ),
                  ),

                  if (_errorMessage != null) ...[
                    const SizedBox(height: 18),

                    Container(
                      padding: const EdgeInsets.all(14),
                      decoration: BoxDecoration(
                        color: colorScheme.error
                            .withAlpha(15),
                        borderRadius:
                        BorderRadius.circular(12),
                        border: Border.all(
                          color: colorScheme.error
                              .withAlpha(70),
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
                                color:
                                colorScheme.error,
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
                    label: Text(
                      widget.initialExercise == null
                          ? 'Adicionar exercício'
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

  void _clearError() {
    if (_errorMessage == null) {
      return;
    }

    setState(() {
      _errorMessage = null;
    });
  }

  void _save() {
    final sets = int.tryParse(
      _setsController.text.trim(),
    );

    final repetitions =
    _repetitionsController.text.trim();

    final restSeconds = int.tryParse(
      _restController.text.trim(),
    );

    if (sets == null || sets <= 0) {
      _showError(
        'Informe uma quantidade válida de séries.',
      );
      return;
    }

    if (repetitions.isEmpty) {
      _showError(
        'Informe as repetições do exercício.',
      );
      return;
    }

    if (restSeconds == null ||
        restSeconds < 0) {
      _showError(
        'Informe um tempo de descanso válido.',
      );
      return;
    }

    final notes = _notesController.text.trim();

    Navigator.of(context).pop(
      WorkoutDraftExercise(
        id: widget.initialExercise?.id,
        exerciseId: widget.exerciseId,
        exerciseName: widget.exerciseName,
        muscleGroup: widget.muscleGroup,
        sets: sets,
        repetitions: repetitions,
        restSeconds: restSeconds,
        notes: notes.isEmpty
            ? null
            : notes,
      ),
    );
  }

  void _showError(String message) {
    setState(() {
      _errorMessage = message;
    });
  }
}
