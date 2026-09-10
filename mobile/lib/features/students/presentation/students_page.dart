import 'package:flutter/material.dart';
import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_bottom_navigation.dart';
import 'package:gymflow/features/students/data/students_service.dart';
import 'package:gymflow/features/students/models/student_summary.dart';
import 'package:gymflow/features/students/presentation/students_view_model.dart';
import 'package:provider/provider.dart';
import 'package:go_router/go_router.dart';
import 'package:gymflow/app/session/app_role.dart';
import 'package:gymflow/app/session/session_controller.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_brand_header.dart';

class StudentsPage extends StatelessWidget {
  const StudentsPage({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) => StudentsViewModel(
        StudentsService(
          context.read<ApiClient>(),
        ),
      )..loadInitial(),
      child: const _StudentsView(),
    );
  }
}

class _StudentsView extends StatefulWidget {
  const _StudentsView();

  @override
  State<_StudentsView> createState() => _StudentsViewState();
}

class _StudentsViewState extends State<_StudentsView> {
  final _searchController = TextEditingController();

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final viewModel = context.watch<StudentsViewModel>();
    final isAdmin = context.select<SessionController, bool>(
          (controller) => controller.user?.role == AppRole.admin,
    );
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Scaffold(
      body: SafeArea(
        bottom: false,
        child: RefreshIndicator(
          onRefresh: viewModel.refresh,
          child: CustomScrollView(
            physics: const AlwaysScrollableScrollPhysics(),
            slivers: [
              const SliverPadding(
                padding: EdgeInsets.fromLTRB(24, 22, 24, 0),
                sliver: SliverToBoxAdapter(
                  child: ProfessorAdminBrandHeader(
                    height: 100,
                  ),
                ),
              ),

              SliverPadding(
                padding: const EdgeInsets.fromLTRB(24, 28, 24, 0),
                sliver: SliverToBoxAdapter(
                  child: Row(
                    children: [
                      Expanded(
                        child: Text(
                          'Alunos',
                          style: theme.textTheme.headlineLarge?.copyWith(
                            fontWeight: FontWeight.w800,
                          ),
                        ),
                      ),
                      if (isAdmin)
                        FilledButton.icon(
                          onPressed: () async {
                            final created = await context.push<bool>(
                              '/admin/students/new',
                            );

                            if (!context.mounted || created != true) {
                              return;
                            }

                            await context
                                .read<StudentsViewModel>()
                                .loadInitial();
                          },
                          icon: const Icon(
                            Icons.person_add_alt_1_rounded,
                            size: 20,
                          ),
                          label: const Text('Novo aluno'),
                        ),
                    ],
                  ),
                ),
              ),

              SliverPadding(
                padding: const EdgeInsets.fromLTRB(24, 6, 24, 0),
                sliver: SliverToBoxAdapter(
                  child: Text(
                    'Gerencie e acompanhe seus alunos.',
                    style: theme.textTheme.bodyLarge?.copyWith(
                      color: colorScheme.onSurfaceVariant,
                    ),
                  ),
                ),
              ),

              SliverPadding(
                padding: const EdgeInsets.fromLTRB(24, 26, 24, 0),
                sliver: SliverToBoxAdapter(
                  child: TextField(
                    controller: _searchController,
                    keyboardType: TextInputType.text,
                    textInputAction: TextInputAction.search,
                    onChanged: (value) {
                      viewModel.updateSearch(value);
                      setState(() {});
                    },
                    decoration: InputDecoration(
                      hintText: 'Buscar aluno por nome, e-mail ou telefone...',
                      prefixIcon: Icon(
                        Icons.search_rounded,
                        color: colorScheme.primary,
                        size: 28,
                      ),
                      suffixIcon: _searchController.text.isEmpty
                          ? null
                          : IconButton(
                        tooltip: 'Limpar busca',
                        onPressed: () {
                          _searchController.clear();
                          viewModel.updateSearch('');
                          setState(() {});
                        },
                        icon: const Icon(
                          Icons.close_rounded,
                        ),
                      ),
                      filled: true,
                      fillColor: colorScheme.surface,
                      contentPadding: const EdgeInsets.symmetric(
                        horizontal: 16,
                        vertical: 17,
                      ),
                      border: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(14),
                        borderSide: BorderSide(
                          color: colorScheme.outlineVariant,
                        ),
                      ),
                      enabledBorder: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(14),
                        borderSide: BorderSide(
                          color: colorScheme.outlineVariant,
                        ),
                      ),
                      focusedBorder: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(14),
                        borderSide: BorderSide(
                          color: colorScheme.primary,
                          width: 1.5,
                        ),
                      ),
                    ),
                  ),
                ),
              ),

              SliverPadding(
                padding: const EdgeInsets.fromLTRB(24, 22, 24, 10),
                sliver: SliverToBoxAdapter(
                  child: Row(
                    children: [
                      Expanded(
                        child: Text.rich(
                          TextSpan(
                            children: [
                              const TextSpan(
                                text: 'Total de alunos: ',
                              ),
                              TextSpan(
                                text: '${viewModel.totalCount}',
                                style: TextStyle(
                                  color: colorScheme.primary,
                                  fontWeight: FontWeight.w800,
                                ),
                              ),
                            ],
                          ),
                          style: theme.textTheme.bodyMedium?.copyWith(
                            color: colorScheme.onSurfaceVariant,
                          ),
                        ),
                      ),
                      _StatusFilter(
                        currentFilter: viewModel.statusFilter,
                        onSelected: viewModel.updateStatusFilter,
                      ),
                    ],
                  ),
                ),
              ),

              if (viewModel.isLoading && viewModel.hasStudents)
                SliverToBoxAdapter(
                  child: LinearProgressIndicator(
                    minHeight: 2,
                    color: colorScheme.primary,
                    backgroundColor: Colors.transparent,
                  ),
                ),

              if (viewModel.errorMessage != null &&
                  viewModel.hasStudents)
                SliverPadding(
                  padding: const EdgeInsets.fromLTRB(24, 10, 24, 0),
                  sliver: SliverToBoxAdapter(
                    child: _InlineError(
                      message: viewModel.errorMessage!,
                      onRetry: viewModel.retry,
                    ),
                  ),
                ),

              if (viewModel.isLoading && !viewModel.hasStudents)
                const SliverFillRemaining(
                  hasScrollBody: false,
                  child: Center(
                    child: CircularProgressIndicator(),
                  ),
                )
              else if (viewModel.errorMessage != null &&
                  !viewModel.hasStudents)
                SliverFillRemaining(
                  hasScrollBody: false,
                  child: _FullErrorState(
                    message: viewModel.errorMessage!,
                    onRetry: viewModel.retry,
                  ),
                )
              else if (!viewModel.hasStudents)
                  SliverFillRemaining(
                    hasScrollBody: false,
                    child: _EmptyStudentsState(
                      hasSearch: viewModel.hasSearch,
                      hasFilter: viewModel.hasActiveFilter,
                      onCreateStudentTap: isAdmin
                          ? () async {
                        final created = await context.push<bool>(
                          '/admin/students/new',
                        );

                        if (!context.mounted || created != true) {
                          return;
                        }

                        await context
                            .read<StudentsViewModel>()
                            .loadInitial();
                      }
                          : null,
                    ),
                  )
                else
                  SliverPadding(
                    padding: const EdgeInsets.fromLTRB(24, 8, 24, 0),
                    sliver: SliverList(
                      delegate: SliverChildBuilderDelegate(
                            (context, index) {
                          final student = viewModel.students[index];

                          return Padding(
                            padding: const EdgeInsets.only(
                              bottom: 12,
                            ),
                            child: _StudentCard(
                              student: student,
                              onTap: () {
                                final user = context.read<SessionController>().user;

                                if (user == null) {
                                  return;
                                }

                                final basePath = switch (user.role) {
                                  AppRole.admin => '/admin',
                                  AppRole.professor => '/professor',
                                  AppRole.student => null,
                                };

                                if (basePath != null) {
                                  context.push<bool>(
                                    '$basePath/students/${student.id}',
                                  ).then((updated) {
                                    if (updated == true && context.mounted) {
                                      context.read<StudentsViewModel>().refresh();
                                    }
                                  });
                                }
                              },
                            ),
                          );
                        },
                        childCount: viewModel.students.length,
                      ),
                    ),
                  ),

              if (viewModel.hasStudents)
                SliverPadding(
                  padding: const EdgeInsets.fromLTRB(
                    24,
                    10,
                    24,
                    28,
                  ),
                  sliver: SliverToBoxAdapter(
                    child: _Pagination(
                      page: viewModel.page,
                      totalPages: viewModel.totalPages,
                      hasPrevious: viewModel.hasPreviousPage,
                      hasNext: viewModel.hasNextPage,
                      isLoading: viewModel.isLoading,
                      onPrevious: viewModel.goToPreviousPage,
                      onNext: viewModel.goToNextPage,
                    ),
                  ),
                ),

              const SliverToBoxAdapter(
                child: SizedBox(height: 10),
              ),
            ],
          ),
        ),
      ),
      bottomNavigationBar: const ProfessorAdminBottomNavigation(
        currentItem: ProfessorAdminNavItem.students,
      ),
    );
  }
}

class _StatusFilter extends StatelessWidget {
  const _StatusFilter({
    required this.currentFilter,
    required this.onSelected,
  });

  final StudentsStatusFilter currentFilter;
  final ValueChanged<StudentsStatusFilter> onSelected;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return PopupMenuButton<StudentsStatusFilter>(
      tooltip: 'Filtrar alunos por status',
      initialValue: currentFilter,
      onSelected: onSelected,
      itemBuilder: (context) => const [
        PopupMenuItem(
          value: StudentsStatusFilter.all,
          child: Text('Todos'),
        ),
        PopupMenuItem(
          value: StudentsStatusFilter.active,
          child: Text('Ativos'),
        ),
        PopupMenuItem(
          value: StudentsStatusFilter.inactive,
          child: Text('Inativos'),
        ),
      ],
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            'Status: ',
            style: theme.textTheme.bodyMedium?.copyWith(
              color: colorScheme.onSurfaceVariant,
            ),
          ),
          Text(
            _label,
            style: theme.textTheme.bodyMedium?.copyWith(
              color: colorScheme.primary,
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(width: 4),
          Icon(
            Icons.keyboard_arrow_down_rounded,
            color: colorScheme.onSurfaceVariant,
          ),
        ],
      ),
    );
  }

  String get _label {
    return switch (currentFilter) {
      StudentsStatusFilter.all => 'Todos',
      StudentsStatusFilter.active => 'Ativos',
      StudentsStatusFilter.inactive => 'Inativos',
    };
  }
}

class _StudentCard extends StatelessWidget {
  const _StudentCard({
    required this.student,
    required this.onTap,
  });

  final StudentSummary student;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    final statusColor = student.isActive
        ? const Color(0xFF22C55E)
        : colorScheme.error;

    return Material(
      color: Colors.transparent,
      borderRadius: BorderRadius.circular(16),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(16),
        child: Container(
          padding: const EdgeInsets.all(16),
          decoration: BoxDecoration(
            color: colorScheme.surface,
            borderRadius: BorderRadius.circular(16),
            border: Border.all(
              color: colorScheme.outlineVariant.withAlpha(120),
            ),
          ),
          child: Row(
            children: [
              Container(
                width: 58,
                height: 58,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  color: colorScheme.primary.withAlpha(22),
                ),
                child: Text(
                  _initials(student.name),
                  style: theme.textTheme.titleLarge?.copyWith(
                    color: colorScheme.primary,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),

              const SizedBox(width: 14),

              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      student.name,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: theme.textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.w800,
                      ),
                    ),

                    const SizedBox(height: 7),

                    _StudentInfoLine(
                      icon: Icons.mail_outline_rounded,
                      text: student.email,
                    ),

                    const SizedBox(height: 5),

                    _StudentInfoLine(
                      icon: Icons.phone_outlined,
                      text: student.phone?.trim().isNotEmpty == true
                          ? student.phone!
                          : 'Telefone não informado',
                    ),
                  ],
                ),
              ),

              const SizedBox(width: 10),

              Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 10,
                      vertical: 6,
                    ),
                    decoration: BoxDecoration(
                      color: statusColor.withAlpha(24),
                      borderRadius: BorderRadius.circular(10),
                    ),
                    child: Text(
                      student.isActive ? 'Ativo' : 'Inativo',
                      style: theme.textTheme.labelMedium?.copyWith(
                        color: statusColor,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                  ),

                  const SizedBox(height: 8),

                  Icon(
                    Icons.chevron_right_rounded,
                    color: colorScheme.onSurfaceVariant,
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
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
      return parts.first.substring(0, 1).toUpperCase();
    }

    return '${parts.first.substring(0, 1)}'
        '${parts.last.substring(0, 1)}'
        .toUpperCase();
  }
}
class _StudentInfoLine extends StatelessWidget {
  const _StudentInfoLine({
    required this.icon,
    required this.text,
  });

  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Row(
      children: [
        Icon(
          icon,
          size: 17,
          color: colorScheme.onSurfaceVariant,
        ),
        const SizedBox(width: 7),
        Expanded(
          child: Text(
            text,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: theme.textTheme.bodySmall?.copyWith(
              color: colorScheme.onSurfaceVariant,
            ),
          ),
        ),
      ],
    );
  }
}

class _Pagination extends StatelessWidget {
  const _Pagination({
    required this.page,
    required this.totalPages,
    required this.hasPrevious,
    required this.hasNext,
    required this.isLoading,
    required this.onPrevious,
    required this.onNext,
  });

  final int page;
  final int totalPages;
  final bool hasPrevious;
  final bool hasNext;
  final bool isLoading;
  final VoidCallback onPrevious;
  final VoidCallback onNext;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Row(
      children: [
        Expanded(
          child: OutlinedButton.icon(
            onPressed:
            hasPrevious && !isLoading ? onPrevious : null,
            icon: const Icon(
              Icons.chevron_left_rounded,
              size: 20,
            ),
            label: const Text('Anterior'),
          ),
        ),

        const SizedBox(width: 12),

        Text(
          totalPages > 0
              ? '$page de $totalPages'
              : '0 de 0',
          style: theme.textTheme.bodyMedium?.copyWith(
            color: colorScheme.onSurfaceVariant,
            fontWeight: FontWeight.w600,
          ),
        ),

        const SizedBox(width: 12),

        Expanded(
          child: OutlinedButton.icon(
            onPressed:
            hasNext && !isLoading ? onNext : null,
            iconAlignment: IconAlignment.end,
            icon: const Icon(
              Icons.chevron_right_rounded,
              size: 20,
            ),
            label: const Text('Próxima'),
          ),
        ),
      ],
    );
  }
}

class _InlineError extends StatelessWidget {
  const _InlineError({
    required this.message,
    required this.onRetry,
  });

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: colorScheme.errorContainer,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Row(
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
                color: colorScheme.onErrorContainer,
              ),
            ),
          ),
          TextButton(
            onPressed: onRetry,
            child: const Text('Tentar novamente'),
          ),
        ],
      ),
    );
  }
}

class _FullErrorState extends StatelessWidget {
  const _FullErrorState({
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
      child: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(
              Icons.cloud_off_outlined,
              size: 46,
              color: colorScheme.onSurfaceVariant,
            ),
            const SizedBox(height: 14),
            Text(
              message,
              textAlign: TextAlign.center,
              style: theme.textTheme.bodyLarge,
            ),
            const SizedBox(height: 18),
            FilledButton(
              onPressed: onRetry,
              child: const Text('Tentar novamente'),
            ),
          ],
        ),
      ),
    );
  }
}

class _EmptyStudentsState extends StatelessWidget {
  const _EmptyStudentsState({
    required this.hasSearch,
    required this.hasFilter,
    this.onCreateStudentTap,
  });

  final bool hasSearch;
  final bool hasFilter;
  final VoidCallback? onCreateStudentTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    final filtered = hasSearch || hasFilter;

    return Padding(
      padding: const EdgeInsets.all(32),
      child: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(
              filtered
                  ? Icons.search_off_rounded
                  : Icons.people_outline_rounded,
              size: 48,
              color: colorScheme.onSurfaceVariant,
            ),
            const SizedBox(height: 14),
            Text(
              filtered
                  ? 'Nenhum aluno encontrado.'
                  : 'Nenhum aluno cadastrado.',
              textAlign: TextAlign.center,
              style: theme.textTheme.titleMedium?.copyWith(
                fontWeight: FontWeight.w700,
              ),
            ),
            const SizedBox(height: 7),
            Text(
              filtered
                  ? 'Tente alterar a busca ou o filtro de status.'
                  : 'Os alunos cadastrados aparecerão aqui.',
              textAlign: TextAlign.center,
              style: theme.textTheme.bodyMedium?.copyWith(
                color: colorScheme.onSurfaceVariant,
              ),
            ),
            if (!filtered && onCreateStudentTap != null) ...[
              const SizedBox(height: 18),

              FilledButton.icon(
                onPressed: onCreateStudentTap,
                icon: const Icon(Icons.person_add_alt_1_rounded),
                label: const Text('Cadastrar aluno'),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
