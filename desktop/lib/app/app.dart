import 'dart:async';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'package:avelri_gestao/app/session/session_controller.dart';
import 'package:avelri_gestao/app/session/erp_feature_controller.dart';
import 'package:avelri_gestao/app/session/session_status.dart';
import 'package:avelri_gestao/app/theme/app_theme.dart';
import 'package:avelri_gestao/app/theme/branding_controller.dart';
import 'package:avelri_gestao/app/theme/default_branding.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/updates/presentation/update_check_scheduler.dart';
import 'package:avelri_gestao/features/updates/presentation/update_controller.dart';

class AvelriGestaoApp extends StatefulWidget {
  const AvelriGestaoApp({
    required this.sessionController,
    required this.brandingController,
    required this.erpFeatureController,
    required this.apiClient,
    required this.router,
    this.updateController,
    super.key,
  });

  final SessionController sessionController;
  final BrandingController brandingController;
  final ErpFeatureController erpFeatureController;
  final ApiClient apiClient;
  final GoRouter router;
  final UpdateController? updateController;

  @override
  State<AvelriGestaoApp> createState() => _AvelriGestaoAppState();
}

class _AvelriGestaoAppState extends State<AvelriGestaoApp>
    with WidgetsBindingObserver {
  Timer? _featureRefresh;
  late final UpdateController _updateController;
  late final UpdateCheckScheduler _updateCheckScheduler;
  late final bool _ownsUpdateController;

  @override
  void initState() {
    super.initState();
    _ownsUpdateController = widget.updateController == null;
    _updateController =
        widget.updateController ??
        UpdateController.forDesktop(
          requestDesktopShutdown: () async {
            await ServicesBinding.instance.exitApplication(
              ui.AppExitType.required,
            );
          },
        );
    _updateCheckScheduler = UpdateCheckScheduler(controller: _updateController);
    widget.sessionController.addListener(_sessionChanged);
    WidgetsBinding.instance.addObserver(this);
    _featureRefresh = Timer.periodic(
      const Duration(minutes: 1),
      (_) => _refreshFeatures(),
    );
    _sessionChanged();
  }

  void _sessionChanged() {
    unawaited(
      _updateCheckScheduler.handleSessionStatus(
        widget.sessionController.status,
      ),
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
    if (state == AppLifecycleState.resumed) {
      _refreshFeatures();
      unawaited(_updateCheckScheduler.onAppResumed());
    }
  }

  @override
  void dispose() {
    _featureRefresh?.cancel();
    widget.sessionController.removeListener(_sessionChanged);
    _updateCheckScheduler.dispose();
    if (_ownsUpdateController) _updateController.dispose();
    WidgetsBinding.instance.removeObserver(this);
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        Provider<ApiClient>.value(value: widget.apiClient),
        ChangeNotifierProvider<UpdateController>.value(
          value: _updateController,
        ),
        ChangeNotifierProvider.value(value: widget.sessionController),
        ChangeNotifierProvider.value(value: widget.brandingController),
        ChangeNotifierProvider.value(value: widget.erpFeatureController),
      ],
      child: Consumer2<BrandingController, SessionController>(
        builder: (context, brandingController, sessionController, child) {
          return MaterialApp.router(
            debugShowCheckedModeBanner: false,
            title: sessionController.status == SessionStatus.authenticated
                ? brandingController.branding.displayName
                : avelriDefaultBranding.displayName,
            locale: const Locale('pt', 'BR'),
            supportedLocales: const [Locale('pt', 'BR')],
            localizationsDelegates: const [
              GlobalMaterialLocalizations.delegate,
              GlobalWidgetsLocalizations.delegate,
              GlobalCupertinoLocalizations.delegate,
            ],
            theme: AppTheme.fromBranding(brandingController.branding),
            routerConfig: widget.router,
            builder: (context, child) {
              final previewLabel = brandingController.previewLabel;
              if (previewLabel == null ||
                  sessionController.status != SessionStatus.authenticated) {
                return child ?? const SizedBox.shrink();
              }

              final theme = Theme.of(context);
              return Column(
                children: [
                  BrandingPreviewNotice(
                    label: previewLabel,
                    backgroundColor: theme.colorScheme.surfaceContainerHighest,
                    foregroundColor: theme.colorScheme.onSurface,
                  ),
                  Expanded(child: child ?? const SizedBox.shrink()),
                ],
              );
            },
          );
        },
      ),
    );
  }
}

class BrandingPreviewNotice extends StatelessWidget {
  const BrandingPreviewNotice({
    required this.label,
    required this.backgroundColor,
    required this.foregroundColor,
    super.key,
  });

  final String label;
  final Color backgroundColor;
  final Color foregroundColor;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 30,
      width: double.infinity,
      child: ColoredBox(
        color: backgroundColor,
        child: Center(
          child: Text(
            'Pré-visualização local do tema: $label',
            style: TextStyle(
              color: foregroundColor,
              fontSize: 11,
              fontWeight: FontWeight.w600,
            ),
          ),
        ),
      ),
    );
  }
}
