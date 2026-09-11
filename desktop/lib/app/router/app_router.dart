import 'package:flutter/foundation.dart';
import 'package:go_router/go_router.dart';

import 'package:avelri_gestao/app/router/management_shell_page.dart';
import 'package:avelri_gestao/app/router/session_loading_page.dart';
import 'package:avelri_gestao/app/session/session_controller.dart';
import 'package:avelri_gestao/app/session/session_status.dart';
import 'package:avelri_gestao/app/theme/branding_controller.dart';
import 'package:avelri_gestao/features/auth/presentation/login_page.dart';
import 'package:avelri_gestao/features/dashboard/presentation/dashboard_page.dart';
import 'package:avelri_gestao/features/dashboard/presentation/module_placeholder_page.dart';

class AppRouter {
  AppRouter(
      this._sessionController,
      this._brandingController,
      );

  final SessionController _sessionController;
  final BrandingController _brandingController;

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
        return '/login';
      }

      if (!_brandingController.isLoadedForGym(user.gymId)) {
        return location == '/bootstrap' ? null : '/bootstrap';
      }

      if (location == '/bootstrap' || location == '/login') {
        return '/dashboard';
      }

      return null;
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
      ShellRoute(
        builder: (context, state, child) {
          return ManagementShellPage(
            location: state.uri.path,
            child: child,
          );
        },
        routes: [
          GoRoute(
            path: '/dashboard',
            builder: (context, state) => const DashboardPage(),
          ),
          GoRoute(
            path: '/students',
            builder: (context, state) =>
            const ModulePlaceholderPage(title: 'Alunos'),
          ),
          GoRoute(
            path: '/plans',
            builder: (context, state) =>
            const ModulePlaceholderPage(title: 'Planos'),
          ),
          GoRoute(
            path: '/finance',
            builder: (context, state) =>
            const ModulePlaceholderPage(title: 'Financeiro'),
          ),
          GoRoute(
            path: '/check-in',
            builder: (context, state) =>
            const ModulePlaceholderPage(title: 'Check-in'),
          ),
          GoRoute(
            path: '/reports',
            builder: (context, state) =>
            const ModulePlaceholderPage(title: 'Relatórios'),
          ),
          GoRoute(
            path: '/settings',
            builder: (context, state) =>
            const ModulePlaceholderPage(title: 'Configurações'),
          ),
        ],
      ),
    ],
  );
}
