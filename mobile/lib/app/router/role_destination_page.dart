import 'package:flutter/material.dart';

class RoleDestinationPage extends StatelessWidget {
  const RoleDestinationPage({
    required this.title,
    super.key,
  });

  final String title;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Center(
        child: Text(title),
      ),
    );
  }
}
