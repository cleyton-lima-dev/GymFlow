import 'package:flutter/material.dart';
import 'package:gymflow/app/app.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:gymflow/app/router/app_router.dart';
import 'package:gymflow/app/session/session_controller.dart';
import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/core/storage/token_storage.dart';
import 'package:gymflow/features/auth/data/auth_service.dart';

void main() {
  const apiBaseUrl = String.fromEnvironment('API_BASE_URL');

  if (apiBaseUrl.isEmpty) {
    throw StateError('API_BASE_URL was not provided.');
  }

  const secureStorage = FlutterSecureStorage();

  final tokenStorage = TokenStorage(secureStorage);
  final apiClient = ApiClient(baseUrl: apiBaseUrl);
  final authService = AuthService(apiClient);

  final sessionController = SessionController(
    tokenStorage,
    authService,
  );

  final appRouter = AppRouter(sessionController);

  runApp(
    GymFlowApp(
      sessionController: sessionController,
      router: appRouter.router,
    ),
  );
}
