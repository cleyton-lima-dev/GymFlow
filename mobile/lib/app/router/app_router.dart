import 'package:go_router/go_router.dart';
import 'package:gymflow/app/router/role_destination_page.dart';
import 'package:gymflow/features/auth/presentation/login_page.dart';
import 'package:gymflow/app/router/session_loading_page.dart';
import 'package:gymflow/app/session/session_controller.dart';
import 'package:gymflow/app/session/app_role.dart';
import 'package:gymflow/app/session/session_status.dart';
import 'package:flutter/foundation.dart';
import 'package:gymflow/app/theme/branding_controller.dart';

class AppRouter {
  AppRouter(
      this._sessionController,
      this._brandingController,
      );

  final SessionController _sessionController;
  final BrandingController _brandingController;

  String _destinationForRole(AppRole role) {
    return switch (role) {
      AppRole.admin => '/admin',
      AppRole.professor => '/professor',
      AppRole.student => '/student',
    };
  }

  late final GoRouter router = GoRouter(
    initialLocation: '/bootstrap',
    refreshListenable: Listenable.merge([
      _sessionController,
      _brandingController,
    ]),
    redirect: (context, state) {
      final status = _sessionController.status;
      final location = state.matchedLocation;

      if (status == SessionStatus.unknown) {
        return location == '/bootstrap' ? null : '/bootstrap';
      }

      if (status == SessionStatus.unauthenticated) {
        return location == '/login' ? null : '/login';
      }

      final user = _sessionController.user;

      if (user == null) {
        return location == '/login' ? null : '/login';
      }

      if (!_brandingController.isLoadedForGym(user.gymId)) {
        return location == '/bootstrap' ? null : '/bootstrap';
      }

      final destination = _destinationForRole(user.role);

      return location == destination ? null : destination;
    },
    routes: [
      GoRoute(
        path: '/bootstrap',
        builder: (context, state) => const SessionLoadingPage(),
      ),
      GoRoute(
        path: '/login',
        builder: (context, state) => const LoginPage(),
      ),
      GoRoute(
        path: '/admin',
        builder: (context, state) =>
        const RoleDestinationPage(title: 'Admin'),
      ),
      GoRoute(
        path: '/professor',
        builder: (context, state) =>
        const RoleDestinationPage(title: 'Professor'),
      ),
      GoRoute(
        path: '/student',
        builder: (context, state) =>
        const RoleDestinationPage(title: 'Aluno'),
      ),
    ],
  );
}
