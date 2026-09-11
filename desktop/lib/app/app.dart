import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'package:avelri_gestao/app/session/session_controller.dart';
import 'package:avelri_gestao/app/theme/app_theme.dart';
import 'package:avelri_gestao/app/theme/branding_controller.dart';
import 'package:avelri_gestao/core/network/api_client.dart';

class AvelriGestaoApp extends StatelessWidget {
  const AvelriGestaoApp({
    required this.sessionController,
    required this.brandingController,
    required this.apiClient,
    required this.router,
    super.key,
  });

  final SessionController sessionController;
  final BrandingController brandingController;
  final ApiClient apiClient;
  final GoRouter router;

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        Provider<ApiClient>.value(value: apiClient),
        ChangeNotifierProvider.value(value: sessionController),
        ChangeNotifierProvider.value(value: brandingController),
      ],
      child: Consumer<BrandingController>(
        builder: (context, brandingController, child) {
          return MaterialApp.router(
            debugShowCheckedModeBanner: false,
            title: brandingController.branding.displayName,
            locale: const Locale('pt', 'BR'),
            supportedLocales: const [
              Locale('pt', 'BR'),
            ],
            localizationsDelegates: const [
              GlobalMaterialLocalizations.delegate,
              GlobalWidgetsLocalizations.delegate,
              GlobalCupertinoLocalizations.delegate,
            ],
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
