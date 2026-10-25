import 'package:flutter/widgets.dart';
import 'package:go_router/go_router.dart';
import 'package:avelri_gestao/features/students/presentation/student_details_page.dart';
import 'package:avelri_gestao/app/router/management_shell_page.dart';
import 'package:avelri_gestao/app/router/session_loading_page.dart';
import 'package:avelri_gestao/app/router/session_popup_observer.dart';
import 'package:avelri_gestao/app/session/session_controller.dart';
import 'package:avelri_gestao/app/session/erp_feature_controller.dart';
import 'package:avelri_gestao/app/session/session_status.dart';
import 'package:avelri_gestao/app/theme/branding_controller.dart';
import 'package:avelri_gestao/features/auth/presentation/login_page.dart';
import 'package:avelri_gestao/features/dashboard/presentation/dashboard_page.dart';
import 'package:avelri_gestao/features/gym_settings/presentation/gym_settings_page.dart';
import 'package:avelri_gestao/features/students/presentation/students_page.dart';
import 'package:avelri_gestao/features/students/presentation/student_import_page.dart';
import 'package:avelri_gestao/features/students/presentation/migration_wizard_page.dart';
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
import 'package:avelri_gestao/features/access_control/presentation/access_control_page.dart';
import 'package:avelri_gestao/features/check_ins/presentation/check_ins_page.dart';
import 'package:avelri_gestao/features/reports/presentation/reports_page.dart';
import 'package:avelri_gestao/core/network/erp_access_policy.dart';

class AppRouter {
  AppRouter(
    this._sessionController,
    this._brandingController,
    this._erpFeatureController,
  ) {
    _lastSessionRevision = _sessionController.revision;
    _lastErpEnabled = _erpEnabled;
    _sessionController.addListener(_authorizationChanged);
    _erpFeatureController.addListener(_authorizationChanged);
  }

  final SessionController _sessionController;
  final BrandingController _brandingController;
  final ErpFeatureController _erpFeatureController;
  final _rootPopups = SessionPopupObserver();
  final _shellPopups = SessionPopupObserver();
  late int _lastSessionRevision;
  late bool _lastErpEnabled;
  bool _disposed = false;

  bool get _erpEnabled {
    final gymId = _sessionController.user?.gymId;
    return gymId != null && _erpFeatureController.isEnabledForGym(gymId);
  }

  void _authorizationChanged() {
    final currentErp = _erpEnabled;
    final shouldDismiss =
        _lastSessionRevision != _sessionController.revision ||
        (_lastErpEnabled && !currentErp);
    _lastSessionRevision = _sessionController.revision;
    _lastErpEnabled = currentErp;
    if (shouldDismiss) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (_disposed) return;
        _rootPopups.dismissPopups();
        _shellPopups.dismissPopups();
      });
    }
  }

  void dispose() {
    _disposed = true;
    _sessionController.removeListener(_authorizationChanged);
    _erpFeatureController.removeListener(_authorizationChanged);
    router.dispose();
  }

  late final GoRouter router = GoRouter(
    initialLocation: '/bootstrap',
    observers: [_rootPopups],
    refreshListenable: Listenable.merge([
      _sessionController,
      _brandingController,
      _erpFeatureController,
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

      final isErpRoute = ErpAccessPolicy.isErpRoute(location);
      if (isErpRoute && !_erpFeatureController.isEnabledForGym(user.gymId)) {
        return '/students';
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
        observers: [_shellPopups],
        builder: (context, state, child) {
          return KeyedSubtree(
            key: ValueKey(_sessionController.revision),
            child: ManagementShellPage(location: state.uri.path, child: child),
          );
        },
        routes: [
          GoRoute(
            path: '/dashboard',
            pageBuilder: (context, state) =>
                const NoTransitionPage(child: DashboardPage()),
          ),
          GoRoute(
            path: '/students',
            pageBuilder: (context, state) => NoTransitionPage(
              child: StudentsPage(
                assessmentFilter: state.uri.queryParameters['assessmentFilter'],
              ),
            ),
          ),
          GoRoute(
            path: '/students/new',
            pageBuilder: (context, state) =>
                const NoTransitionPage(child: CreateStudentPage()),
          ),
          GoRoute(
            path: '/students/import',
            pageBuilder: (context, state) =>
                const NoTransitionPage(child: StudentImportPage()),
          ),
          GoRoute(
            path: '/students/migration',
            pageBuilder: (context, state) =>
                const NoTransitionPage(child: MigrationWizardPage()),
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
            pageBuilder: (context, state) =>
                const NoTransitionPage(child: PlansPage()),
          ),
          GoRoute(
            path: '/plans/new',
            pageBuilder: (context, state) =>
                const NoTransitionPage(child: CreatePlanPage()),
          ),
          GoRoute(
            path: '/plans/edit',
            redirect: (context, state) =>
                state.extra is PlanSummary ? null : '/plans',
            pageBuilder: (context, state) {
              final plan = state.extra as PlanSummary;

              return NoTransitionPage(child: EditPlanPage(plan: plan));
            },
          ),
          GoRoute(
            path: '/enrollments',
            pageBuilder: (context, state) => NoTransitionPage(
              child: EnrollmentsPage(
                expiringWithinDays: int.tryParse(
                  state.uri.queryParameters['expiringWithinDays'] ?? '',
                ),
              ),
            ),
          ),
          GoRoute(
            path: '/enrollments/new',
            pageBuilder: (context, state) =>
                const NoTransitionPage(child: CreateEnrollmentPage()),
          ),
          GoRoute(
            path: '/finance',
            pageBuilder: (context, state) => NoTransitionPage(
              child: ChargesPage(
                overdueOnly: state.uri.queryParameters['status'] == 'overdue',
              ),
            ),
          ),
          GoRoute(
            path: '/access-control',
            pageBuilder: (context, state) =>
                const NoTransitionPage(child: AccessControlPage()),
          ),
                paidMonth: DateTime.tryParse(
                  state.uri.queryParameters['paidMonth'] ?? '',
                ),
          GoRoute(
            path: '/check-in',
            pageBuilder: (context, state) =>
                const NoTransitionPage(child: CheckInsPage()),
          ),
          GoRoute(
            path: '/reports',
            pageBuilder: (context, state) =>
                const NoTransitionPage(child: ReportsPage()),
          ),
          GoRoute(
            path: '/settings',
            pageBuilder: (context, state) =>
                const NoTransitionPage(child: GymSettingsPage()),
          ),
        ],
      ),
    ],
  );
}
