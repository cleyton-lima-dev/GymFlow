import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:gymflow/app/session/session_controller.dart';
import 'package:provider/provider.dart';

class GymFlowApp extends StatelessWidget {
  const GymFlowApp({
    required this.sessionController,
    required this.router,
    super.key,
  });

  final SessionController sessionController;
  final GoRouter router;

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider.value(
      value: sessionController,
      child: MaterialApp.router(
        title: 'GymFlow',
        routerConfig: router,
      ),
    );
  }
}
