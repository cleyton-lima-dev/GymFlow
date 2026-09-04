import 'package:flutter/material.dart';
import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_bottom_navigation.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_page_header.dart';
import 'package:gymflow/features/workout_templates/data/workout_templates_service.dart';
import 'package:gymflow/features/workout_templates/models/workout_template_summary.dart';
import 'package:gymflow/features/workouts/presentation/select_workout_template_view_model.dart';
import 'package:provider/provider.dart';

class SelectWorkoutTemplatePage extends StatelessWidget {
  const SelectWorkoutTemplatePage({
    required this.studentId,
    required this.studentName,
    super.key,
  });

  final String studentId;
  final String studentName;

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) => SelectWorkoutTemplateViewModel(
        WorkoutTemplatesService(
          context.read<ApiClient>(),
        ),
      )..loadInitial(),
      child: _SelectWorkoutTemplateView(
        studentName: studentName,
      ),
    );
  }
}

class _SelectWorkoutTemplateView extends StatefulWidget {
  const _SelectWorkoutTemplateView({
    required this.studentName,
  });

  final String studentName;

  @override
  State<_SelectWorkoutTemplateView> createState() =>
      _SelectWorkoutTemplateViewState();
}

class _SelectWorkoutTemplateViewState
    extends State<_SelectWorkoutTemplateView> {
  final _searchController = TextEditingController();

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final viewModel =
    context.watch<SelectWorkoutTemplateViewModel>();

    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Scaffold(
      bottomNavigationBar:
      const ProfessorAdminBottomNavigation(
        currentItem: ProfessorAdminNavItem.students,
      ),
      body: SafeArea(
        bottom: false,
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
                            'Selecionar modelo',
                            style: theme
                                .textTheme.headlineSmall
                                ?.copyWith(
                              fontWeight:
                              FontWeight.w800,
                            ),
                          ),

                          const SizedBox(height: 6),

                          Text(
                            'Escolha um modelo ativo para criar o treino do aluno.',
                            style: theme
                                .textTheme.bodyMedium
                                ?.copyWith(
                              color: colorScheme
                                  .onSurfaceVariant,
                              height: 1.4,
                            ),
                          ),

                          const SizedBox(height: 20),

                          _StudentCard(
                            studentName:
                            widget.studentName,
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
                              'Buscar modelo de treino...',
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

                          Text(
                            _resultsLabel(
                              viewModel.totalCount,
                            ),
                            style: theme
                                .textTheme.bodyMedium
                                ?.copyWith(
                              color: colorScheme
                                  .onSurfaceVariant,
                              fontWeight:
                              FontWeight.w600,
                            ),
                          ),

                          const SizedBox(height: 14),
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
                        const SizedBox(height: 10),
                        itemBuilder:
                            (context, index) {
                          final template =
                          viewModel.items[index];

                          return Center(
                            child: ConstrainedBox(
                              constraints:
                              const BoxConstraints(
                                maxWidth: 680,
                              ),
                              child: _TemplateCard(
                                template: template,
                                isSubmitting:
                                viewModel.isSubmitting,
                                isThisSubmitting:
                                viewModel
                                    .submittingTemplateId ==
                                    template.id,
                                onSelect: () =>
                                    _selectTemplate(
                                      context,
                                      viewModel,
                                      template,
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
                                message:
                                viewModel.errorMessage!,
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
                                      .isLoadingMore ||
                                      viewModel
                                          .isSubmitting
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
                      child: SizedBox(height: 20),
                    ),

                    SliverToBoxAdapter(
                      child: Padding(
                        padding:
                        const EdgeInsets.symmetric(
                          horizontal: 20,
                        ),
                        child: Center(
                          child: ConstrainedBox(
                            constraints:
                            const BoxConstraints(
                              maxWidth: 680,
                            ),
                            child: Container(
                              padding:
                              const EdgeInsets.all(
                                14,
                              ),
                              decoration: BoxDecoration(
                                color: colorScheme.primary
                                    .withAlpha(14),
                                borderRadius:
                                BorderRadius.circular(
                                  14,
                                ),
                              ),
                              child: Row(
                                crossAxisAlignment:
                                CrossAxisAlignment.start,
                                children: [
                                  Icon(
                                    Icons
                                        .info_outline_rounded,
                                    size: 20,
                                    color:
                                    colorScheme.primary,
                                  ),
                                  const SizedBox(
                                    width: 10,
                                  ),
                                  Expanded(
                                    child: Text(
                                      'Ao selecionar, o Avelri criará um treino para este aluno usando a estrutura do modelo.',
                                      style: theme
                                          .textTheme
                                          .bodySmall
                                          ?.copyWith(
                                        height: 1.4,
                                      ),
                                    ),
                                  ),
                                ],
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

  Future<void> _selectTemplate(
      BuildContext context,
      SelectWorkoutTemplateViewModel viewModel,
      WorkoutTemplateSummary template,
      ) async {
    final selectedTemplate =
    await viewModel.selectTemplate(template);

    if (!context.mounted ||
        selectedTemplate == null) {
      return;
    }

    Navigator.of(context).pop(
      selectedTemplate,
    );
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

class _TemplateCard extends StatelessWidget {
  const _TemplateCard({
    required this.template,
    required this.isSubmitting,
    required this.isThisSubmitting,
    required this.onSelect,
  });

  final WorkoutTemplateSummary template;
  final bool isSubmitting;
  final bool isThisSubmitting;
  final VoidCallback onSelect;

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
        crossAxisAlignment:
        CrossAxisAlignment.center,
        children: [
          Container(
            width: 44,
            height: 44,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color:
              colorScheme.primary.withAlpha(18),
            ),
            child: Icon(
              Icons.content_copy_rounded,
              color: colorScheme.primary,
              size: 20,
            ),
          ),

          const SizedBox(width: 14),

          Expanded(
            child: Column(
              crossAxisAlignment:
              CrossAxisAlignment.start,
              children: [
                Text(
                  template.name,
                  style:
                  theme.textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.w800,
                  ),
                ),

                if (template.description != null &&
                    template.description!
                        .trim()
                        .isNotEmpty) ...[
                  const SizedBox(height: 5),

                  Text(
                    template.description!.trim(),
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style:
                    theme.textTheme.bodySmall?.copyWith(
                      color:
                      colorScheme.onSurfaceVariant,
                      height: 1.35,
                    ),
                  ),
                ],
              ],
            ),
          ),

          const SizedBox(width: 12),

          FilledButton(
            onPressed:
            isSubmitting ? null : onSelect,
            child: isThisSubmitting
                ? const SizedBox(
              width: 18,
              height: 18,
              child:
              CircularProgressIndicator(
                strokeWidth: 2,
              ),
            )
                : const Text(
              'Selecionar',
            ),
          ),
        ],
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
          Icon(
            Icons.search_off_rounded,
            size: 44,
            color: colorScheme.primary,
          ),

          const SizedBox(height: 14),

          Text(
            'Nenhum modelo encontrado',
            textAlign: TextAlign.center,
            style:
            theme.textTheme.titleMedium?.copyWith(
              fontWeight: FontWeight.w800,
            ),
          ),

          const SizedBox(height: 6),

          Text(
            'Tente buscar por outro nome.',
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
    final colorScheme =
        Theme.of(context).colorScheme;

    return Padding(
      padding: const EdgeInsets.all(32),
      child: Column(
        mainAxisAlignment:
        MainAxisAlignment.center,
        children: [
          Icon(
            Icons.error_outline_rounded,
            size: 42,
            color: colorScheme.error,
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

String _resultsLabel(int count) {
  return count == 1
      ? '1 modelo encontrado'
      : '$count modelos encontrados';
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
