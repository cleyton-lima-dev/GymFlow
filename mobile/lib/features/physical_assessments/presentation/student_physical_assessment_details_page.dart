import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_brand_header.dart';
import 'package:gymflow/features/home/presentation/widgets/student_bottom_navigation.dart';
import 'package:gymflow/features/physical_assessments/data/physical_assessments_service.dart';
import 'package:gymflow/features/physical_assessments/presentation/physical_assessment_details_view_model.dart';
import 'package:gymflow/features/physical_assessments/presentation/student_physical_assessment_page.dart';
import 'package:provider/provider.dart';

class StudentPhysicalAssessmentDetailsPage extends StatelessWidget {
  const StudentPhysicalAssessmentDetailsPage({
    required this.assessmentId,
    super.key,
  });

  final String assessmentId;

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) => PhysicalAssessmentDetailsViewModel.forCurrentUser(
        PhysicalAssessmentsService(context.read<ApiClient>()),
        assessmentId,
      )..load(),
      child: const _StudentPhysicalAssessmentDetailsView(),
    );
  }
}

class _StudentPhysicalAssessmentDetailsView extends StatelessWidget {
  const _StudentPhysicalAssessmentDetailsView();

  @override
  Widget build(BuildContext context) {
    final viewModel = context.watch<PhysicalAssessmentDetailsViewModel>();

    return Scaffold(
      body: SafeArea(
        bottom: false,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(24, 12, 24, 32),
          children: [
            Center(
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 680),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    SizedBox(
                      height: 82,
                      child: Stack(
                        alignment: Alignment.center,
                        children: [
                          const ProfessorAdminBrandHeader(height: 72),
                          Align(
                            alignment: Alignment.centerLeft,
                            child: TextButton.icon(
                              onPressed: () {
                                context.pop();
                              },
                              icon: const Icon(Icons.chevron_left_rounded),
                              label: const Text('Voltar'),
                              style: TextButton.styleFrom(
                                padding: EdgeInsets.zero,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),

                    const SizedBox(height: 18),

                    Text(
                      'Detalhes da avaliação',
                      style: Theme.of(context).textTheme.headlineMedium
                          ?.copyWith(fontWeight: FontWeight.w800),
                    ),

                    const SizedBox(height: 24),

                    if (viewModel.isLoading)
                      const Center(child: CircularProgressIndicator())
                    else if (viewModel.errorMessage != null)
                      _DetailsError(
                        message: viewModel.errorMessage!,
                        onRetry: viewModel.load,
                      )
                    else if (viewModel.assessment != null)
                      StudentPhysicalAssessmentContent(
                        assessment: viewModel.assessment!,
                        historical: !viewModel.isLatest,
                      ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
      bottomNavigationBar: StudentBottomNavigation(
        currentItem: StudentNavItem.more,
        onHomeTap: () {
          context.go('/student');
        },
        onHistoryTap: () {
          context.go('/student/history');
        },
        onMoreTap: () {
          context.go('/student/more');
        },
      ),
    );
  }
}

class _DetailsError extends StatelessWidget {
  const _DetailsError({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      padding: const EdgeInsets.all(24),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: colorScheme.error.withAlpha(80)),
      ),
      child: Column(
        children: [
          Text(message, textAlign: TextAlign.center),
          const SizedBox(height: 16),
          OutlinedButton.icon(
            onPressed: onRetry,
            icon: const Icon(Icons.refresh_rounded),
            label: const Text('Tentar novamente'),
          ),
        ],
      ),
    );
  }
}
