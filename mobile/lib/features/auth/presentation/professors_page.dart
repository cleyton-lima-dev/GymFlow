import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/features/auth/data/auth_service.dart';
import 'package:gymflow/features/auth/data/professor_response.dart';
import 'package:gymflow/features/auth/presentation/professors_view_model.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_bottom_navigation.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_page_header.dart';
import 'package:provider/provider.dart';

class ProfessorsPage extends StatelessWidget {
  const ProfessorsPage({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) => ProfessorsViewModel(
        AuthService(
          context.read<ApiClient>(),
        ),
      )..loadProfessors(),
      child: const _ProfessorsView(),
    );
  }
}

class _ProfessorsView extends StatelessWidget {
  const _ProfessorsView();

  Future<void> _openCreateProfessor(
      BuildContext context,
      ) async {
    final created = await context.push<bool>(
      '/admin/professors/new',
    );

    if (!context.mounted || created != true) {
      return;
    }

    await context
        .read<ProfessorsViewModel>()
        .loadProfessors();
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;
    final viewModel = context.watch<ProfessorsViewModel>();

    return Scaffold(
      body: SafeArea(
        bottom: false,
        child: RefreshIndicator(
          onRefresh: viewModel.loadProfessors,
          child: ListView(
            padding: const EdgeInsets.fromLTRB(
              24,
              18,
              24,
              32,
            ),
            children: [
              const ProfessorAdminPageHeader(),

              const SizedBox(height: 24),

              Row(
                crossAxisAlignment:
                CrossAxisAlignment.start,
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment:
                      CrossAxisAlignment.start,
                      children: [
                        Text(
                          'Professores',
                          style: theme
                              .textTheme
                              .headlineLarge
                              ?.copyWith(
                            fontWeight:
                            FontWeight.w800,
                          ),
                        ),
                        const SizedBox(height: 8),
                        Text(
                          'Gerencie os professores desta academia.',
                          style: theme
                              .textTheme
                              .bodyLarge
                              ?.copyWith(
                            color: colorScheme
                                .onSurfaceVariant,
                            height: 1.45,
                          ),
                        ),
                      ],
                    ),
                  ),

                  const SizedBox(width: 16),

                  FilledButton.icon(
                    onPressed: () =>
                        _openCreateProfessor(context),
                    icon: const Icon(
                      Icons.person_add_alt_1_rounded,
                    ),
                    label:
                    const Text('Novo professor'),
                  ),
                ],
              ),

              const SizedBox(height: 28),

              if (viewModel.isLoading &&
                  viewModel.professors.isEmpty)
                const Padding(
                  padding:
                  EdgeInsets.symmetric(vertical: 64),
                  child: Center(
                    child: CircularProgressIndicator(),
                  ),
                )
              else if (viewModel.errorMessage != null)
                _ErrorState(
                  message: viewModel.errorMessage!,
                  onRetry: viewModel.loadProfessors,
                )
              else if (viewModel.professors.isEmpty)
                  _EmptyState(
                    onCreate: () =>
                        _openCreateProfessor(context),
                  )
                else ...[
                    Text(
                      '${viewModel.professors.length} '
                          '${viewModel.professors.length == 1 ? 'professor' : 'professores'}',
                      style:
                      theme.textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.w700,
                      ),
                    ),

                    const SizedBox(height: 14),

                    ...viewModel.professors.map(
                          (professor) => Padding(
                        padding:
                        const EdgeInsets.only(bottom: 12),
                        child: _ProfessorCard(
                          professor: professor,
                        ),
                      ),
                    ),
                  ],
            ],
          ),
        ),
      ),
      bottomNavigationBar:
      const ProfessorAdminBottomNavigation(
        currentItem: ProfessorAdminNavItem.more,
      ),
    );
  }
}

class _ProfessorCard extends StatelessWidget {
  const _ProfessorCard({
    required this.professor,
  });

  final ProfessorResponse professor;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    final initial = professor.name.trim().isEmpty
        ? '?'
        : professor.name.trim()[0].toUpperCase();

    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(18),
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
              color: colorScheme.primary.withAlpha(18),
            ),
            child: Text(
              initial,
              style: theme.textTheme.titleLarge?.copyWith(
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
                  professor.name,
                  style:
                  theme.textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.w800,
                  ),
                ),
                const SizedBox(height: 5),
                Row(
                  children: [
                    Icon(
                      Icons.mail_outline_rounded,
                      size: 17,
                      color:
                      colorScheme.onSurfaceVariant,
                    ),
                    const SizedBox(width: 6),
                    Expanded(
                      child: Text(
                        professor.email,
                        overflow: TextOverflow.ellipsis,
                        style: theme.textTheme.bodyMedium
                            ?.copyWith(
                          color: colorScheme
                              .onSurfaceVariant,
                        ),
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),

          const SizedBox(width: 12),

          Container(
            padding: const EdgeInsets.symmetric(
              horizontal: 10,
              vertical: 6,
            ),
            decoration: BoxDecoration(
              color: professor.isActive
                  ? Colors.green.withAlpha(18)
                  : colorScheme.error.withAlpha(18),
              borderRadius: BorderRadius.circular(999),
            ),
            child: Text(
              professor.isActive ? 'Ativo' : 'Inativo',
              style: theme.textTheme.labelMedium?.copyWith(
                color: professor.isActive
                    ? Colors.green.shade700
                    : colorScheme.error,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _EmptyState extends StatelessWidget {
  const _EmptyState({
    required this.onCreate,
  });

  final VoidCallback onCreate;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 56),
      child: Column(
        children: [
          Icon(
            Icons.groups_2_outlined,
            size: 58,
            color: colorScheme.primary,
          ),
          const SizedBox(height: 18),
          Text(
            'Nenhum professor cadastrado',
            style: theme.textTheme.titleLarge?.copyWith(
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 8),
          Text(
            'Cadastre o primeiro professor desta academia.',
            textAlign: TextAlign.center,
            style: theme.textTheme.bodyMedium?.copyWith(
              color: colorScheme.onSurfaceVariant,
            ),
          ),
          const SizedBox(height: 20),
          FilledButton.icon(
            onPressed: onCreate,
            icon: const Icon(
              Icons.person_add_alt_1_rounded,
            ),
            label: const Text('Novo professor'),
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
  final Future<void> Function() onRetry;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 56),
      child: Column(
        children: [
          Icon(
            Icons.error_outline_rounded,
            size: 52,
            color: colorScheme.error,
          ),
          const SizedBox(height: 16),
          Text(
            message,
            textAlign: TextAlign.center,
            style: theme.textTheme.bodyLarge,
          ),
          const SizedBox(height: 20),
          OutlinedButton.icon(
            onPressed: onRetry,
            icon: const Icon(Icons.refresh_rounded),
            label: const Text('Tentar novamente'),
          ),
        ],
      ),
    );
  }
}
