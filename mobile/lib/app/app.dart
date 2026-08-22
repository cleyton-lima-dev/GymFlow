import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:gymflow/app/session/session_controller.dart';
import 'package:provider/provider.dart';
import 'package:gymflow/app/theme/app_theme.dart';
import 'package:gymflow/app/theme/branding_controller.dart';

class GymFlowApp extends StatelessWidget {
  const GymFlowApp({
    required this.sessionController,
    required this.brandingController,
    required this.router,
    super.key,
  });

  final SessionController sessionController;
  final BrandingController brandingController;
  final GoRouter router;

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        ChangeNotifierProvider.value(value: sessionController),
        ChangeNotifierProvider.value(value: brandingController),
      ],
      child: Consumer<BrandingController>(
        builder: (context, brandingController, child) {
          return MaterialApp.router(
            title: brandingController.branding.displayName,
            theme: AppTheme.fromBranding(
              brandingController.branding,
            ),
            routerConfig: router,
          );
        },
      ),
    );
  }
}
