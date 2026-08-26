import 'package:flutter/material.dart';

enum StudentNavItem { home, history, more }

class StudentBottomNavigation extends StatelessWidget {
  const StudentBottomNavigation({
    required this.currentItem,
    this.onHomeTap,
    this.onHistoryTap,
    this.onMoreTap,
    super.key,
  });

  final StudentNavItem currentItem;

  final VoidCallback? onHomeTap;
  final VoidCallback? onHistoryTap;
  final VoidCallback? onMoreTap;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return SafeArea(
      top: false,
      child: Container(
        decoration: BoxDecoration(
          color: colorScheme.surface,
          border: Border(
            top: BorderSide(color: colorScheme.outlineVariant.withAlpha(100)),
          ),
        ),
        child: Row(
          children: [
            _NavigationItem(
              icon: Icons.home_rounded,
              label: 'Início',
              selected: currentItem == StudentNavItem.home,
              onTap: onHomeTap,
            ),
            _NavigationItem(
              icon: Icons.history_rounded,
              label: 'Histórico',
              selected: currentItem == StudentNavItem.history,
              onTap: onHistoryTap,
            ),
            _NavigationItem(
              icon: Icons.person_outline_rounded,
              label: 'Mais',
              selected: currentItem == StudentNavItem.more,
              onTap: onMoreTap,
            ),
          ],
        ),
      ),
    );
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

    final color = selected ? colorScheme.primary : colorScheme.onSurfaceVariant;

    return Expanded(
      child: InkWell(
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.symmetric(vertical: 10),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(icon, color: color, size: 27),
              const SizedBox(height: 4),
              Text(
                label,
                style: Theme.of(context).textTheme.labelMedium?.copyWith(
                  color: color,
                  fontWeight: selected ? FontWeight.w700 : FontWeight.w500,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
