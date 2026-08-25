import 'package:flutter/material.dart';
import 'package:gymflow/app/session/app_role.dart';
import 'package:gymflow/app/session/session_controller.dart';
import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/features/exercises/data/exercises_service.dart';
import 'package:gymflow/features/exercises/models/exercise_summary.dart';
import 'package:gymflow/features/exercises/presentation/edit_exercise_view_model.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_bottom_navigation.dart';
import 'package:provider/provider.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_page_header.dart';

class EditExercisePage extends StatelessWidget {
  const EditExercisePage({
    required this.exercise,
    super.key,
  });

  final ExerciseSummary exercise;

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) => EditExerciseViewModel(
        ExercisesService(
          context.read<ApiClient>(),
        ),
        exercise,
      ),
      child: const _EditExerciseView(),
    );
  }
}

class _EditExerciseView extends StatefulWidget {
  const _EditExerciseView();

  @override
  State<_EditExerciseView> createState() =>
      _EditExerciseViewState();
}

class _EditExerciseViewState
    extends State<_EditExerciseView> {
  final _formKey = GlobalKey<FormState>();

  late final TextEditingController _nameController;
  late final TextEditingController _muscleGroupController;
  late final TextEditingController _descriptionController;
  bool _didChange = false;

  @override
  void initState() {
    super.initState();

    final exercise =
        context.read<EditExerciseViewModel>().exercise;

    _nameController = TextEditingController(
      text: exercise.name,
    );

    _muscleGroupController = TextEditingController(
      text: exercise.muscleGroup,
    );

    _descriptionController = TextEditingController(
      text: exercise.description ?? '',
    );
  }

  @override
  void dispose() {
    _nameController.dispose();
    _muscleGroupController.dispose();
    _descriptionController.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    FocusScope.of(context).unfocus();

    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    final viewModel =
    context.read<EditExerciseViewModel>();

    final success = await viewModel.save(
      name: _nameController.text,
      muscleGroup: _muscleGroupController.text,
      description: _descriptionController.text,
    );

    if (!mounted || !success) {
      return;
    }

    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(
        content: Text(
          'Exercício atualizado com sucesso.',
        ),
      ),
    );

    Navigator.of(context).pop(true);
  }

  Future<void> _requestStatusChange(
      bool newStatus,
      ) async {
    final viewModel =
    context.read<EditExerciseViewModel>();

    if (viewModel.isActive == newStatus) {
      return;
    }

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) {
        final action =
        newStatus ? 'ativar' : 'inativar';

        return AlertDialog(
          title: Text(
            newStatus
                ? 'Ativar exercício?'
                : 'Inativar exercício?',
          ),
          content: Text(
            newStatus
                ? 'O exercício voltará a ficar disponível '
                'para uso em modelos e treinos.'
                : 'O exercício ficará indisponível para '
                'novos usos, mas os dados já existentes '
                'serão preservados.',
          ),
          actions: [
            TextButton(
              onPressed: () =>
                  Navigator.of(dialogContext).pop(false),
              child: const Text('Cancelar'),
            ),
            FilledButton(
              onPressed: () =>
                  Navigator.of(dialogContext).pop(true),
              child: Text(
                action[0].toUpperCase() +
                    action.substring(1),
              ),
            ),
          ],
        );
      },
    );

    if (!mounted || confirmed != true) {
      return;
    }

    final success =
    await viewModel.updateStatus(newStatus);

    if (!mounted || !success) {
      return;
    }
    setState(() {
      _didChange = true;
    });

    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(
          newStatus
              ? 'Exercício ativado com sucesso.'
              : 'Exercício inativado com sucesso.',
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final viewModel =
    context.watch<EditExerciseViewModel>();

    final user =
        context.watch<SessionController>().user;

    final isAdmin =
        user?.role == AppRole.admin;

    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Scaffold(
      bottomNavigationBar:
      const ProfessorAdminBottomNavigation(
        currentItem: ProfessorAdminNavItem.more,
      ),
      body: SafeArea(
        child: Form(
          key: _formKey,
          child: CustomScrollView(
            keyboardDismissBehavior:
            ScrollViewKeyboardDismissBehavior.onDrag,
            slivers: [
              SliverToBoxAdapter(
                child: Padding(
                  padding: const EdgeInsets.fromLTRB(
                    20,
                    12,
                    20,
                    32,
                  ),
                  child: Column(
                    crossAxisAlignment:
                    CrossAxisAlignment.stretch,
                    children: [
                      const ProfessorAdminPageHeader(),

                      const SizedBox(height: 18),

                      Text(
                        'Editar exercício',
                        style:
                        theme.textTheme.headlineSmall
                            ?.copyWith(
                          fontWeight: FontWeight.w800,
                        ),
                      ),

                      const SizedBox(height: 4),

                      Text(
                        'Atualize as informações do exercício.',
                        style:
                        theme.textTheme.bodyMedium
                            ?.copyWith(
                          color: colorScheme
                              .onSurfaceVariant,
                        ),
                      ),

                      const SizedBox(height: 20),

                      _FormSection(
                        title:
                        'Informações do exercício',
                        icon: Icons
                            .fitness_center_rounded,
                        child: Column(
                          children: [
                            TextFormField(
                              controller:
                              _nameController,
                              enabled:
                              !viewModel.isBusy,
                              maxLength: 150,
                              textCapitalization:
                              TextCapitalization
                                  .sentences,
                              textInputAction:
                              TextInputAction.next,
                              decoration:
                              const InputDecoration(
                                labelText:
                                'Nome do exercício *',
                                border:
                                OutlineInputBorder(),
                              ),
                              validator: (value) {
                                final normalized =
                                    value?.trim() ?? '';

                                if (normalized.isEmpty) {
                                  return 'Informe o nome do exercício';
                                }

                                if (normalized.length >
                                    150) {
                                  return 'Máximo de 150 caracteres';
                                }

                                return null;
                              },
                            ),

                            const SizedBox(height: 16),

                            TextFormField(
                              controller:
                              _muscleGroupController,
                              enabled:
                              !viewModel.isBusy,
                              maxLength: 100,
                              textCapitalization:
                              TextCapitalization
                                  .sentences,
                              textInputAction:
                              TextInputAction.next,
                              decoration:
                              const InputDecoration(
                                labelText:
                                'Grupo muscular *',
                                prefixIcon: Icon(
                                  Icons
                                      .accessibility_new_rounded,
                                ),
                                border:
                                OutlineInputBorder(),
                              ),
                              validator: (value) {
                                final normalized =
                                    value?.trim() ?? '';

                                if (normalized.isEmpty) {
                                  return 'Informe o grupo muscular';
                                }

                                if (normalized.length >
                                    100) {
                                  return 'Máximo de 100 caracteres';
                                }

                                return null;
                              },
                            ),

                            const SizedBox(height: 16),

                            TextFormField(
                              controller:
                              _descriptionController,
                              enabled:
                              !viewModel.isBusy,
                              minLines: 4,
                              maxLines: 6,
                              maxLength: 500,
                              textCapitalization:
                              TextCapitalization
                                  .sentences,
                              decoration:
                              const InputDecoration(
                                labelText: 'Descrição',
                                alignLabelWithHint: true,
                                border:
                                OutlineInputBorder(),
                              ),
                              validator: (value) {
                                if ((value ?? '')
                                    .trim()
                                    .length >
                                    500) {
                                  return 'Máximo de 500 caracteres';
                                }

                                return null;
                              },
                            ),
                          ],
                        ),
                      ),

                      if (isAdmin) ...[
                        const SizedBox(height: 20),

                        _StatusSection(
                          isActive:
                          viewModel.isActive,
                          isUpdating:
                          viewModel.isUpdatingStatus,
                          onStatusSelected:
                          _requestStatusChange,
                        ),
                      ],

                      if (viewModel.errorMessage !=
                          null) ...[
                        const SizedBox(height: 20),

                        _ErrorMessage(
                          message:
                          viewModel.errorMessage!,
                        ),
                      ],

                      const SizedBox(height: 24),

                      SizedBox(
                        height: 52,
                        child: FilledButton.icon(
                          onPressed: viewModel.isBusy
                              ? null
                              : _save,
                          icon: viewModel.isSaving
                              ? const SizedBox(
                            width: 20,
                            height: 20,
                            child:
                            CircularProgressIndicator(
                              strokeWidth: 2,
                            ),
                          )
                              : const Icon(
                            Icons
                                .check_rounded,
                          ),
                          label: Text(
                            viewModel.isSaving
                                ? 'Salvando...'
                                : 'Salvar alterações',
                          ),
                        ),
                      ),

                      const SizedBox(height: 10),

                      OutlinedButton(
                        onPressed: viewModel.isBusy
                            ? null
                            : () => Navigator.of(
                          context,
                        ).pop(_didChange ? true : null,
                        ),
                        child:
                        const Text('Cancelar'),
                      ),

                      const SizedBox(height: 16),

                      _PreservationInfo(
                        isAdmin: isAdmin,
                      ),
                    ],
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _StatusSection extends StatelessWidget {
  const _StatusSection({
    required this.isActive,
    required this.isUpdating,
    required this.onStatusSelected,
  });

  final bool isActive;
  final bool isUpdating;
  final ValueChanged<bool> onStatusSelected;

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
        crossAxisAlignment:
        CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Icon(
                Icons.toggle_on_outlined,
                color: colorScheme.primary,
              ),
              const SizedBox(width: 10),
              Text(
                'Status',
                style:
                theme.textTheme.titleLarge
                    ?.copyWith(
                  fontWeight: FontWeight.w800,
                ),
              ),
            ],
          ),

          const SizedBox(height: 8),

          Text(
            'Controle a disponibilidade do exercício '
                'para novos usos.',
            style:
            theme.textTheme.bodySmall?.copyWith(
              color:
              colorScheme.onSurfaceVariant,
            ),
          ),

          const SizedBox(height: 16),

          Row(
            children: [
              Expanded(
                child: _StatusOption(
                  label: 'Ativo',
                  icon:
                  Icons.check_circle_outline,
                  selected: isActive,
                  enabled: !isUpdating,
                  onTap: () =>
                      onStatusSelected(true),
                ),
              ),

              const SizedBox(width: 12),

              Expanded(
                child: _StatusOption(
                  label: 'Inativo',
                  icon:
                  Icons.block_rounded,
                  selected: !isActive,
                  enabled: !isUpdating,
                  onTap: () =>
                      onStatusSelected(false),
                ),
              ),
            ],
          ),

          if (isUpdating) ...[
            const SizedBox(height: 14),
            const LinearProgressIndicator(),
          ],
        ],
      ),
    );
  }
}

class _StatusOption extends StatelessWidget {
  const _StatusOption({
    required this.label,
    required this.icon,
    required this.selected,
    required this.enabled,
    required this.onTap,
  });

  final String label;
  final IconData icon;
  final bool selected;
  final bool enabled;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final colorScheme =
        Theme.of(context).colorScheme;

    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: enabled ? onTap : null,
        borderRadius: BorderRadius.circular(12),
        child: AnimatedContainer(
          duration:
          const Duration(milliseconds: 150),
          padding:
          const EdgeInsets.symmetric(
            horizontal: 14,
            vertical: 15,
          ),
          decoration: BoxDecoration(
            color: selected
                ? colorScheme.primary.withAlpha(18)
                : Colors.transparent,
            borderRadius:
            BorderRadius.circular(12),
            border: Border.all(
              color: selected
                  ? colorScheme.primary
                  : colorScheme.outlineVariant,
              width: selected ? 2 : 1,
            ),
          ),
          child: Row(
            mainAxisAlignment:
            MainAxisAlignment.center,
            children: [
              Icon(
                icon,
                size: 20,
                color: selected
                    ? colorScheme.primary
                    : colorScheme
                    .onSurfaceVariant,
              ),
              const SizedBox(width: 8),
              Text(
                label,
                style: TextStyle(
                  fontWeight: selected
                      ? FontWeight.w800
                      : FontWeight.w600,
                  color: selected
                      ? colorScheme.primary
                      : null,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _PreservationInfo extends StatelessWidget {
  const _PreservationInfo({
    required this.isAdmin,
  });

  final bool isAdmin;

  @override
  Widget build(BuildContext context) {
    final colorScheme =
        Theme.of(context).colorScheme;

    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: colorScheme.primary.withAlpha(14),
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
          Expanded(
            child: Text(
              isAdmin
                  ? 'Para preservar treinos e históricos, '
                  'exercícios não são excluídos. '
                  'Quando necessário, marque o exercício '
                  'como inativo.'
                  : 'As alterações são aplicadas ao exercício '
                  'cadastrado no banco da academia.',
            ),
          ),
        ],
      ),
    );
  }
}

class _FormSection extends StatelessWidget {
  const _FormSection({
    required this.title,
    required this.icon,
    required this.child,
  });

  final String title;
  final IconData icon;
  final Widget child;

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
        crossAxisAlignment:
        CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Icon(
                icon,
                color: colorScheme.primary,
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Text(
                  title,
                  style: theme
                      .textTheme.titleLarge
                      ?.copyWith(
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 18),
          child,
        ],
      ),
    );
  }
}

class _ErrorMessage extends StatelessWidget {
  const _ErrorMessage({
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
        color: colorScheme.errorContainer,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Row(
        crossAxisAlignment:
        CrossAxisAlignment.start,
        children: [
          Icon(
            Icons.error_outline_rounded,
            color:
            colorScheme.onErrorContainer,
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              message,
              style: TextStyle(
                color:
                colorScheme.onErrorContainer,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
