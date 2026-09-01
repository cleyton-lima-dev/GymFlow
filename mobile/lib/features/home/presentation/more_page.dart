import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:gymflow/app/session/app_role.dart';
import 'package:gymflow/app/session/session_controller.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_bottom_navigation.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_brand_header.dart';
import 'package:provider/provider.dart';

class MorePage extends StatelessWidget {
  const MorePage({
    super.key,
  });

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Scaffold(
      bottomNavigationBar:
      const ProfessorAdminBottomNavigation(
        currentItem: ProfessorAdminNavItem.more,
      ),
      body: SafeArea(
        bottom: false,
        child: SingleChildScrollView(
          padding: const EdgeInsets.fromLTRB(
            20,
            16,
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
                  const ProfessorAdminBrandHeader(),

                  const SizedBox(height: 28),

                  Text(
                    'Mais',
                    style:
                    theme.textTheme.headlineSmall?.copyWith(
                      fontWeight: FontWeight.w800,
                    ),
                  ),

                  const SizedBox(height: 6),

                  Text(
                    'Acesse outras ferramentas e opções da sua conta.',
                    style:
                    theme.textTheme.bodyMedium?.copyWith(
                      color:
                      colorScheme.onSurfaceVariant,
                      height: 1.4,
                    ),
                  ),

                  const SizedBox(height: 24),

                  _MoreOptionCard(
                    icon:
                    Icons.fitness_center_rounded,
                    title: 'Banco de exercícios',
                    description:
                    'Gerencie os exercícios disponíveis na academia.',
                    onTap: () =>
                        _openExercises(context),
                  ),

                  const SizedBox(height: 12),

                  _MoreOptionCard(
                    icon: Icons.logout_rounded,
                    title: 'Sair',
                    description:
                    'Encerrar a sessão neste dispositivo.',
                    isDestructive: true,
                    onTap: () =>
                        _confirmLogout(context),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }

  void _openExercises(BuildContext context) {
    final user =
        context.read<SessionController>().user;

    if (user == null) {
      return;
    }

    final basePath = switch (user.role) {
      AppRole.admin => '/admin',
      AppRole.professor => '/professor',
      AppRole.student => null,
    };

    if (basePath == null) {
      return;
    }

    context.push(
      '$basePath/exercises',
    );
  }

  Future<void> _confirmLogout(
      BuildContext context,
      ) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) {
        return AlertDialog(
          title: const Text(
            'Sair da conta?',
          ),
          content: const Text(
            'Você precisará entrar novamente para acessar o Avelri.',
          ),
          actions: [
            TextButton(
              onPressed: () {
                Navigator.of(dialogContext).pop(
                  false,
                );
              },
              child: const Text(
                'Cancelar',
              ),
            ),
            FilledButton(
              onPressed: () {
                Navigator.of(dialogContext).pop(
                  true,
                );
              },
              child: const Text(
                'Sair',
              ),
            ),
          ],
        );
      },
    );

    if (confirmed != true ||
        !context.mounted) {
      return;
    }

    await context
        .read<SessionController>()
        .logout();
  }
}

class _MoreOptionCard extends StatelessWidget {
  const _MoreOptionCard({
    required this.icon,
    required this.title,
    required this.description,
    required this.onTap,
    this.isDestructive = false,
  });

  final IconData icon;
  final String title;
  final String description;
  final VoidCallback onTap;
  final bool isDestructive;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    final accentColor = isDestructive
        ? colorScheme.error
        : colorScheme.primary;

    return Material(
      color: colorScheme.surface,
      borderRadius: BorderRadius.circular(16),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(16),
        child: Container(
          padding: const EdgeInsets.all(18),
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
                width: 46,
                height: 46,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  color:
                  accentColor.withAlpha(18),
                ),
                child: Icon(
                  icon,
                  color: accentColor,
                ),
              ),

              const SizedBox(width: 14),

              Expanded(
                child: Column(
                  crossAxisAlignment:
                  CrossAxisAlignment.start,
                  children: [
                    Text(
                      title,
                      style: theme
                          .textTheme.titleMedium
                          ?.copyWith(
                        color: isDestructive
                            ? colorScheme.error
                            : null,
                        fontWeight:
                        FontWeight.w800,
                      ),
                    ),

                    const SizedBox(height: 4),

                    Text(
                      description,
                      style: theme
                          .textTheme.bodySmall
                          ?.copyWith(
                        color: colorScheme
                            .onSurfaceVariant,
                        height: 1.35,
                      ),
                    ),
                  ],
                ),
              ),

              const SizedBox(width: 10),

              Icon(
                Icons.chevron_right_rounded,
                color: isDestructive
                    ? colorScheme.error
                    : colorScheme
                    .onSurfaceVariant,
              ),
            ],
          ),
        ),
      ),
    );
  }
}
