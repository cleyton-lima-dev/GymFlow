import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:gymflow/app/session/session_controller.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_brand_header.dart';
import 'package:gymflow/features/home/presentation/widgets/student_bottom_navigation.dart';
import 'package:provider/provider.dart';

class StudentMorePage extends StatelessWidget {
  const StudentMorePage({super.key});

  @override
  Widget build(BuildContext context) {
    final sessionController = context.watch<SessionController>();

    final user = sessionController.user;

    final name = user?.name.trim() ?? '';

    final firstName = name.isEmpty ? 'Aluno' : name.split(RegExp(r'\s+')).first;

    final colorScheme = Theme.of(context).colorScheme;

    return Scaffold(
      body: SafeArea(
        bottom: false,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(24, 16, 24, 32),
          children: [
            Center(
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 680),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    const ProfessorAdminBrandHeader(height: 88),

                    const SizedBox(height: 28),

                    Text(
                      'Olá, $firstName! 👋',
                      style: Theme.of(context).textTheme.headlineMedium
                          ?.copyWith(fontWeight: FontWeight.w800),
                    ),

                    const SizedBox(height: 6),

                    Text(
                      'Acesse suas informações e opções da conta.',
                      style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                        color: colorScheme.onSurfaceVariant,
                        height: 1.4,
                      ),
                    ),

                    const SizedBox(height: 24),

                    Text(
                      'Conta',
                      style: Theme.of(context).textTheme.titleLarge
                          ?.copyWith(fontWeight: FontWeight.w800),
                    ),

                    const SizedBox(height: 14),

                    _MoreOptionCard(
                      icon: Icons.person_outline_rounded,
                      title: 'Meus dados',
                      description: 'Visualize suas informações pessoais.',
                      onTap: () {
                        context.push('/student/profile');
                      },
                    ),

                    const SizedBox(height: 32),

                    const SizedBox(height: 10),

                    _MoreOptionCard(
                      icon: Icons.monitor_weight_outlined,
                      title: 'Minha Avaliação Física',
                      description:
                          'Consulte sua última avaliação e medidas corporais.',
                      onTap: () {
                        context.push('/student/physical-assessment');
                      },
                    ),

                    Text(
                      'Sessão',
                      style: Theme.of(context).textTheme.titleLarge
                          ?.copyWith(fontWeight: FontWeight.w800),
                    ),

                    const SizedBox(height: 14),

                    _MoreOptionCard(
                      icon: Icons.logout_rounded,
                      title: 'Sair da conta',
                      description: 'Encerre sua sessão neste dispositivo.',
                      destructive: true,
                      onTap: () {
                        _confirmLogout(context);
                      },
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
      bottomNavigationBar: StudentBottomNavigation(
        currentItem: StudentNavItem.more,
        onHomeTap: () {
          context.go('/student');
        },
        onHistoryTap: () {
          context.go('/student/history');
        },
      ),
    );
  }

  Future<void> _confirmLogout(BuildContext context) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) {
        return AlertDialog(
          title: const Text('Sair da conta?'),
          content: const Text(
            'Você precisará entrar novamente para acessar o Avelri.',
          ),
          actions: [
            TextButton(
              onPressed: () {
                Navigator.of(dialogContext).pop(false);
              },
              child: const Text('Cancelar'),
            ),
            FilledButton(
              onPressed: () {
                Navigator.of(dialogContext).pop(true);
              },
              child: const Text('Sair'),
            ),
          ],
        );
      },
    );

    if (confirmed != true || !context.mounted) {
      return;
    }

    await context.read<SessionController>().logout();
  }
}

class _MoreOptionCard extends StatelessWidget {
  const _MoreOptionCard({
    required this.icon,
    required this.title,
    required this.description,
    required this.onTap,
    this.destructive = false,
  });

  final IconData icon;
  final String title;
  final String description;
  final VoidCallback onTap;
  final bool destructive;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    final accent = destructive ? colorScheme.error : colorScheme.primary;

    return Material(
      color: colorScheme.surface,
      borderRadius: BorderRadius.circular(16),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(16),
        child: Container(
          padding: const EdgeInsets.all(18),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(16),
            border: Border.all(
              color: destructive
                  ? colorScheme.error.withAlpha(70)
                  : colorScheme.outlineVariant.withAlpha(120),
            ),
          ),
          child: Row(
            children: [
              Container(
                width: 46,
                height: 46,
                decoration: BoxDecoration(
                  color: accent.withAlpha(18),
                  shape: BoxShape.circle,
                ),
                child: Icon(icon, color: accent),
              ),

              const SizedBox(width: 14),

              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      title,
                      style: Theme.of(context).textTheme.titleMedium?.copyWith(
                        color: destructive ? colorScheme.error : null,
                        fontWeight: FontWeight.w800,
                      ),
                    ),

                    const SizedBox(height: 4),

                    Text(
                      description,
                      style: Theme.of(context).textTheme.bodySmall?.copyWith(
                        color: colorScheme.onSurfaceVariant,
                        height: 1.35,
                      ),
                    ),
                  ],
                ),
              ),

              const SizedBox(width: 10),

              Icon(
                Icons.chevron_right_rounded,
                color: destructive
                    ? colorScheme.error
                    : colorScheme.onSurfaceVariant,
              ),
            ],
          ),
        ),
      ),
    );
  }
}
