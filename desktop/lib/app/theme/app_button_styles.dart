import 'package:flutter/material.dart';

/// Shared action styles for the Avelri desktop application.
///
/// Filled, outlined, text and icon buttons receive their common geometry from
/// [AppTheme]. Use this destructive style only for actions with irreversible
/// or materially negative consequences.
class AppButtonStyles {
  const AppButtonStyles._();

  static ButtonStyle destructive(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    return FilledButton.styleFrom(
      backgroundColor: colorScheme.error,
      foregroundColor: colorScheme.onError,
    );
  }

  static ButtonStyle destructiveOutlined(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    return OutlinedButton.styleFrom(
      foregroundColor: colorScheme.error,
      side: BorderSide(color: colorScheme.error),
    );
  }

  static ButtonStyle destructiveText(BuildContext context) {
    return TextButton.styleFrom(
      foregroundColor: Theme.of(context).colorScheme.error,
    );
  }
}
