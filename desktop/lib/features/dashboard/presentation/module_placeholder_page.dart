import 'package:flutter/material.dart';

class ModulePlaceholderPage extends StatelessWidget {
  const ModulePlaceholderPage({
    required this.title,
    super.key,
  });

  final String title;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Text(
        '$title — módulo ainda não implementado',
        style: Theme.of(context).textTheme.titleLarge,
      ),
    );
  }
}
