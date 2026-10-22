import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import 'package:avelri_gestao/app/app.dart';
import 'package:avelri_gestao/app/config/app_config.dart';
import 'package:avelri_gestao/app/router/app_router.dart';
import 'package:avelri_gestao/app/session/session_controller.dart';
import 'package:avelri_gestao/app/session/erp_feature_controller.dart';
import 'package:avelri_gestao/app/session/session_status.dart';
import 'package:avelri_gestao/app/theme/ac_power_gym_branding.dart';
import 'package:avelri_gestao/app/theme/branding_controller.dart';
import 'package:avelri_gestao/app/theme/default_branding.dart';
import 'package:avelri_gestao/app/theme/local_branding_repository.dart';
import 'package:avelri_gestao/app/theme/space_branding.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/core/storage/token_storage.dart';
import 'package:avelri_gestao/features/auth/data/auth_service.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();

  final config = AppConfig.fromEnvironment();

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

  const brandingRepository = LocalBrandingRepository({
    '11111111-1111-1111-1111-111111111111': acPowerGymBranding,
    '91f3fdb7-22b9-43ee-8a10-5e411c44cfbe': spaceBranding,
  });

  final brandingController = BrandingController(
    brandingRepository,
    defaultBranding: avelriDefaultBranding,
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
