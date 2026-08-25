import 'package:flutter/material.dart';
import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/features/exercises/data/exercises_service.dart';
import 'package:gymflow/features/exercises/presentation/create_exercise_view_model.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_bottom_navigation.dart';
import 'package:provider/provider.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_page_header.dart';

class CreateExercisePage extends StatelessWidget {
  const CreateExercisePage({
    super.key,
  });

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) => CreateExerciseViewModel(
        ExercisesService(
          context.read<ApiClient>(),
        ),
      ),
      child: const _CreateExerciseView(),
    );
  }
}

class _CreateExerciseView extends StatefulWidget {
  const _CreateExerciseView();

  @override
  State<_CreateExerciseView> createState() =>
      _CreateExerciseViewState();
}

class _CreateExerciseViewState
    extends State<_CreateExerciseView> {
  final _formKey = GlobalKey<FormState>();

  final _nameController = TextEditingController();
  final _muscleGroupController = TextEditingController();
  final _descriptionController = TextEditingController();

  @override
  void dispose() {
    _nameController.dispose();
    _muscleGroupController.dispose();
    _descriptionController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    FocusScope.of(context).unfocus();

    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    final viewModel = context.read<CreateExerciseViewModel>();

    final success = await viewModel.submit(
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
          'Exercício cadastrado com sucesso.',
        ),
      ),
    );

    Navigator.of(context).pop(true);
  }

  @override
  Widget build(BuildContext context) {
    final viewModel =
    context.watch<CreateExerciseViewModel>();

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
                        'Novo exercício',
                        style:
                        theme.textTheme.headlineSmall
                            ?.copyWith(
                          fontWeight: FontWeight.w800,
                        ),
                      ),

                      const SizedBox(height: 4),

                      Text(
                        'Cadastre um novo exercício no banco '
                            'da sua academia.',
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
                              !viewModel.isSubmitting,
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
                                hintText:
                                'Ex.: Supino reto com barra',
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
                              !viewModel.isSubmitting,
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
                                hintText:
                                'Ex.: Peito',
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
                              !viewModel.isSubmitting,
                              minLines: 4,
                              maxLines: 6,
                              maxLength: 500,
                              textCapitalization:
                              TextCapitalization
                                  .sentences,
                              decoration:
                              const InputDecoration(
                                labelText: 'Descrição',
                                hintText:
                                'Descreva o exercício, foco, '
                                    'técnica e dicas importantes...',
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

                      const SizedBox(height: 20),

                      Container(
                        padding:
                        const EdgeInsets.all(16),
                        decoration: BoxDecoration(
                          color: colorScheme.primary
                              .withAlpha(16),
                          borderRadius:
                          BorderRadius.circular(14),
                        ),
                        child: Row(
                          crossAxisAlignment:
                          CrossAxisAlignment.start,
                          children: [
                            Icon(
                              Icons
                                  .info_outline_rounded,
                              color:
                              colorScheme.primary,
                            ),
                            const SizedBox(width: 12),
                            Expanded(
                              child: Column(
                                crossAxisAlignment:
                                CrossAxisAlignment
                                    .start,
                                children: [
                                  Text(
                                    'Sobre o cadastro',
                                    style: theme
                                        .textTheme
                                        .titleSmall
                                        ?.copyWith(
                                      fontWeight:
                                      FontWeight
                                          .w800,
                                    ),
                                  ),
                                  const SizedBox(
                                    height: 5,
                                  ),
                                  const Text(
                                    'O exercício será cadastrado '
                                        'como ativo e ficará disponível '
                                        'para uso em modelos e treinos.',
                                  ),
                                ],
                              ),
                            ),
                          ],
                        ),
                      ),

                      const SizedBox(height: 16),

                      Container(
                        padding:
                        const EdgeInsets.all(16),
                        decoration: BoxDecoration(
                          color: colorScheme.surface,
                          borderRadius:
                          BorderRadius.circular(14),
                          border: Border.all(
                            color: colorScheme
                                .outlineVariant
                                .withAlpha(120),
                          ),
                        ),
                        child: Row(
                          crossAxisAlignment:
                          CrossAxisAlignment.start,
                          children: [
                            Icon(
                              Icons.lightbulb_outline,
                              color:
                              colorScheme.primary,
                            ),
                            const SizedBox(width: 12),
                            const Expanded(
                              child: Column(
                                crossAxisAlignment:
                                CrossAxisAlignment
                                    .start,
                                children: [
                                  Text(
                                    'Dica',
                                    style: TextStyle(
                                      fontWeight:
                                      FontWeight
                                          .w800,
                                    ),
                                  ),
                                  SizedBox(height: 5),
                                  Text(
                                    'Mantenha os nomes padronizados '
                                        'e use descrições claras para '
                                        'facilitar a busca e o uso pelos '
                                        'professores.',
                                  ),
                                ],
                              ),
                            ),
                          ],
                        ),
                      ),

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
                          onPressed:
                          viewModel.isSubmitting
                              ? null
                              : _submit,
                          icon: viewModel.isSubmitting
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
                            viewModel.isSubmitting
                                ? 'Salvando...'
                                : 'Salvar exercício',
                          ),
                        ),
                      ),

                      const SizedBox(height: 10),

                      OutlinedButton(
                        onPressed:
                        viewModel.isSubmitting
                            ? null
                            : () =>
                            Navigator.of(
                              context,
                            ).pop(),
                        child:
                        const Text('Cancelar'),
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
                  style: theme.textTheme.titleLarge
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
            color: colorScheme.onErrorContainer,
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
