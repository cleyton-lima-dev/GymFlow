import 'package:flutter/material.dart';
import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/features/exercises/data/exercises_service.dart';
import 'package:gymflow/features/exercises/models/exercise_summary.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_page_header.dart';
import 'package:gymflow/features/workout_templates/presentation/select_workout_template_exercise_view_model.dart';
import 'package:provider/provider.dart';

class SelectWorkoutTemplateExercisePage
    extends StatelessWidget {
  const SelectWorkoutTemplateExercisePage({
    super.key,
  });

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) =>
      SelectWorkoutTemplateExerciseViewModel(
        ExercisesService(
          context.read<ApiClient>(),
        ),
      )..loadInitial(),
      child: const _SelectWorkoutTemplateExerciseView(),
    );
  }
}

class _SelectWorkoutTemplateExerciseView
    extends StatefulWidget {
  const _SelectWorkoutTemplateExerciseView();

  @override
  State<_SelectWorkoutTemplateExerciseView>
  createState() =>
      _SelectWorkoutTemplateExerciseViewState();
}

class _SelectWorkoutTemplateExerciseViewState
    extends State<_SelectWorkoutTemplateExerciseView> {
  final _searchController = TextEditingController();

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final viewModel = context
        .watch<SelectWorkoutTemplateExerciseViewModel>();

    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Scaffold(
      body: SafeArea(
        child: RefreshIndicator(
          onRefresh: viewModel.refresh,
          child: CustomScrollView(
            physics:
            const AlwaysScrollableScrollPhysics(),
            keyboardDismissBehavior:
            ScrollViewKeyboardDismissBehavior.onDrag,
            slivers: [
              SliverToBoxAdapter(
                child: Padding(
                  padding: const EdgeInsets.fromLTRB(
                    20,
                    12,
                    20,
                    0,
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
                            'Selecionar exercício',
                            style: theme
                                .textTheme.headlineSmall
                                ?.copyWith(
                              fontWeight:
                              FontWeight.w800,
                            ),
                          ),

                          const SizedBox(height: 6),

                          Text(
                            'Escolha um exercício ativo do banco para adicionar ao dia.',
                            style: theme
                                .textTheme.bodyMedium
                                ?.copyWith(
                              color: colorScheme
                                  .onSurfaceVariant,
                              height: 1.4,
                            ),
                          ),

                          const SizedBox(height: 22),

                          TextField(
                            controller:
                            _searchController,
                            onChanged: (value) {
                              viewModel
                                  .setSearch(value);

                              setState(() {});
                            },
                            textInputAction:
                            TextInputAction.search,
                            decoration:
                            InputDecoration(
                              hintText:
                              'Buscar por exercício ou grupo muscular...',
                              prefixIcon:
                              const Icon(
                                Icons.search_rounded,
                              ),
                              suffixIcon:
                              _searchController
                                  .text.isEmpty
                                  ? null
                                  : IconButton(
                                tooltip:
                                'Limpar busca',
                                onPressed: () {
                                  _searchController
                                      .clear();

                                  viewModel
                                      .setSearch(
                                    '',
                                  );

                                  setState(
                                        () {},
                                  );
                                },
                                icon:
                                const Icon(
                                  Icons
                                      .close_rounded,
                                ),
                              ),
                            ),
                          ),

                          const SizedBox(height: 14),

                          Container(
                            padding:
                            const EdgeInsets.symmetric(
                              horizontal: 14,
                              vertical: 11,
                            ),
                            decoration: BoxDecoration(
                              color: colorScheme.primary
                                  .withAlpha(14),
                              borderRadius:
                              BorderRadius.circular(
                                12,
                              ),
                            ),
                            child: Row(
                              children: [
                                Icon(
                                  Icons
                                      .verified_outlined,
                                  size: 18,
                                  color:
                                  colorScheme.primary,
                                ),

                                const SizedBox(
                                  width: 9,
                                ),

                                Expanded(
                                  child: Text(
                                    'Apenas exercícios ativos podem ser selecionados.',
                                    style: theme
                                        .textTheme
                                        .bodySmall
                                        ?.copyWith(
                                      color: colorScheme
                                          .onSurfaceVariant,
                                    ),
                                  ),
                                ),
                              ],
                            ),
                          ),

                          const SizedBox(height: 18),
                        ],
                      ),
                    ),
                  ),
                ),
              ),

              if (viewModel.isLoading)
                const SliverFillRemaining(
                  hasScrollBody: false,
                  child: Center(
                    child:
                    CircularProgressIndicator(),
                  ),
                )
              else if (viewModel.errorMessage != null &&
                  viewModel.items.isEmpty)
                SliverFillRemaining(
                  hasScrollBody: false,
                  child: _ErrorState(
                    message:
                    viewModel.errorMessage!,
                    onRetry:
                    viewModel.loadInitial,
                  ),
                )
              else if (viewModel.items.isEmpty)
                  const SliverFillRemaining(
                    hasScrollBody: false,
                    child: _EmptyState(),
                  )
                else ...[
                    SliverPadding(
                      padding:
                      const EdgeInsets.symmetric(
                        horizontal: 20,
                      ),
                      sliver: SliverList.separated(
                        itemCount:
                        viewModel.items.length,
                        separatorBuilder: (_, _) =>
                        const SizedBox(
                          height: 10,
                        ),
                        itemBuilder:
                            (context, index) {
                          final exercise =
                          viewModel.items[index];

                          return Center(
                            child: ConstrainedBox(
                              constraints:
                              const BoxConstraints(
                                maxWidth: 680,
                              ),
                              child: _ExerciseCard(
                                exercise: exercise,
                                onTap: () =>
                                    Navigator.of(context)
                                        .pop(
                                      exercise,
                                    ),
                              ),
                            ),
                          );
                        },
                      ),
                    ),

                    if (viewModel.errorMessage != null)
                      SliverToBoxAdapter(
                        child: Padding(
                          padding:
                          const EdgeInsets.fromLTRB(
                            20,
                            16,
                            20,
                            0,
                          ),
                          child: Center(
                            child: ConstrainedBox(
                              constraints:
                              const BoxConstraints(
                                maxWidth: 680,
                              ),
                              child: _InlineError(
                                message: viewModel
                                    .errorMessage!,
                              ),
                            ),
                          ),
                        ),
                      ),

                    if (viewModel.hasNextPage ||
                        viewModel.isLoadingMore)
                      SliverToBoxAdapter(
                        child: Padding(
                          padding:
                          const EdgeInsets.fromLTRB(
                            20,
                            18,
                            20,
                            0,
                          ),
                          child: Center(
                            child: ConstrainedBox(
                              constraints:
                              const BoxConstraints(
                                maxWidth: 680,
                              ),
                              child: SizedBox(
                                height: 46,
                                child: OutlinedButton(
                                  onPressed:
                                  viewModel
                                      .isLoadingMore
                                      ? null
                                      : viewModel
                                      .loadMore,
                                  child: viewModel
                                      .isLoadingMore
                                      ? const SizedBox(
                                    width: 18,
                                    height: 18,
                                    child:
                                    CircularProgressIndicator(
                                      strokeWidth:
                                      2,
                                    ),
                                  )
                                      : const Text(
                                    'Carregar mais',
                                  ),
                                ),
                              ),
                            ),
                          ),
                        ),
                      ),

                    const SliverToBoxAdapter(
                      child: SizedBox(height: 32),
                    ),
                  ],
            ],
          ),
        ),
      ),
    );
  }
}

class _ExerciseCard extends StatelessWidget {
  const _ExerciseCard({
    required this.exercise,
    required this.onTap,
  });

  final ExerciseSummary exercise;
  final VoidCallback onTap;

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
              color: colorScheme.outlineVariant
                  .withAlpha(120),
            ),
          ),
          child: Row(
            children: [
              Container(
                width: 44,
                height: 44,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  color:
                  colorScheme.primary.withAlpha(
                    18,
                  ),
                ),
                child: Icon(
                  Icons.fitness_center_rounded,
                  color: colorScheme.primary,
                  size: 21,
                ),
              ),

              const SizedBox(width: 14),

              Expanded(
                child: Column(
                  crossAxisAlignment:
                  CrossAxisAlignment.start,
                  children: [
                    Text(
                      exercise.name,
                      style: theme
                          .textTheme.titleMedium
                          ?.copyWith(
                        fontWeight:
                        FontWeight.w800,
                      ),
                    ),

                    const SizedBox(height: 4),

                    Text(
                      exercise.muscleGroup,
                      style: theme
                          .textTheme.bodySmall
                          ?.copyWith(
                        color: colorScheme
                            .onSurfaceVariant,
                        fontWeight:
                        FontWeight.w600,
                      ),
                    ),

                    if (exercise.description !=
                        null &&
                        exercise.description!
                            .trim()
                            .isNotEmpty) ...[
                      const SizedBox(height: 5),

                      Text(
                        exercise.description!
                            .trim(),
                        maxLines: 2,
                        overflow:
                        TextOverflow.ellipsis,
                        style: theme
                            .textTheme.bodySmall
                            ?.copyWith(
                          color: colorScheme
                              .onSurfaceVariant,
                        ),
                      ),
                    ],
                  ],
                ),
              ),

              const SizedBox(width: 10),

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

class _EmptyState extends StatelessWidget {
  const _EmptyState();

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Padding(
      padding: const EdgeInsets.all(32),
      child: Column(
        mainAxisAlignment:
        MainAxisAlignment.center,
        children: [
          Container(
            width: 58,
            height: 58,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color:
              colorScheme.primary.withAlpha(18),
            ),
            child: Icon(
              Icons.search_off_rounded,
              color: colorScheme.primary,
              size: 28,
            ),
          ),

          const SizedBox(height: 16),

          Text(
            'Nenhum exercício encontrado',
            textAlign: TextAlign.center,
            style:
            theme.textTheme.titleMedium?.copyWith(
              fontWeight: FontWeight.w800,
            ),
          ),

          const SizedBox(height: 6),

          Text(
            'Tente buscar por outro nome ou grupo muscular.',
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

class _ErrorState extends StatelessWidget {
  const _ErrorState({
    required this.message,
    required this.onRetry,
  });

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Padding(
      padding: const EdgeInsets.all(32),
      child: Column(
        mainAxisAlignment:
        MainAxisAlignment.center,
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
            style:
            theme.textTheme.bodyMedium,
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
        children: [
          Icon(
            Icons.error_outline_rounded,
            color: colorScheme.error,
            size: 20,
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
