import 'package:go_router/go_router.dart';
import 'package:gymflow/features/home/presentation/student_home_page.dart';
import 'package:gymflow/features/auth/presentation/login_page.dart';
import 'package:gymflow/app/router/session_loading_page.dart';
import 'package:gymflow/app/session/session_controller.dart';
import 'package:gymflow/app/session/app_role.dart';
import 'package:gymflow/app/session/session_status.dart';
import 'package:flutter/foundation.dart';
import 'package:gymflow/app/theme/branding_controller.dart';
import 'package:gymflow/features/home/presentation/professor_admin_home_page.dart';
import 'package:gymflow/features/students/presentation/students_page.dart';
import 'package:gymflow/features/students/presentation/student_details_page.dart';
import 'package:gymflow/features/students/presentation/edit_student_page.dart';
import 'package:gymflow/features/students/presentation/create_student_page.dart';
import 'package:gymflow/features/physical_assessments/presentation/physical_assessment_history_page.dart';
import 'package:gymflow/features/physical_assessments/presentation/physical_assessment_details_page.dart';
import 'package:gymflow/features/physical_assessments/presentation/create_physical_assessment_page.dart';
import 'package:gymflow/features/exercises/presentation/exercises_page.dart';
import 'package:gymflow/features/exercises/presentation/create_exercise_page.dart';
import 'package:gymflow/features/exercises/models/exercise_summary.dart';
import 'package:gymflow/features/exercises/presentation/edit_exercise_page.dart';
import 'package:gymflow/features/workout_templates/presentation/workout_templates_page.dart';
import 'package:gymflow/features/workout_templates/presentation/workout_template_details_page.dart';
import 'package:gymflow/features/workout_templates/presentation/create_workout_template_page.dart';
import 'package:gymflow/features/workout_templates/presentation/edit_workout_template_page.dart';
import 'package:gymflow/features/workouts/presentation/create_or_assign_workout_page.dart';
import 'package:gymflow/features/workouts/presentation/create_workout_page.dart';
import 'package:gymflow/features/workouts/presentation/select_workout_template_page.dart';
import 'package:gymflow/features/workout_templates/models/workout_template_details.dart';
import 'package:gymflow/features/workouts/models/workout_draft_from_template.dart';
import 'package:gymflow/features/workouts/presentation/edit_workout_page.dart';
import 'package:gymflow/features/workouts/models/workout_details.dart';
import 'package:gymflow/features/workouts/presentation/workout_history_page.dart';
import 'package:gymflow/features/home/presentation/more_page.dart';
import 'package:gymflow/features/workouts/presentation/student_workout_day_page.dart';
import 'package:gymflow/features/workouts/presentation/student_workout_history_page.dart';
import 'package:gymflow/features/home/presentation/student_more_page.dart';
import 'package:gymflow/features/students/presentation/student_personal_data_page.dart';
import 'package:gymflow/features/physical_assessments/presentation/student_physical_assessment_page.dart';
import 'package:gymflow/features/physical_assessments/presentation/student_physical_assessment_details_page.dart';
import 'package:gymflow/features/physical_assessments/presentation/student_physical_assessment_history_page.dart';
import 'package:gymflow/features/auth/presentation/create_professor_page.dart';
import 'package:gymflow/features/auth/presentation/professors_page.dart';

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

  bool _isAllowedLocationForRole(
      AppRole role,
      String location,
      ) {
    final basePath = _destinationForRole(role);

    return location == basePath ||
        location.startsWith('$basePath/');
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

      if (_isAllowedLocationForRole(user.role, location)) {
        return null;
      }

      return destination;

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
        builder: (context, state) => const ProfessorAdminHomePage(),
      ),
      GoRoute(
        path: '/professor',
        builder: (context, state) => const ProfessorAdminHomePage(),
      ),
      GoRoute(
        path: '/admin/more',
        builder: (context, state) => const MorePage(),
      ),
      GoRoute(
        path: '/admin/professors',
        builder: (context, state) => const ProfessorsPage(),
      ),
      GoRoute(
        path: '/admin/professors/new',
        builder: (context, state) => const CreateProfessorPage(),
      ),
      GoRoute(
        path: '/professor/more',
        builder: (context, state) => const MorePage(),
      ),
      GoRoute(
        path: '/admin/exercises/new',
        builder: (context, state) =>
        const CreateExercisePage(),
      ),
      GoRoute(
        path: '/admin/exercises/:exerciseId/edit',
        builder: (context, state) => EditExercisePage(
          exercise: state.extra as ExerciseSummary,
        ),
      ),
      GoRoute(
        path: '/admin/exercises',
        builder: (context, state) => ExercisesPage(
          onNewExerciseTap: () {
            return context.push<bool>(
              '/admin/exercises/new',
            );
          },
          onExerciseTap: (exercise) {
            return context.push<bool>(
              '/admin/exercises/${exercise.id}/edit',
              extra: exercise,
            );
          },
        ),
      ),
      GoRoute(
        path: '/professor/exercises/new',
        builder: (context, state) =>
        const CreateExercisePage(),
      ),
      GoRoute(
        path: '/professor/exercises/:exerciseId/edit',
        builder: (context, state) => EditExercisePage(
          exercise: state.extra as ExerciseSummary,
        ),
      ),
      GoRoute(
        path: '/professor/exercises',
        builder: (context, state) => ExercisesPage(
          onNewExerciseTap: () {
            return context.push<bool>(
              '/professor/exercises/new',
            );
          },
          onExerciseTap: (exercise) {
            return context.push<bool>(
              '/professor/exercises/${exercise.id}/edit',
              extra: exercise,
            );
          },
        ),
      ),
      GoRoute(
        path: '/admin/templates/new',
        builder: (context, state) =>
            CreateWorkoutTemplatePage(
              onCreated: () {
                context.pop(true);
              },
            ),
      ),
      GoRoute(
        path: '/admin/templates/:templateId/edit',
        builder: (context, state) {
          final templateId =
          state.pathParameters['templateId']!;

          return EditWorkoutTemplatePage(
            templateId: templateId,
            onUpdated: () {
              context.pop(true);
            },
          );
        },
      ),
      GoRoute(
        path: '/admin/templates/:templateId',
        builder: (context, state) {
          final templateId =
          state.pathParameters['templateId']!;

          return WorkoutTemplateDetailsPage(
            templateId: templateId,
            onEditTap: () async {
              final updated = await context.push<bool>(
                '/admin/templates/$templateId/edit',
              );

              return updated ?? false;
            },
          );
        },
      ),
      GoRoute(
        path: '/admin/templates',
        builder: (context, state) =>
            WorkoutTemplatesPage(
              onNewTemplateTap: () async {
                final created = await context.push<bool>(
                  '/admin/templates/new',
                );

                return created ?? false;
              },
              onTemplateTap: (template) async {
                await context.push(
                  '/admin/templates/${template.id}',
                );
              },
            ),
      ),
      GoRoute(
        path: '/professor/templates/new',
        builder: (context, state) =>
            CreateWorkoutTemplatePage(
              onCreated: () {
                context.pop(true);
              },
            ),
      ),
      GoRoute(
        path: '/professor/templates/:templateId/edit',
        builder: (context, state) {
          final templateId =
          state.pathParameters['templateId']!;

          return EditWorkoutTemplatePage(
            templateId: templateId,
            onUpdated: () {
              context.pop(true);
            },
          );
        },
      ),
      GoRoute(
        path: '/professor/templates/:templateId',
        builder: (context, state) {
          final templateId =
          state.pathParameters['templateId']!;

          return WorkoutTemplateDetailsPage(
            templateId: templateId,
            onEditTap: () async {
              final updated = await context.push<bool>(
                '/professor/templates/$templateId/edit',
              );

              return updated ?? false;
            },
          );
        },
      ),
      GoRoute(
        path: '/professor/templates',
        builder: (context, state) =>
            WorkoutTemplatesPage(
              onNewTemplateTap: () async {
                final created = await context.push<bool>(
                  '/professor/templates/new',
                );

                return created ?? false;
              },
              onTemplateTap: (template) async {
                await context.push(
                  '/professor/templates/${template.id}',
                );
              },
            ),
      ),
      GoRoute(
        path: '/student',
        builder: (context, state) => const StudentHomePage(),
      ),
      GoRoute(
        path: '/student/workout/day',
        builder: (context, state) {
          final arguments = state.extra;

          if (arguments is! StudentWorkoutDayArguments) {
            return const StudentHomePage();
          }

          return StudentWorkoutDayPage(
            arguments: arguments,
          );
        },
      ),
      GoRoute(
        path: '/student/history',
        builder: (context, state) =>
        const StudentWorkoutHistoryPage(),
      ),
      GoRoute(
        path: '/student/more',
        builder: (context, state) =>
        const StudentMorePage(),
      ),
      GoRoute(
        path: '/student/profile',
        builder: (context, state) =>
        const StudentPersonalDataPage(),
      ),
      GoRoute(
        path: '/student/physical-assessment',
        builder: (context, state) =>
        const StudentPhysicalAssessmentPage(),
      ),
      GoRoute(
        path: '/student/physical-assessment/history',
        builder: (context, state) =>
        const StudentPhysicalAssessmentHistoryPage(),
      ),

      GoRoute(
        path:
        '/student/physical-assessment/:assessmentId',
        builder: (context, state) {
          final assessmentId =
          state.pathParameters['assessmentId'];

          if (assessmentId == null ||
              assessmentId.isEmpty) {
            return const StudentPhysicalAssessmentPage();
          }

          return StudentPhysicalAssessmentDetailsPage(
            assessmentId: assessmentId,
          );
        },
      ),
      GoRoute(
        path: '/admin/students',
        builder: (context, state) => const StudentsPage(),
      ),
      GoRoute(
        path: '/admin/students/new',
        builder: (context, state) => const CreateStudentPage(),
      ),
      GoRoute(
        path: '/professor/students',
        builder: (context, state) => const StudentsPage(),
      ),
      GoRoute(
        path:
        '/admin/students/:studentId/physical-assessments/new',
        builder: (context, state) =>
            CreatePhysicalAssessmentPage(
              studentId: state.pathParameters['studentId']!,
              studentName:
              state.extra as String? ?? 'Aluno',
            ),
      ),
      GoRoute(
        path:
        '/admin/students/:studentId/physical-assessments/:assessmentId',
        builder: (context, state) =>
            PhysicalAssessmentDetailsPage(
              studentId: state.pathParameters['studentId']!,
              assessmentId:
              state.pathParameters['assessmentId']!,
              studentName:
              state.extra as String? ?? 'Aluno',
            ),
      ),
      GoRoute(
        path:
        '/professor/students/:studentId/physical-assessments/new',
        builder: (context, state) =>
            CreatePhysicalAssessmentPage(
              studentId: state.pathParameters['studentId']!,
              studentName:
              state.extra as String? ?? 'Aluno',
            ),
      ),
      GoRoute(
        path:
        '/professor/students/:studentId/physical-assessments/:assessmentId',
        builder: (context, state) =>
            PhysicalAssessmentDetailsPage(
              studentId: state.pathParameters['studentId']!,
              assessmentId:
              state.pathParameters['assessmentId']!,
              studentName:
              state.extra as String? ?? 'Aluno',
            ),
      ),
      GoRoute(
        path: '/admin/students/:studentId/physical-assessments',
        builder: (context, state) => PhysicalAssessmentHistoryPage(
          studentId: state.pathParameters['studentId']!,
          studentName: state.extra as String? ?? 'Aluno',
          onNewAssessmentTap: () {
            final studentId =
            state.pathParameters['studentId']!;

            return context.push<bool>(
              '/admin/students/$studentId/'
                  'physical-assessments/new',
              extra: state.extra,
            );
          },
          onAssessmentTap: (assessmentId) {
            final studentId =
            state.pathParameters['studentId']!;

            context.push(
              '/admin/students/$studentId/'
                  'physical-assessments/$assessmentId',
              extra: state.extra,
            );
          },
        ),
      ),
      GoRoute(
        path: '/professor/students/:studentId/physical-assessments',
        builder: (context, state) => PhysicalAssessmentHistoryPage(
          studentId: state.pathParameters['studentId']!,
          studentName: state.extra as String? ?? 'Aluno',
          onNewAssessmentTap: () {
            final studentId =
            state.pathParameters['studentId']!;

            return context.push<bool>(
              '/professor/students/$studentId/'
                  'physical-assessments/new',
              extra: state.extra,
            );
          },
          onAssessmentTap: (assessmentId) {
            final studentId =
            state.pathParameters['studentId']!;

            context.push(
              '/professor/students/$studentId/'
                  'physical-assessments/$assessmentId',
              extra: state.extra,
            );
          },
        ),
      ),
      GoRoute(
        path: '/admin/students/:studentId/workouts/select-template',
        builder: (context, state) {
          final studentId =
          state.pathParameters['studentId']!;

          return SelectWorkoutTemplatePage(
            studentId: studentId,
            studentName:
            state.extra as String? ?? 'Aluno',
          );
        },
      ),
      GoRoute(
        path: '/admin/students/:studentId/workouts/manual',
        builder: (context, state) {
          final studentId =
          state.pathParameters['studentId']!;

          final extra = state.extra;

          if (extra is Map<String, dynamic>) {
            final studentName =
                extra['studentName'] as String? ?? 'Aluno';

            final template =
            extra['template'] as WorkoutTemplateDetails?;

            return CreateWorkoutPage(
              studentId: studentId,
              studentName: studentName,
              sourceTemplateId: template?.id,
              initialName: template?.name ?? '',
              initialDescription:
              template?.description ?? '',
              initialDays:
              template?.toWorkoutDraftDays(),
              onCreated: () {
                context.pop(true);
              },
            );
          }

          return CreateWorkoutPage(
            studentId: studentId,
            studentName:
            extra as String? ?? 'Aluno',
            onCreated: () {
              context.pop(true);
            },
          );
        },
      ),
      GoRoute(
        path: '/admin/students/:studentId/workouts/create',
        builder: (context, state) {
          final studentId =
          state.pathParameters['studentId']!;

          final studentName =
              state.extra as String? ?? 'Aluno';

          return CreateOrAssignWorkoutPage(
            studentName: studentName,
            onUseTemplateTap: () async {
              final template =
              await context.push<WorkoutTemplateDetails>(
                '/admin/students/$studentId/'
                    'workouts/select-template',
                extra: studentName,
              );

              if (!context.mounted || template == null) {
                return false;
              }

              final created = await context.push<bool>(
                '/admin/students/$studentId/'
                    'workouts/manual',
                extra: {
                  'studentName': studentName,
                  'template': template,
                },
              );

              return created ?? false;
            },
            onCreateManualTap: () async {
              final created = await context.push<bool>(
                '/admin/students/$studentId/'
                    'workouts/manual',
                extra: studentName,
              );

              return created ?? false;
            },
          );
        },
      ),
      GoRoute(
        path:
        '/professor/students/:studentId/workouts/select-template',
        builder: (context, state) {
          final studentId =
          state.pathParameters['studentId']!;

          return SelectWorkoutTemplatePage(
            studentId: studentId,
            studentName:
            state.extra as String? ?? 'Aluno',
          );
        },
      ),
      GoRoute(
        path:
        '/professor/students/:studentId/workouts/manual',
        builder: (context, state) {
          final studentId =
          state.pathParameters['studentId']!;

          final extra = state.extra;

          if (extra is Map<String, dynamic>) {
            final studentName =
                extra['studentName'] as String? ?? 'Aluno';

            final template =
            extra['template']
            as WorkoutTemplateDetails?;

            return CreateWorkoutPage(
              studentId: studentId,
              studentName: studentName,
              sourceTemplateId: template?.id,
              initialName: template?.name ?? '',
              initialDescription:
              template?.description ?? '',
              initialDays:
              template?.toWorkoutDraftDays(),
              onCreated: () {
                context.pop(true);
              },
            );
          }

          return CreateWorkoutPage(
            studentId: studentId,
            studentName:
            extra as String? ?? 'Aluno',
            onCreated: () {
              context.pop(true);
            },
          );
        },
      ),
      GoRoute(
        path:
        '/professor/students/:studentId/workouts/create',
        builder: (context, state) {
          final studentId =
          state.pathParameters['studentId']!;

          final studentName =
              state.extra as String? ?? 'Aluno';

          return CreateOrAssignWorkoutPage(
            studentName: studentName,
            onUseTemplateTap: () async {
              final template =
              await context
                  .push<WorkoutTemplateDetails>(
                '/professor/students/$studentId/'
                    'workouts/select-template',
                extra: studentName,
              );

              if (!context.mounted ||
                  template == null) {
                return false;
              }

              final created = await context.push<bool>(
                '/professor/students/$studentId/'
                    'workouts/manual',
                extra: {
                  'studentName': studentName,
                  'template': template,
                },
              );

              return created ?? false;
            },
            onCreateManualTap: () async {
              final created = await context.push<bool>(
                '/professor/students/$studentId/'
                    'workouts/manual',
                extra: studentName,
              );

              return created ?? false;
            },
          );
        },
      ),GoRoute(
        path: '/admin/students/:studentId/workouts/edit',
        builder: (context, state) {
          final extra = state.extra;

          if (extra is! Map<String, dynamic>) {
            throw StateError(
              'Dados do treino não informados.',
            );
          }

          final workout =
          extra['workout'] as WorkoutDetails;

          final studentName =
              extra['studentName'] as String? ?? 'Aluno';

          return EditWorkoutPage(
            workout: workout,
            studentName: studentName,
            onUpdated: () {
              context.pop(true);
            },
          );
        },
      ),
      GoRoute(
        path: '/admin/students/:studentId/workouts/history',
        builder: (context, state) {
          final studentId =
          state.pathParameters['studentId']!;

          return WorkoutHistoryPage(
            studentId: studentId,
            studentName:
            state.extra as String? ?? 'Aluno',
          );
        },
      ),
      GoRoute(
        path: '/admin/students/:studentId',
        builder: (context, state) => StudentDetailsPage(
          studentId: state.pathParameters['studentId']!,
        ),
      ),
      GoRoute(
        path: '/admin/students/:studentId/edit',
        builder: (context, state) => EditStudentPage(
          studentId: state.pathParameters['studentId']!,
        ),
      ),
      GoRoute(
        path:
        '/professor/students/:studentId/workouts/select-template',
        builder: (context, state) {
          final studentId =
          state.pathParameters['studentId']!;

          return SelectWorkoutTemplatePage(
            studentId: studentId,
            studentName:
            state.extra as String? ?? 'Aluno',
          );
        },
      ),
      GoRoute(
        path:
        '/professor/students/:studentId/workouts/manual',
        builder: (context, state) {
          final studentId =
          state.pathParameters['studentId']!;

          return CreateWorkoutPage(
            studentId: studentId,
            studentName:
            state.extra as String? ?? 'Aluno',
            onCreated: () {
              context.pop(true);
            },
          );
        },
      ),
      GoRoute(
        path:
        '/professor/students/:studentId/workouts/create',
        builder: (context, state) {
          final studentId =
          state.pathParameters['studentId']!;

          final studentName =
              state.extra as String? ?? 'Aluno';

          return CreateOrAssignWorkoutPage(
            studentName: studentName,
            onUseTemplateTap: () async {
              final created = await context.push<bool>(
                '/professor/students/$studentId/'
                    'workouts/select-template',
                extra: studentName,
              );

              return created ?? false;
            },
            onCreateManualTap: () async {
              final created = await context.push<bool>(
                '/professor/students/$studentId/'
                    'workouts/manual',
                extra: studentName,
              );

              return created ?? false;
            },
          );
        },
      ),
      GoRoute(
        path:
        '/professor/students/:studentId/workouts/edit',
        builder: (context, state) {
          final extra = state.extra;

          if (extra is! Map<String, dynamic>) {
            throw StateError(
              'Dados do treino não informados.',
            );
          }

          final workout =
          extra['workout'] as WorkoutDetails;

          final studentName =
              extra['studentName'] as String? ?? 'Aluno';

          return EditWorkoutPage(
            workout: workout,
            studentName: studentName,
            onUpdated: () {
              context.pop(true);
            },
          );
        },
      ),
      GoRoute(
        path:
        '/professor/students/:studentId/workouts/history',
        builder: (context, state) {
          final studentId =
          state.pathParameters['studentId']!;

          return WorkoutHistoryPage(
            studentId: studentId,
            studentName:
            state.extra as String? ?? 'Aluno',
          );
        },
      ),
      GoRoute(
        path: '/professor/students/:studentId',
        builder: (context, state) => StudentDetailsPage(
          studentId: state.pathParameters['studentId']!,
        ),
      ),
    ],
  );
}
