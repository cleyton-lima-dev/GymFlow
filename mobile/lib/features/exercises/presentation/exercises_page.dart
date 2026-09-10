import 'package:flutter/material.dart';
import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/features/exercises/data/exercises_service.dart';
import 'package:gymflow/features/exercises/models/exercise_summary.dart';
import 'package:gymflow/features/exercises/presentation/exercises_view_model.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_bottom_navigation.dart';
import 'package:provider/provider.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_page_header.dart';

class ExercisesPage extends StatelessWidget {
  const ExercisesPage({
    this.onNewExerciseTap,
    this.onExerciseTap,
    super.key,
  });

  final Future<bool?> Function()? onNewExerciseTap;
  final Future<bool?> Function(ExerciseSummary)? onExerciseTap;

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) => ExercisesViewModel(
        ExercisesService(
          context.read<ApiClient>(),
        ),
      )..loadInitial(),
      child: _ExercisesView(
        onNewExerciseTap: onNewExerciseTap,
        onExerciseTap: onExerciseTap,
      ),
    );
  }
}

class _ExercisesView extends StatefulWidget {
  const _ExercisesView({
    required this.onNewExerciseTap,
    required this.onExerciseTap,
  });

  final Future<bool?> Function()? onNewExerciseTap;
  final Future<bool?> Function(ExerciseSummary)? onExerciseTap;

  @override
  State<_ExercisesView> createState() =>
      _ExercisesViewState();
}

class _ExercisesViewState extends State<_ExercisesView> {
  final _searchController = TextEditingController();

  Future<void> _openNewExercise(
      ExercisesViewModel viewModel,
      ) async {
    final callback = widget.onNewExerciseTap;

    if (callback == null) {
      return;
    }

    final created = await callback();

    if (!mounted || created != true) {
      return;
    }

    await viewModel.loadInitial();
  }

  Future<void> _openExercise(
      ExercisesViewModel viewModel,
      ExerciseSummary exercise,
      ) async {
    final callback = widget.onExerciseTap;

    if (callback == null) {
      return;
    }

    final changed = await callback(exercise);

    if (!mounted || changed != true) {
      return;
    }

    await viewModel.refresh();
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  Future<void> _openMuscleGroupFilter(
      ExercisesViewModel viewModel,
      ) async {
    var muscleGroupValue = viewModel.muscleGroup ?? '';

    final result = await showModalBottomSheet<String>(
      context: context,
      isScrollControlled: true,
      builder: (context) {
        return SafeArea(
          child: Padding(
            padding: EdgeInsets.fromLTRB(
              20,
              20,
              20,
              20 + MediaQuery.viewInsetsOf(context).bottom,
            ),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment:
              CrossAxisAlignment.stretch,
              children: [
                Text(
                  'Filtrar por grupo muscular',
                  style: Theme.of(context)
                      .textTheme
                      .titleLarge
                      ?.copyWith(
                    fontWeight: FontWeight.w800,
                  ),
                ),

                const SizedBox(height: 8),

                Text(
                  'Digite o grupo muscular exatamente como '
                      'ele está cadastrado.',
                  style: Theme.of(context)
                      .textTheme
                      .bodyMedium
                      ?.copyWith(
                    color: Theme.of(context)
                        .colorScheme
                        .onSurfaceVariant,
                  ),
                ),

                const SizedBox(height: 18),

                TextFormField(
                  initialValue: muscleGroupValue,
                  autofocus: true,
                  textCapitalization: TextCapitalization.sentences,
                  decoration: const InputDecoration(
                    labelText: 'Grupo muscular',
                    hintText: 'Ex.: Peito',
                    prefixIcon: Icon(Icons.fitness_center_rounded),
                    border: OutlineInputBorder(),
                  ),
                  onChanged: (value) {
                    muscleGroupValue = value;
                  },
                  onFieldSubmitted: (value) {
                    Navigator.of(context).pop(
                      value.trim(),
                    );
                  },
                ),

                const SizedBox(height: 18),

                Row(
                  children: [
                    Expanded(
                      child: OutlinedButton(
                        onPressed: () {
                          Navigator.of(context).pop('');
                        },
                        child: const Text('Limpar'),
                      ),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: FilledButton(
                        onPressed: () {
                          Navigator.of(context).pop(
                            muscleGroupValue.trim(),
                          );
                        },
                        child: const Text('Aplicar'),
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
        );
      },
    );


    if (!mounted || result == null) {
      return;
    }

    await viewModel.setMuscleGroup(
      result.isEmpty ? null : result,
    );
  }

  @override
  Widget build(BuildContext context) {
    final viewModel = context.watch<ExercisesViewModel>();

    return Scaffold(
      bottomNavigationBar:
      const ProfessorAdminBottomNavigation(
        currentItem: ProfessorAdminNavItem.more,
      ),
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
                  child: Column(
                    crossAxisAlignment:
                    CrossAxisAlignment.stretch,
                    children: [
                      const ProfessorAdminPageHeader(),
                      const SizedBox(height: 18),

                      Text(
                        'Banco de exercícios',
                        style: Theme.of(context)
                            .textTheme
                            .headlineSmall
                            ?.copyWith(
                          fontWeight: FontWeight.w800,
                        ),
                      ),

                      const SizedBox(height: 4),

                      Text(
                        'Gerencie os exercícios disponíveis '
                            'da sua academia.',
                        style: Theme.of(context)
                            .textTheme
                            .bodyMedium
                            ?.copyWith(
                          color: Theme.of(context)
                              .colorScheme
                              .onSurfaceVariant,
                        ),
                      ),

                      const SizedBox(height: 20),

                      TextField(
                        controller: _searchController,
                        onChanged: viewModel.setSearch,
                        textInputAction:
                        TextInputAction.search,
                        decoration: InputDecoration(
                          hintText:
                          'Buscar exercício ou grupo muscular...',
                          prefixIcon: const Icon(
                            Icons.search_rounded,
                          ),
                          suffixIcon:
                          _searchController.text.isEmpty
                              ? null
                              : IconButton(
                            onPressed: () {
                              _searchController
                                  .clear();

                              viewModel.setSearch('');
                              setState(() {});
                            },
                            icon: const Icon(
                              Icons.close_rounded,
                            ),
                          ),
                          border:
                          const OutlineInputBorder(),
                        ),
                      ),

                      const SizedBox(height: 14),

                      Row(
                        children: [
                          Expanded(
                            child: OutlinedButton.icon(
                              onPressed: () =>
                                  _openMuscleGroupFilter(
                                    viewModel,
                                  ),
                              icon: const Icon(
                                Icons.filter_alt_outlined,
                              ),
                              label: Text(
                                viewModel.muscleGroup ??
                                    'Grupo muscular',
                                maxLines: 1,
                                overflow:
                                TextOverflow.ellipsis,
                              ),
                            ),
                          ),

                          const SizedBox(width: 10),

                          Expanded(
                            child: PopupMenuButton<
                                ExerciseStatusFilter>(
                              initialValue:
                              viewModel.statusFilter,
                              onSelected:
                              viewModel.setStatusFilter,
                              itemBuilder: (context) => const [
                                PopupMenuItem(
                                  value:
                                  ExerciseStatusFilter.all,
                                  child: Text('Todos'),
                                ),
                                PopupMenuItem(
                                  value:
                                  ExerciseStatusFilter.active,
                                  child: Text('Ativos'),
                                ),
                                PopupMenuItem(
                                  value:
                                  ExerciseStatusFilter.inactive,
                                  child: Text('Inativos'),
                                ),
                              ],
                              child: _FilterButton(
                                icon: Icons.toggle_on_outlined,
                                label: _statusLabel(
                                  viewModel.statusFilter,
                                ),
                              ),
                            ),
                          ),
                        ],
                      ),

                      const SizedBox(height: 14),

                      SizedBox(
                        height: 48,
                        child: FilledButton.icon(
                          onPressed: widget.onNewExerciseTap == null
                              ? null
                              : () => _openNewExercise(viewModel),
                          icon: const Icon(
                            Icons.add_rounded,
                          ),
                          label:
                          const Text('Novo exercício'),
                        ),
                      ),

                      const SizedBox(height: 18),

                      _ResultsHeader(
                        totalCount: viewModel.totalCount,
                      ),

                      const SizedBox(height: 14),
                    ],
                  ),
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
                    onRetry: viewModel.loadInitial,
                  ),
                )
              else if (viewModel.items.isEmpty)
                  SliverFillRemaining(
                    hasScrollBody: false,
                    child: _EmptyState(
                      hasSearch: viewModel.search.trim().isNotEmpty,
                      hasFilter:
                      viewModel.muscleGroup != null ||
                          viewModel.statusFilter != ExerciseStatusFilter.all,
                      onCreate: widget.onNewExerciseTap == null
                          ? null
                          : () => _openNewExercise(viewModel),
                    ),
                  )
                else ...[
                    SliverPadding(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 20,
                      ),
                      sliver: SliverList.separated(
                        itemCount: viewModel.items.length,
                        separatorBuilder: (_, _) =>
                        const SizedBox(height: 10),
                        itemBuilder: (context, index) {
                          final exercise =
                          viewModel.items[index];

                          return _ExerciseCard(
                            exercise: exercise,
                            onTap: widget.onExerciseTap == null
                                ? null
                                : () => _openExercise(
                              viewModel,
                              exercise,
                            ),
                          );
                        },
                      ),
                    ),

                    const SliverToBoxAdapter(
                      child: SizedBox(height: 20),
                    ),

                    SliverToBoxAdapter(
                      child: Padding(
                        padding: const EdgeInsets.symmetric(
                          horizontal: 20,
                        ),
                        child: _Pagination(
                          page: viewModel.page,
                          totalPages: viewModel.totalPages,
                          hasPreviousPage:
                          viewModel.hasPreviousPage,
                          hasNextPage:
                          viewModel.hasNextPage,
                          isChangingPage:
                          viewModel.isChangingPage,
                          onPrevious:
                          viewModel.previousPage,
                          onNext: viewModel.nextPage,
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

  String _statusLabel(
      ExerciseStatusFilter filter,
      ) {
    return switch (filter) {
      ExerciseStatusFilter.all => 'Status',
      ExerciseStatusFilter.active => 'Ativos',
      ExerciseStatusFilter.inactive => 'Inativos',
    };
  }
}

class _FilterButton extends StatelessWidget {
  const _FilterButton({
    required this.icon,
    required this.label,
  });

  final IconData icon;
  final String label;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      height: 48,
      padding: const EdgeInsets.symmetric(
        horizontal: 14,
      ),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(12),
        border: Border.all(
          color: colorScheme.outlineVariant,
        ),
      ),
      child: Row(
        children: [
          Icon(
            icon,
            color: colorScheme.primary,
          ),
          const SizedBox(width: 8),
          Expanded(
            child: Text(
              label,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
            ),
          ),
          const Icon(
            Icons.keyboard_arrow_down_rounded,
          ),
        ],
      ),
    );
  }
}

class _ResultsHeader extends StatelessWidget {
  const _ResultsHeader({
    required this.totalCount,
  });

  final int totalCount;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Row(
      children: [
        Expanded(
          child: Text(
            totalCount == 1
                ? '1 exercício encontrado'
                : '$totalCount exercícios encontrados',
            style: Theme.of(context)
                .textTheme
                .bodyMedium
                ?.copyWith(
              color:
              colorScheme.onSurfaceVariant,
              fontWeight: FontWeight.w600,
            ),
          ),
        ),
      ],
    );
  }
}

class _ExerciseCard extends StatelessWidget {
  const _ExerciseCard({
    required this.exercise,
    required this.onTap,
  });

  final ExerciseSummary exercise;
  final VoidCallback? onTap;

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
                crossAxisAlignment:
                CrossAxisAlignment.start,
                children: [
                  Container(
                    width: 42,
                    height: 42,
                    decoration: BoxDecoration(
                      shape: BoxShape.circle,
                      color:
                      colorScheme.primary.withAlpha(18),
                    ),
                    child: Icon(
                      Icons.fitness_center_rounded,
                      color: colorScheme.primary,
                      size: 21,
                    ),
                  ),

                  const SizedBox(width: 12),

                  Expanded(
                    child: Column(
                      crossAxisAlignment:
                      CrossAxisAlignment.start,
                      children: [
                        Text(
                          exercise.name,
                          style: theme.textTheme.titleMedium
                              ?.copyWith(
                            fontWeight: FontWeight.w800,
                          ),
                        ),

                        const SizedBox(height: 4),

                        Text(
                          exercise.muscleGroup,
                          style: theme.textTheme.bodySmall
                              ?.copyWith(
                            color: colorScheme.primary,
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                      ],
                    ),
                  ),

                  const SizedBox(width: 8),

                  _StatusBadge(
                    isActive: exercise.isActive,
                  ),

                  const SizedBox(width: 2),

                  Icon(
                    Icons.chevron_right_rounded,
                    color:
                    colorScheme.onSurfaceVariant,
                  ),
                ],
              ),

              if (exercise.description != null &&
                  exercise.description!
                      .trim()
                      .isNotEmpty) ...[
                const SizedBox(height: 12),
                Text(
                  exercise.description!.trim(),
                  maxLines: 3,
                  overflow: TextOverflow.ellipsis,
                  style: theme.textTheme.bodyMedium
                      ?.copyWith(
                    color:
                    colorScheme.onSurfaceVariant,
                    height: 1.4,
                  ),
                ),
              ],
            ],
          ),
        ),
      ),
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
        : Theme.of(context).colorScheme.onSurfaceVariant;

    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: 9,
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
          fontSize: 11,
          fontWeight: FontWeight.w800,
        ),
      ),
    );
  }
}

class _Pagination extends StatelessWidget {
  const _Pagination({
    required this.page,
    required this.totalPages,
    required this.hasPreviousPage,
    required this.hasNextPage,
    required this.isChangingPage,
    required this.onPrevious,
    required this.onNext,
  });

  final int page;
  final int totalPages;
  final bool hasPreviousPage;
  final bool hasNextPage;
  final bool isChangingPage;

  final VoidCallback onPrevious;
  final VoidCallback onNext;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Expanded(
          child: OutlinedButton(
            onPressed:
            hasPreviousPage && !isChangingPage
                ? onPrevious
                : null,
            child: const Text('Anterior'),
          ),
        ),
        const SizedBox(width: 12),
        Text(
          '$page de $totalPages',
          style: const TextStyle(
            fontWeight: FontWeight.w700,
          ),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: OutlinedButton(
            onPressed:
            hasNextPage && !isChangingPage
                ? onNext
                : null,
            child: isChangingPage
                ? const SizedBox(
              width: 18,
              height: 18,
              child: CircularProgressIndicator(
                strokeWidth: 2,
              ),
            )
                : const Text('Próxima'),
          ),
        ),
      ],
    );
  }
}

class _EmptyState extends StatelessWidget {
  const _EmptyState({
    required this.hasSearch,
    required this.hasFilter,
    this.onCreate,
  });

  final bool hasSearch;
  final bool hasFilter;
  final VoidCallback? onCreate;
  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;
    final filtered = hasSearch || hasFilter;

    return Padding(
      padding: const EdgeInsets.all(32),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Icon(
            Icons.fitness_center_rounded,
            size: 48,
            color: colorScheme.primary,
          ),
          const SizedBox(height: 16),
          Text(
            filtered
                ? 'Nenhum exercício encontrado'
                : 'Nenhum exercício cadastrado',
            textAlign: TextAlign.center,
            style: theme.textTheme.titleLarge?.copyWith(
              fontWeight: FontWeight.w800,
            ),
          ),

          const SizedBox(height: 8),

          Text(
            filtered
                ? 'Tente alterar a busca ou os filtros aplicados.'
                : 'Cadastre o primeiro exercício do banco desta academia.',
            textAlign: TextAlign.center,
            style: theme.textTheme.bodyMedium?.copyWith(
              color: colorScheme.onSurfaceVariant,
            ),
          ),

          if (!filtered && onCreate != null) ...[
            const SizedBox(height: 18),

            FilledButton.icon(
              onPressed: onCreate,
              icon: const Icon(Icons.add_rounded),
              label: const Text('Cadastrar exercício'),
            ),
          ],
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
    final colorScheme = Theme.of(context).colorScheme;

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
            child: const Text('Tentar novamente'),
          ),
        ],
      ),
    );
  }
}
