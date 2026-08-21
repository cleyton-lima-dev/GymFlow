import 'package:flutter/material.dart';
import 'package:gymflow/features/auth/presentation/login_page.dart';

class GymFlowApp extends StatelessWidget {
  const GymFlowApp({super.key});

  @override
  Widget build(BuildContext context) {
    return const MaterialApp(
      title: 'GymFlow',
      home: LoginPage(),
    );
  }
}
