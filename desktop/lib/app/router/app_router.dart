import 'package:flutter/foundation.dart';
import 'package:go_router/go_router.dart';
import 'package:avelri_gestao/features/students/presentation/student_details_page.dart';
import 'package:avelri_gestao/app/router/management_shell_page.dart';
import 'package:avelri_gestao/app/router/session_loading_page.dart';
import 'package:avelri_gestao/app/session/session_controller.dart';
import 'package:avelri_gestao/app/session/session_status.dart';
import 'package:avelri_gestao/app/theme/branding_controller.dart';
import 'package:avelri_gestao/features/auth/presentation/login_page.dart';
import 'package:avelri_gestao/features/dashboard/presentation/dashboard_page.dart';
import 'package:avelri_gestao/features/dashboard/presentation/module_placeholder_page.dart';
import 'package:avelri_gestao/features/students/presentation/students_page.dart';
import 'package:avelri_gestao/features/students/presentation/create_student_page.dart';
import 'package:avelri_gestao/features/students/presentation/edit_student_page.dart';
import 'package:avelri_gestao/app/session/app_role.dart';
import 'package:avelri_gestao/features/plans/presentation/plans_page.dart';
import 'package:avelri_gestao/features/plans/presentation/create_plan_page.dart';
import 'package:avelri_gestao/features/plans/presentation/edit_plan_page.dart';
import 'package:avelri_gestao/features/plans/models/plan_summary.dart';
import 'package:avelri_gestao/features/enrollments/presentation/enrollments_page.dart';
import 'package:avelri_gestao/features/enrollments/presentation/create_enrollment_page.dart';
import 'package:avelri_gestao/features/charges/presentation/charges_page.dart';

class AppRouter {
  AppRouter(this._sessionController, this._brandingController);

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

      if (user.role != AppRole.admin) {
        _sessionController.invalidateSession();
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
      GoRoute(path: '/login', builder: (context, state) => const LoginPage()),
      ShellRoute(
        builder: (context, state, child) {
          return ManagementShellPage(location: state.uri.path, child: child);
        },
        routes: [
          GoRoute(
            path: '/dashboard',
            pageBuilder: (context, state) => const NoTransitionPage(
              child: DashboardPage(),
            ),
          ),
          GoRoute(
            path: '/students',
            pageBuilder: (context, state) => const NoTransitionPage(
              child: StudentsPage(),
            ),
          ),
          GoRoute(
            path: '/students/new',
            pageBuilder: (context, state) => const NoTransitionPage(
              child: CreateStudentPage(),
            ),
          ),
          GoRoute(
            path: '/students/:studentId',
            pageBuilder: (context, state) => NoTransitionPage(
              child: StudentDetailsPage(
                studentId: state.pathParameters['studentId']!,
              ),
            ),
          ),
          GoRoute(
            path: '/students/:studentId/edit',
            pageBuilder: (context, state) => NoTransitionPage(
              child: EditStudentPage(
                studentId: state.pathParameters['studentId']!,
              ),
            ),
          ),
          GoRoute(
            path: '/plans',
            pageBuilder: (context, state) => const NoTransitionPage(
              child: PlansPage(),
            ),
          ),
          GoRoute(
            path: '/plans/new',
            pageBuilder: (context, state) => const NoTransitionPage(
              child: CreatePlanPage(),
            ),
          ),
          GoRoute(
            path: '/plans/edit',
            pageBuilder: (context, state) {
              final plan = state.extra as PlanSummary;

              return NoTransitionPage(
                child: EditPlanPage(plan: plan),
              );
            },
          ),
          GoRoute(
            path: '/enrollments',
            pageBuilder: (context, state) => const NoTransitionPage(
              child: EnrollmentsPage(),
            ),
          ),
          GoRoute(
            path: '/enrollments/new',
            pageBuilder: (context, state) => const NoTransitionPage(
              child: CreateEnrollmentPage(),
            ),
          ),
          GoRoute(
            path: '/finance',
            pageBuilder: (context, state) => const NoTransitionPage(
              child: ChargesPage(),
            ),
          ),
          GoRoute(
            path: '/check-in',
            pageBuilder: (context, state) => const NoTransitionPage(
              child: ModulePlaceholderPage(title: 'Check-in'),
            ),
          ),
          GoRoute(
            path: '/reports',
            pageBuilder: (context, state) => const NoTransitionPage(
              child: ModulePlaceholderPage(title: 'Relatórios'),
            ),
          ),
          GoRoute(
            path: '/settings',
            pageBuilder: (context, state) => const NoTransitionPage(
              child: ModulePlaceholderPage(title: 'Configurações'),
            ),
          ),
        ],
      ),
    ],
  );
}
