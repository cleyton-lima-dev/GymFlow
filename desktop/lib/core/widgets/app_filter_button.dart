import 'package:flutter/material.dart';

/// A consistent, keyboard-accessible filter choice used in list toolbars.
class AppFilterButton extends StatelessWidget {
  const AppFilterButton({
    required this.label,
    required this.selected,
    required this.onPressed,
    this.color,
    super.key,
  });

  final String label;
  final bool selected;
  final VoidCallback onPressed;
  final Color? color;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;
    final selectedColor = color ?? colorScheme.primary;

    return OutlinedButton(
      onPressed: onPressed,
      style: OutlinedButton.styleFrom(
        foregroundColor: selected
            ? colorScheme.onPrimary
            : colorScheme.onSurfaceVariant,
        backgroundColor: selected ? selectedColor : colorScheme.surface,
        side: BorderSide(color: selected ? selectedColor : theme.dividerColor),
        minimumSize: const Size(0, 48),
        padding: const EdgeInsets.symmetric(horizontal: 14),
      ),
      child: Text(label),
    );
  }
}
