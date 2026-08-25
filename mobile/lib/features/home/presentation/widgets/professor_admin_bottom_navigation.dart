import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:gymflow/app/session/app_role.dart';
import 'package:gymflow/app/session/session_controller.dart';
import 'package:provider/provider.dart';

enum ProfessorAdminNavItem {
  home,
  students,
  templates,
  more,
}

class ProfessorAdminBottomNavigation extends StatelessWidget {
  const ProfessorAdminBottomNavigation({
    required this.currentItem,
    super.key,
  });

  final ProfessorAdminNavItem currentItem;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return SafeArea(
      top: false,
      child: Container(
        decoration: BoxDecoration(
          color: colorScheme.surface,
          border: Border(
            top: BorderSide(
              color: colorScheme.outlineVariant.withAlpha(100),
            ),
          ),
        ),
        child: Row(
          children: [
            _NavigationItem(
              icon: Icons.home_rounded,
              label: 'Início',
              selected: currentItem == ProfessorAdminNavItem.home,
              onTap: () => _goTo(
                context,
                ProfessorAdminNavItem.home,
              ),
            ),
            _NavigationItem(
              icon: Icons.people_alt_rounded,
              label: 'Alunos',
              selected: currentItem == ProfessorAdminNavItem.students,
              onTap: () => _goTo(
                context,
                ProfessorAdminNavItem.students,
              ),
            ),
            _NavigationItem(
              icon: Icons.description_rounded,
              label: 'Modelos',
              selected: currentItem == ProfessorAdminNavItem.templates,
              onTap: () => _goTo(
                context,
                ProfessorAdminNavItem.templates,
              ),
            ),
            _NavigationItem(
              icon: Icons.more_horiz_rounded,
              label: 'Mais',
              selected: currentItem == ProfessorAdminNavItem.more,
              onTap: () => _goTo(
                context,
                ProfessorAdminNavItem.more,
              ),
            ),
          ],
        ),
      ),
    );
  }

  void _goTo(
      BuildContext context,
      ProfessorAdminNavItem destination,
      ) {
    final user = context.read<SessionController>().user;

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

    final path = switch (destination) {
      ProfessorAdminNavItem.home => basePath,
      ProfessorAdminNavItem.students => '$basePath/students',
      ProfessorAdminNavItem.templates => '$basePath/templates',
      ProfessorAdminNavItem.more => '$basePath/more',
    };

    context.go(path);
  }
}

class _NavigationItem extends StatelessWidget {
  const _NavigationItem({
    required this.icon,
    required this.label,
    required this.selected,
    required this.onTap,
  });

  final IconData icon;
  final String label;
  final bool selected;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    final color = selected
        ? colorScheme.primary
        : colorScheme.onSurfaceVariant;

    return Expanded(
      child: InkWell(
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.symmetric(
            vertical: 10,
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(
                icon,
                color: color,
                size: 27,
              ),
              const SizedBox(height: 4),
              Text(
                label,
                style: Theme.of(context)
                    .textTheme
                    .labelMedium
                    ?.copyWith(
                  color: color,
                  fontWeight: selected
                      ? FontWeight.w700
                      : FontWeight.w500,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
