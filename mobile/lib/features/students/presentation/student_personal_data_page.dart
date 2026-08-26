import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:gymflow/app/theme/branding_controller.dart';
import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_brand_header.dart';
import 'package:gymflow/features/home/presentation/widgets/student_bottom_navigation.dart';
import 'package:gymflow/features/students/data/students_service.dart';
import 'package:gymflow/features/students/models/student_summary.dart';
import 'package:gymflow/features/students/presentation/student_profile_view_model.dart';
import 'package:provider/provider.dart';
import 'package:gymflow/features/physical_assessments/data/physical_assessments_service.dart';
import 'package:gymflow/features/physical_assessments/presentation/physical_assessment_summary_view_model.dart';
import 'package:gymflow/features/physical_assessments/presentation/widgets/student_physical_assessment_status_card.dart';

class StudentPersonalDataPage extends StatelessWidget {
  const StudentPersonalDataPage({super.key});

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        ChangeNotifierProvider(
          create: (context) => StudentProfileViewModel(
            StudentsService(context.read<ApiClient>()),
          )..load(),
        ),
        ChangeNotifierProvider(
          create: (context) =>
              PhysicalAssessmentSummaryViewModel.forCurrentUser(
                PhysicalAssessmentsService(context.read<ApiClient>()),
              )..load(),
        ),
      ],
      child: const _StudentPersonalDataView(),
    );
  }
}

class _StudentPersonalDataView extends StatelessWidget {
  const _StudentPersonalDataView();

  @override
  Widget build(BuildContext context) {
    final viewModel = context.watch<StudentProfileViewModel>();

    final gymName = context.watch<BrandingController>().branding.displayName;

    return Scaffold(
      body: SafeArea(
        bottom: false,
        child: RefreshIndicator(
          onRefresh: viewModel.refresh,
          child: ListView(
            physics: const AlwaysScrollableScrollPhysics(),
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
                        'Meus dados',
                        style: Theme.of(context).textTheme.headlineMedium
                            ?.copyWith(fontWeight: FontWeight.w800),
                      ),

                      const SizedBox(height: 6),

                      Text(
                        'Visualize suas informações pessoais.',
                        style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                          color: Theme.of(context).colorScheme.onSurfaceVariant,
                        ),
                      ),

                      const SizedBox(height: 26),

                      if (viewModel.isLoading && !viewModel.hasLoaded)
                        const _LoadingState()
                      else if (viewModel.student != null)
                        _StudentDataContent(
                          student: viewModel.student!,
                          gymName: gymName,
                        )
                      else
                        _ErrorState(
                          message:
                              viewModel.errorMessage ??
                              'Não foi possível carregar seus dados.',
                          onRetry: viewModel.load,
                        ),
                    ],
                  ),
                ),
              ),
            ],
          ),
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

class _StudentDataContent extends StatelessWidget {
  const _StudentDataContent({required this.student, required this.gymName});

  final StudentSummary student;
  final String gymName;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        _ProfileHeader(student: student),

        const SizedBox(height: 18),

        const StudentPhysicalAssessmentStatusCard(),

        const SizedBox(height: 28),

        Row(
          children: [
            Icon(
              Icons.person_outline_rounded,
              color: Theme.of(context).colorScheme.primary,
            ),
            const SizedBox(width: 8),
            Text(
              'Informações pessoais',
              style: Theme.of(context).textTheme.titleLarge
                  ?.copyWith(fontWeight: FontWeight.w800),
            ),
          ],
        ),

        const SizedBox(height: 14),

        _DataRow(
          icon: Icons.person_outline_rounded,
          label: 'Nome completo',
          value: student.name,
        ),

        _DataRow(
          icon: Icons.email_outlined,
          label: 'E-mail',
          value: student.email,
        ),

        _DataRow(
          icon: Icons.phone_outlined,
          label: 'Telefone',
          value: _optionalText(student.phone),
        ),

        _DataRow(
          icon: Icons.calendar_month_outlined,
          label: 'Data de nascimento',
          value: student.birthDate == null
              ? '—'
              : _formatDate(student.birthDate!),
        ),

        _DataRow(
          icon: Icons.apartment_rounded,
          label: 'Academia',
          value: gymName,
        ),

        _DataRow(
          icon: Icons.event_available_outlined,
          label: 'Cadastrado em',
          value: _formatDate(student.createdAt),
        ),

        const SizedBox(height: 22),

        const _InfoCard(),
      ],
    );
  }

  static String _optionalText(String? value) {
    final normalized = value?.trim();

    if (normalized == null || normalized.isEmpty) {
      return '—';
    }

    return normalized;
  }

  static String _formatDate(DateTime value) {
    final local = value.toLocal();

    return '${local.day.toString().padLeft(2, '0')}/'
        '${local.month.toString().padLeft(2, '0')}/'
        '${local.year}';
  }
}

class _ProfileHeader extends StatelessWidget {
  const _ProfileHeader({required this.student});

  final StudentSummary student;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: colorScheme.outlineVariant.withAlpha(120)),
      ),
      child: Row(
        children: [
          Container(
            width: 62,
            height: 62,
            decoration: BoxDecoration(
              color: colorScheme.primary.withAlpha(18),
              shape: BoxShape.circle,
            ),
            child: Icon(
              Icons.person_rounded,
              size: 38,
              color: colorScheme.primary,
            ),
          ),

          const SizedBox(width: 16),

          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Flexible(
                      child: Text(
                        student.name,
                        style: Theme.of(context).textTheme.titleLarge
                            ?.copyWith(fontWeight: FontWeight.w800),
                      ),
                    ),

                    const SizedBox(width: 8),

                    Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 9,
                        vertical: 4,
                      ),
                      decoration: BoxDecoration(
                        color: colorScheme.primary.withAlpha(18),
                        borderRadius: BorderRadius.circular(10),
                      ),
                      child: Text(
                        'Aluno',
                        style: Theme.of(context).textTheme.labelMedium
                            ?.copyWith(
                              color: colorScheme.primary,
                              fontWeight: FontWeight.w700,
                            ),
                      ),
                    ),
                  ],
                ),

                const SizedBox(height: 8),

                Row(
                  children: [
                    Icon(
                      Icons.email_outlined,
                      size: 17,
                      color: colorScheme.onSurfaceVariant,
                    ),
                    const SizedBox(width: 6),
                    Expanded(
                      child: Text(
                        student.email,
                        overflow: TextOverflow.ellipsis,
                        style: Theme.of(context).textTheme.bodyMedium
                            ?.copyWith(color: colorScheme.onSurfaceVariant),
                      ),
                    ),
                  ],
                ),

                const SizedBox(height: 5),

                Row(
                  children: [
                    Icon(
                      Icons.calendar_today_outlined,
                      size: 17,
                      color: colorScheme.onSurfaceVariant,
                    ),
                    const SizedBox(width: 6),
                    Text(
                      'Cadastrado em '
                      '${_formatDate(student.createdAt)}',
                      style: Theme.of(context).textTheme.bodySmall
                          ?.copyWith(color: colorScheme.onSurfaceVariant),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  String _formatDate(DateTime value) {
    final local = value.toLocal();

    return '${local.day.toString().padLeft(2, '0')}/'
        '${local.month.toString().padLeft(2, '0')}/'
        '${local.year}';
  }
}

class _DataRow extends StatelessWidget {
  const _DataRow({
    required this.icon,
    required this.label,
    required this.value,
  });

  final IconData icon;
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      margin: const EdgeInsets.only(bottom: 10),
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(15),
        border: Border.all(color: colorScheme.outlineVariant.withAlpha(100)),
      ),
      child: Row(
        children: [
          Container(
            width: 40,
            height: 40,
            decoration: BoxDecoration(
              color: colorScheme.primary.withAlpha(18),
              borderRadius: BorderRadius.circular(12),
            ),
            child: Icon(icon, color: colorScheme.primary, size: 21),
          ),

          const SizedBox(width: 14),

          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  label,
                  style: Theme.of(context).textTheme.bodySmall
                      ?.copyWith(color: colorScheme.onSurfaceVariant),
                ),

                const SizedBox(height: 3),

                Text(
                  value,
                  style: Theme.of(context).textTheme.bodyLarge
                      ?.copyWith(fontWeight: FontWeight.w600),
                ),
              ],
            ),
          ),

          Text(
            '—',
            style: TextStyle(
              color: colorScheme.onSurfaceVariant.withAlpha(120),
            ),
          ),
        ],
      ),
    );
  }
}

class _InfoCard extends StatelessWidget {
  const _InfoCard();

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: colorScheme.outlineVariant.withAlpha(100)),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(Icons.info_outline_rounded, color: colorScheme.primary),

          const SizedBox(width: 12),

          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Dados cadastrais',
                  style: Theme.of(context).textTheme.titleSmall
                      ?.copyWith(fontWeight: FontWeight.w800),
                ),

                const SizedBox(height: 4),

                Text(
                  'Caso alguma informação esteja incorreta, '
                  'entre em contato com a administração da academia.',
                  style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                    color: colorScheme.onSurfaceVariant,
                    height: 1.4,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _LoadingState extends StatelessWidget {
  const _LoadingState();

  @override
  Widget build(BuildContext context) {
    return const Padding(
      padding: EdgeInsets.symmetric(vertical: 80),
      child: Center(child: CircularProgressIndicator()),
    );
  }
}

class _ErrorState extends StatelessWidget {
  const _ErrorState({required this.message, required this.onRetry});

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
          Icon(Icons.error_outline_rounded, size: 40, color: colorScheme.error),

          const SizedBox(height: 14),

          Text(message, textAlign: TextAlign.center),

          const SizedBox(height: 18),

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
