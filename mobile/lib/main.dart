import 'dart:async';
import 'package:flutter/material.dart';
import 'package:gymflow/app/app.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:gymflow/app/router/app_router.dart';
import 'package:gymflow/app/session/session_controller.dart';
import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/core/storage/token_storage.dart';
import 'package:gymflow/features/auth/data/auth_service.dart';
import 'package:gymflow/app/theme/branding_controller.dart';
import 'package:gymflow/app/theme/default_branding.dart';
import 'package:gymflow/app/theme/local_branding_repository.dart';
import 'package:gymflow/app/session/session_status.dart';
import 'package:gymflow/app/theme/ac_power_gym_branding.dart';
import 'package:gymflow/app/theme/space_branding.dart';
import 'package:gymflow/app/config/app_config.dart';

void main() {
  final config = AppConfig.fromEnvironment();

  const secureStorage = FlutterSecureStorage();

  final tokenStorage = TokenStorage(secureStorage);
  final apiClient = ApiClient(
    baseUrl: config.apiBaseUrl,
  );
  final authService = AuthService(apiClient);

  final sessionController = SessionController(
    tokenStorage,
    authService,
    apiClient,
  );

  apiClient.setUnauthorizedHandler(sessionController.invalidateSession);

  const brandingRepository = LocalBrandingRepository({
    '11111111-1111-1111-1111-111111111111': acPowerGymBranding,
    '91f3fdb7-22b9-43ee-8a10-5e411c44cfbe': spaceBranding,
  });

  final brandingController = BrandingController(
    brandingRepository,
    defaultBranding: gymFlowDefaultBranding,
  );

  void syncBrandingWithSession() {
    final user = sessionController.user;

    if (sessionController.status == SessionStatus.authenticated &&
        user != null) {
      unawaited(
        brandingController.loadForGym(user.gymId),
      );
      return;
    }

    if (sessionController.status == SessionStatus.unauthenticated) {
      brandingController.reset();
    }
  }

  sessionController.addListener(syncBrandingWithSession);

  final appRouter = AppRouter(
    sessionController,
    brandingController,
  );

  runApp(
    GymFlowApp(
      sessionController: sessionController,
      brandingController: brandingController,
      apiClient: apiClient,
      router: appRouter.router,
    ),
  );
}
