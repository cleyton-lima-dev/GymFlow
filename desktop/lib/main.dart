import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import 'package:avelri_gestao/app/app.dart';
import 'package:avelri_gestao/app/config/app_config.dart';
import 'package:avelri_gestao/app/router/app_router.dart';
import 'package:avelri_gestao/app/session/session_controller.dart';
import 'package:avelri_gestao/app/session/erp_feature_controller.dart';
import 'package:avelri_gestao/app/session/session_status.dart';
import 'package:avelri_gestao/app/theme/branding_controller.dart';
import 'package:avelri_gestao/app/theme/branding_preview.dart';
import 'package:avelri_gestao/app/theme/default_branding.dart';
import 'package:avelri_gestao/app/theme/local_branding_repository.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/core/storage/token_storage.dart';
import 'package:avelri_gestao/features/auth/data/auth_service.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();

  final config = AppConfig.fromEnvironment();
  const requestedBrandingPreview = String.fromEnvironment('BRANDING_PREVIEW');
  const homologationAcGymId = String.fromEnvironment(
    'HOMOLOGATION_AC_BRANDING_GYM_ID',
  );
  final brandingPreview = BrandingPreview.resolve(
    requestedBrandingPreview,
    isDevelopment: config.isDevelopment,
    isDebug: kDebugMode,
  );

  const secureStorage = FlutterSecureStorage(
    wOptions: WindowsOptions(useBackwardCompatibility: true),
  );

  final tokenStorage = TokenStorage(secureStorage);

  final apiClient = ApiClient(baseUrl: config.apiBaseUrl);

  final authService = AuthService(apiClient);

  final sessionController = SessionController(
    tokenStorage,
    authService,
    apiClient,
  );
  final erpFeatureController = ErpFeatureController(apiClient);
  apiClient.setErpAccessDeniedHandler(erpFeatureController.denyCurrentSession);

  apiClient.setUnauthorizedHandler(sessionController.invalidateSession);

  final brandingRepository = LocalBrandingRepository.forApplication(
    allowHomologationBinding: config.isDevelopment,
    acPowerHomologationGymId: homologationAcGymId,
  );

  final brandingController = BrandingController(
    brandingRepository,
    defaultBranding: avelriDefaultBranding,
    preview: brandingPreview,
  );

  void syncBrandingWithSession() {
    final user = sessionController.user;

    if (sessionController.status == SessionStatus.authenticated &&
        user != null) {
      unawaited(brandingController.loadForGym(user.gymId));
      unawaited(erpFeatureController.loadForGym(user.gymId));
      return;
    }

    if (sessionController.status == SessionStatus.unauthenticated) {
      brandingController.reset();
      erpFeatureController.reset();
    }
  }

  sessionController.addListener(syncBrandingWithSession);

  final appRouter = AppRouter(
    sessionController,
    brandingController,
    erpFeatureController,
  );

  runApp(
    AvelriGestaoApp(
      sessionController: sessionController,
      brandingController: brandingController,
      erpFeatureController: erpFeatureController,
      apiClient: apiClient,
      router: appRouter.router,
    ),
  );
}
