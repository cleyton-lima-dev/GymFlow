import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'package:avelri_gestao/app/session/session_controller.dart';
import 'package:avelri_gestao/app/session/erp_feature_controller.dart';
import 'package:avelri_gestao/app/session/session_status.dart';
import 'package:avelri_gestao/app/theme/app_theme.dart';
import 'package:avelri_gestao/app/theme/branding_controller.dart';
import 'package:avelri_gestao/core/network/api_client.dart';

class AvelriGestaoApp extends StatefulWidget {
  const AvelriGestaoApp({
    required this.sessionController,
    required this.brandingController,
    required this.erpFeatureController,
    required this.apiClient,
    required this.router,
    super.key,
  });

  final SessionController sessionController;
  final BrandingController brandingController;
  final ErpFeatureController erpFeatureController;
  final ApiClient apiClient;
  final GoRouter router;

  @override
  State<AvelriGestaoApp> createState() => _AvelriGestaoAppState();
}

class _AvelriGestaoAppState extends State<AvelriGestaoApp>
    with WidgetsBindingObserver {
  Timer? _featureRefresh;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    _featureRefresh = Timer.periodic(
      const Duration(minutes: 1),
      (_) => _refreshFeatures(),
    );
  }

  void _refreshFeatures() {
    final session = widget.sessionController;
    final gymId = session.user?.gymId;
    if (session.status == SessionStatus.authenticated && gymId != null) {
      unawaited(widget.erpFeatureController.loadForGym(gymId, refresh: true));
    }
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) _refreshFeatures();
  }

  @override
  void dispose() {
    _featureRefresh?.cancel();
    WidgetsBinding.instance.removeObserver(this);
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        Provider<ApiClient>.value(value: widget.apiClient),
        ChangeNotifierProvider.value(value: widget.sessionController),
        ChangeNotifierProvider.value(value: widget.brandingController),
        ChangeNotifierProvider.value(value: widget.erpFeatureController),
      ],
      child: Consumer<BrandingController>(
        builder: (context, brandingController, child) {
          return MaterialApp.router(
            debugShowCheckedModeBanner: false,
            title: brandingController.branding.displayName,
            locale: const Locale('pt', 'BR'),
            supportedLocales: const [Locale('pt', 'BR')],
            localizationsDelegates: const [
              GlobalMaterialLocalizations.delegate,
              GlobalWidgetsLocalizations.delegate,
              GlobalCupertinoLocalizations.delegate,
            ],
            theme: AppTheme.fromBranding(brandingController.branding),
            routerConfig: widget.router,
          );
        },
      ),
    );
  }
}
