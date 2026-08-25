import 'package:flutter/material.dart';
import 'package:gymflow/app/session/app_role.dart';
import 'package:gymflow/app/session/session_controller.dart';
import 'package:provider/provider.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_bottom_navigation.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_brand_header.dart';
import 'package:go_router/go_router.dart';

class ProfessorAdminHomePage extends StatelessWidget {
  const ProfessorAdminHomePage({super.key});

  @override
  Widget build(BuildContext context) {
    final user = context.select<SessionController, dynamic>(
          (controller) => controller.user,
    );


    if (user == null) {
      return const SizedBox.shrink();
    }

    final firstName = _firstName(user.name);
    final isAdmin = user.role == AppRole.admin;

    return Scaffold(
      body: SafeArea(
        bottom: false,
        child: SingleChildScrollView(
          padding: const EdgeInsets.fromLTRB(24, 24, 24, 32),
          child: Center(
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 680),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  const ProfessorAdminBrandHeader(
                    height: 138,
                  ),

                  const SizedBox(height: 38),

                  Text(
                    'Olá, $firstName! 👋',
                    style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                      fontWeight: FontWeight.w800,
                    ),
                  ),

                  const SizedBox(height: 8),

                  Text(
                    'Gerencie seus alunos e treinos de forma rápida e prática.',
                    style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                      color: Theme.of(context)
                          .colorScheme
                          .onSurfaceVariant,
                      height: 1.45,
                    ),
                  ),

                  const SizedBox(height: 32),

                  _StudentSearchCard(
                    onTap: () => _goToStudents(context),
                  ),

                  const SizedBox(height: 24),

                  _QuickActionsCard(
                    isAdmin: isAdmin,
                    onStudentsTap: () => _goToStudents(context),
                  ),

                  const SizedBox(height: 24),

                  const _TipCard(),
                ],
              ),
            ),
          ),
        ),
      ),
      bottomNavigationBar: const ProfessorAdminBottomNavigation(
        currentItem: ProfessorAdminNavItem.home,
      ),
    );
  }

  void _goToStudents(BuildContext context) {
    final user = context.read<SessionController>().user;

    if (user == null) {
      return;
    }

    final path = switch (user.role) {
      AppRole.admin => '/admin/students',
      AppRole.professor => '/professor/students',
      AppRole.student => null,
    };

    if (path != null) {
      context.go(path);
    }
  }

  String _firstName(String name) {
    final normalized = name.trim();

    if (normalized.isEmpty) {
      return 'Professor';
    }

    return normalized.split(RegExp(r'\s+')).first;
  }
}


class _StudentSearchCard extends StatelessWidget {
  const _StudentSearchCard({
    required this.onTap,
  });

  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return _SectionCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            'Buscar aluno',
            style: theme.textTheme.titleLarge?.copyWith(
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 18),
          Material(
            color: Colors.transparent,
            borderRadius: BorderRadius.circular(14),
            child: InkWell(
              onTap: onTap,
              borderRadius: BorderRadius.circular(14),
              child: Container(
                constraints: const BoxConstraints(
                  minHeight: 58,
                ),
                padding: const EdgeInsets.symmetric(horizontal: 16),
                decoration: BoxDecoration(
                  borderRadius: BorderRadius.circular(14),
                  border: Border.all(
                    color: colorScheme.outlineVariant,
                  ),
                ),
                child: Row(
                  children: [
                    Icon(
                      Icons.search_rounded,
                      color: colorScheme.primary,
                      size: 30,
                    ),
                    const SizedBox(width: 14),
                    Expanded(
                      child: Text(
                        'Buscar por nome, e-mail ou telefone...',
                        overflow: TextOverflow.ellipsis,
                        style: theme.textTheme.bodyLarge?.copyWith(
                          color: colorScheme.onSurfaceVariant,
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
          const SizedBox(height: 16),
          Text(
            'Encontre rapidamente os alunos para visualizar treinos e dados.',
            style: theme.textTheme.bodyMedium?.copyWith(
              color: colorScheme.onSurfaceVariant,
              height: 1.4,
            ),
          ),
        ],
      ),
    );
  }
}

class _QuickActionsCard extends StatelessWidget {
  const _QuickActionsCard({
    required this.isAdmin,
    required this.onStudentsTap,
  });

  final bool isAdmin;
  final VoidCallback onStudentsTap;


  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return _SectionCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            'Ações rápidas',
            style: theme.textTheme.titleLarge?.copyWith(
              fontWeight: FontWeight.w800,
            ),
          ),

          const SizedBox(height: 20),

          LayoutBuilder(
            builder: (context, constraints) {
              final compact = constraints.maxWidth < 280;

              if (compact) {
                return Column(
                  children: [
                    _QuickActionItem(
                      icon: Icons.people_alt_rounded,
                      title: 'Alunos',
                      subtitle:
                      isAdmin ? 'Gerenciar alunos' : 'Consultar alunos',
                      onTap: onStudentsTap,
                    ),
                    const SizedBox(height: 12),
                    _QuickActionItem(
                      icon: Icons.fitness_center_rounded,
                      title: 'Modelos de treino',
                      subtitle: 'Criar e editar modelos',
                      onTap: () {
                        context.push(
                          isAdmin
                              ? '/admin/templates'
                              : '/professor/templates',
                        );
                      },
                    ),
                    const SizedBox(height: 12),
                    _QuickActionItem(
                      icon: Icons.assignment_outlined,
                      title: 'Exercícios',
                      subtitle: 'Banco de exercícios',
                      onTap: () {
                        context.push(
                          isAdmin
                              ? '/admin/exercises'
                              : '/professor/exercises',
                        );
                      },
                    ),
                  ],
                );
              }

              return SizedBox(
                height: 204,
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Expanded(
                      child: _QuickActionItem(
                        icon: Icons.people_alt_rounded,
                        title: 'Alunos',
                        subtitle:
                        isAdmin ? 'Gerenciar alunos' : 'Consultar alunos',
                        onTap: onStudentsTap,
                      ),
                    ),
                    const SizedBox(width: 10),
                    Expanded(
                      child: _QuickActionItem(
                        icon: Icons.fitness_center_rounded,
                        title: 'Modelos de treino',
                        subtitle: 'Criar e editar modelos',
                        onTap: () {
                          context.push(
                            isAdmin
                                ? '/admin/templates'
                                : '/professor/templates',
                          );
                        },
                      ),
                    ),
                    const SizedBox(width: 10),
                    Expanded(
                      child: _QuickActionItem(
                        icon: Icons.assignment_outlined,
                        title: 'Exercícios',
                        subtitle: 'Banco de exercícios',
                        onTap: () {
                          context.push(
                            isAdmin
                                ? '/admin/exercises'
                                : '/professor/exercises',
                          );
                        },
                      ),
                    ),
                  ],
                ),
              );
            },
          ),
        ],
      ),
    );
  }
}

class _QuickActionItem extends StatelessWidget {
  const _QuickActionItem({
    required this.icon,
    required this.title,
    required this.subtitle,
    this.onTap,
  });

  final IconData icon;
  final String title;
  final String subtitle;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Material(
      color: Colors.transparent,
      borderRadius: BorderRadius.circular(14),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(14),
        child: Container(
          constraints: const BoxConstraints(
            minHeight: 174,
          ),
          padding: const EdgeInsets.symmetric(
            horizontal: 8,
            vertical: 12,
          ),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(14),
            border: Border.all(
              color: colorScheme.outlineVariant,
            ),
          ),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Container(
                width: 56,
                height: 56,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  color: colorScheme.primary.withAlpha(24),
                ),
                child: Icon(
                  icon,
                  color: colorScheme.primary,
                  size: 28,
                ),
              ),
              const SizedBox(height: 14),
              Text(
                title,
                textAlign: TextAlign.center,
                maxLines: 2,
                style: theme.textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.w800,
                  fontSize: 13,
                  height: 1.15,
                ),
              ),
              const SizedBox(height: 6),
              Text(
                subtitle,
                textAlign: TextAlign.center,
                maxLines: 3,
                style: theme.textTheme.bodySmall?.copyWith(
                  color: colorScheme.onSurfaceVariant,
                  fontSize: 11,
                  height: 1.25,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _TipCard extends StatelessWidget {
  const _TipCard();

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: colorScheme.primary.withAlpha(18),
        borderRadius: BorderRadius.circular(18),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.center,
        children: [
          Container(
            width: 54,
            height: 54,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: colorScheme.primary.withAlpha(24),
            ),
            child: Icon(
              Icons.lightbulb_outline_rounded,
              color: colorScheme.primary,
              size: 28,
            ),
          ),
          const SizedBox(width: 16),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Dica',
                  style: theme.textTheme.titleMedium?.copyWith(
                    color: colorScheme.primary,
                    fontWeight: FontWeight.w800,
                  ),
                ),
                const SizedBox(height: 5),
                Text(
                  'Utilize os modelos de treino para agilizar a criação '
                      'de treinos para seus alunos.',
                  style: theme.textTheme.bodyMedium?.copyWith(
                    color: colorScheme.onSurfaceVariant,
                    height: 1.45,
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

class _SectionCard extends StatelessWidget {
  const _SectionCard({
    required this.child,
  });

  final Widget child;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(
          color: colorScheme.outlineVariant.withAlpha(120),
        ),
      ),
      child: child,
    );
  }
}
