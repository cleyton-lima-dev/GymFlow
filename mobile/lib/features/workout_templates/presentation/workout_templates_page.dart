import 'package:flutter/material.dart';
import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_bottom_navigation.dart';
import 'package:gymflow/features/workout_templates/data/workout_templates_service.dart';
import 'package:gymflow/features/workout_templates/models/workout_template_summary.dart';
import 'package:gymflow/features/workout_templates/presentation/workout_templates_view_model.dart';
import 'package:provider/provider.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_page_header.dart';

class WorkoutTemplatesPage extends StatelessWidget {
  const WorkoutTemplatesPage({
    this.onNewTemplateTap,
    this.onTemplateTap,
    super.key,
  });

  final Future<bool> Function()? onNewTemplateTap;
  final Future<void> Function(
      WorkoutTemplateSummary template,
      )? onTemplateTap;

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) => WorkoutTemplatesViewModel(
        WorkoutTemplatesService(
          context.read<ApiClient>(),
        ),
      )..loadInitial(),
      child: _WorkoutTemplatesView(
        onNewTemplateTap: onNewTemplateTap,
        onTemplateTap: onTemplateTap,
      ),
    );
  }
}

class _WorkoutTemplatesView extends StatefulWidget {
  const _WorkoutTemplatesView({
    required this.onNewTemplateTap,
    required this.onTemplateTap,
  });

  final Future<bool> Function()? onNewTemplateTap;
  final Future<void> Function(
      WorkoutTemplateSummary template,
      )? onTemplateTap;

  @override
  State<_WorkoutTemplatesView> createState() =>
      _WorkoutTemplatesViewState();
}

class _WorkoutTemplatesViewState
    extends State<_WorkoutTemplatesView> {
  final _searchController = TextEditingController();

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final viewModel =
    context.watch<WorkoutTemplatesViewModel>();

    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

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
                        'Modelos de treino',
                        style:
                        theme.textTheme.headlineSmall
                            ?.copyWith(
                          fontWeight: FontWeight.w800,
                        ),
                      ),

                      const SizedBox(height: 4),

                      Text(
                        'Crie e gerencie modelos reutilizáveis '
                            'para montar treinos com mais rapidez.',
                        style:
                        theme.textTheme.bodyMedium
                            ?.copyWith(
                          color:
                          colorScheme.onSurfaceVariant,
                        ),
                      ),

                      const SizedBox(height: 20),

                      TextField(
                        controller: _searchController,
                        onChanged: (value) {
                          viewModel.setSearch(value);
                          setState(() {});
                        },
                        textInputAction:
                        TextInputAction.search,
                        decoration: InputDecoration(
                          hintText:
                          'Buscar modelo de treino...',
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

                      PopupMenuButton<
                          WorkoutTemplateStatusFilter>(
                        initialValue:
                        viewModel.statusFilter,
                        onSelected:
                        viewModel.setStatusFilter,
                        itemBuilder: (context) => const [
                          PopupMenuItem(
                            value:
                            WorkoutTemplateStatusFilter
                                .all,
                            child: Text('Todos'),
                          ),
                          PopupMenuItem(
                            value:
                            WorkoutTemplateStatusFilter
                                .active,
                            child: Text('Ativos'),
                          ),
                          PopupMenuItem(
                            value:
                            WorkoutTemplateStatusFilter
                                .inactive,
                            child: Text('Inativos'),
                          ),
                        ],
                        child: _FilterButton(
                          label: _statusLabel(
                            viewModel.statusFilter,
                          ),
                        ),
                      ),

                      const SizedBox(height: 14),

                      SizedBox(
                        height: 48,
                        child: FilledButton.icon(
                          onPressed: widget.onNewTemplateTap == null
                              ? null
                              : () async {
                            final created =
                            await widget.onNewTemplateTap!();

                            if (!context.mounted || !created) {
                              return;
                            }

                            await viewModel.refresh();
                          },
                          icon: const Icon(
                            Icons.add_rounded,
                          ),
                          label: const Text(
                            'Novo modelo',
                          ),
                        ),
                      ),

                      const SizedBox(height: 18),

                      _ResultsHeader(
                        totalCount: viewModel.totalCount,
                      ),

                      const SizedBox(height: 14),

                      const SizedBox(height: 18),
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

                          return _TemplateCard(
                            template: template,
                            onTap: widget.onTemplateTap == null
                                ? null
                                : () async {
                              await widget.onTemplateTap!(
                                template,
                              );

                              if (!context.mounted) {
                                return;
                              }

                              await viewModel.refresh();
                            },
                          );
                        },
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
                        child: _Pagination(
                          page: viewModel.page,
                          totalPages:
                          viewModel.totalPages,
                          hasPreviousPage:
                          viewModel
                              .hasPreviousPage,
                          hasNextPage:
                          viewModel.hasNextPage,
                          isChangingPage:
                          viewModel
                              .isChangingPage,
                          onPrevious:
                          viewModel.previousPage,
                          onNext:
                          viewModel.nextPage,
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
      WorkoutTemplateStatusFilter filter,
      ) {
    return switch (filter) {
      WorkoutTemplateStatusFilter.all =>
      'Todos os status',
      WorkoutTemplateStatusFilter.active =>
      'Ativos',
      WorkoutTemplateStatusFilter.inactive =>
      'Inativos',
    };
  }
}

class _FilterButton extends StatelessWidget {
  const _FilterButton({
    required this.label,
  });

  final String label;

  @override
  Widget build(BuildContext context) {
    final colorScheme =
        Theme.of(context).colorScheme;

    return Container(
      height: 48,
      padding:
      const EdgeInsets.symmetric(
        horizontal: 14,
      ),
      decoration: BoxDecoration(
        borderRadius:
        BorderRadius.circular(12),
        border: Border.all(
          color:
          colorScheme.outlineVariant,
        ),
      ),
      child: Row(
        children: [
          Icon(
            Icons
                .filter_alt_outlined,
            color: colorScheme.primary,
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              label,
              maxLines: 1,
              overflow:
              TextOverflow.ellipsis,
            ),
          ),
          const Icon(
            Icons
                .keyboard_arrow_down_rounded,
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
    final colorScheme =
        Theme.of(context).colorScheme;

    return Text(
      totalCount == 1
          ? '1 modelo encontrado'
          : '$totalCount modelos encontrados',
      style: Theme.of(context)
          .textTheme
          .bodyMedium
          ?.copyWith(
        color:
        colorScheme.onSurfaceVariant,
        fontWeight: FontWeight.w600,
      ),
    );
  }
}

class _TemplateCard extends StatelessWidget {
  const _TemplateCard({
    required this.template,
    required this.onTap,
  });

  final WorkoutTemplateSummary template;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme =
        theme.colorScheme;

    return Material(
      color: colorScheme.surface,
      borderRadius:
      BorderRadius.circular(16),
      child: InkWell(
        onTap: onTap,
        borderRadius:
        BorderRadius.circular(16),
        child: Container(
          padding:
          const EdgeInsets.all(16),
          decoration: BoxDecoration(
            borderRadius:
            BorderRadius.circular(16),
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
              Container(
                width: 44,
                height: 44,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  color: colorScheme.primary
                      .withAlpha(18),
                ),
                child: Icon(
                  Icons
                      .description_outlined,
                  color:
                  colorScheme.primary,
                ),
              ),

              const SizedBox(width: 12),

              Expanded(
                child: Column(
                  crossAxisAlignment:
                  CrossAxisAlignment.start,
                  children: [
                    Row(
                      crossAxisAlignment:
                      CrossAxisAlignment.start,
                      children: [
                        Expanded(
                          child: Text(
                            template.name,
                            style: theme
                                .textTheme
                                .titleMedium
                                ?.copyWith(
                              fontWeight:
                              FontWeight
                                  .w800,
                            ),
                          ),
                        ),

                        const SizedBox(
                          width: 8,
                        ),

                        _StatusBadge(
                          isActive:
                          template
                              .isActive,
                        ),
                      ],
                    ),

                    if (template.description !=
                        null &&
                        template.description!
                            .trim()
                            .isNotEmpty) ...[
                      const SizedBox(
                        height: 7,
                      ),
                      Text(
                        template.description!
                            .trim(),
                        maxLines: 3,
                        overflow:
                        TextOverflow
                            .ellipsis,
                        style: theme
                            .textTheme
                            .bodyMedium
                            ?.copyWith(
                          color: colorScheme
                              .onSurfaceVariant,
                          height: 1.4,
                        ),
                      ),
                    ],

                    const SizedBox(height: 10),

                    Row(
                      children: [
                        Icon(
                          Icons
                              .calendar_today_outlined,
                          size: 16,
                          color: colorScheme
                              .onSurfaceVariant,
                        ),
                        const SizedBox(
                          width: 6,
                        ),
                        Text(
                          'Criado em ${_formatDate(template.createdAt)}',
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
                  ],
                ),
              ),

              const SizedBox(width: 6),

              Icon(
                Icons
                    .chevron_right_rounded,
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
      padding:
      const EdgeInsets.symmetric(
        horizontal: 9,
        vertical: 5,
      ),
      decoration: BoxDecoration(
        color: color.withAlpha(18),
        borderRadius:
        BorderRadius.circular(999),
      ),
      child: Text(
        isActive
            ? 'Ativo'
            : 'Inativo',
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
            hasPreviousPage &&
                !isChangingPage
                ? onPrevious
                : null,
            child:
            const Text('Anterior'),
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
            hasNextPage &&
                !isChangingPage
                ? onNext
                : null,
            child: isChangingPage
                ? const SizedBox(
              width: 18,
              height: 18,
              child:
              CircularProgressIndicator(
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
  const _EmptyState();

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme =
        theme.colorScheme;

    return Padding(
      padding:
      const EdgeInsets.all(32),
      child: Column(
        mainAxisAlignment:
        MainAxisAlignment.center,
        children: [
          Icon(
            Icons
                .description_outlined,
            size: 48,
            color:
            colorScheme.primary,
          ),

          const SizedBox(height: 16),

          Text(
            'Nenhum modelo encontrado',
            textAlign: TextAlign.center,
            style:
            theme.textTheme.titleLarge
                ?.copyWith(
              fontWeight:
              FontWeight.w800,
            ),
          ),

          const SizedBox(height: 8),

          Text(
            'Tente alterar a busca ou o filtro de status.',
            textAlign: TextAlign.center,
            style:
            theme.textTheme.bodyMedium
                ?.copyWith(
              color: colorScheme
                  .onSurfaceVariant,
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
      padding:
      const EdgeInsets.all(32),
      child: Column(
        mainAxisAlignment:
        MainAxisAlignment.center,
        children: [
          Icon(
            Icons
                .error_outline_rounded,
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
