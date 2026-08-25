import 'package:flutter/material.dart';
import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_bottom_navigation.dart';
import 'package:gymflow/features/physical_assessments/data/physical_assessments_service.dart';
import 'package:gymflow/features/physical_assessments/models/physical_assessment.dart';
import 'package:gymflow/features/physical_assessments/models/physical_assessment_history_item.dart';
import 'package:gymflow/features/physical_assessments/presentation/physical_assessment_history_view_model.dart';
import 'package:provider/provider.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_page_header.dart';

class PhysicalAssessmentHistoryPage extends StatelessWidget {
  const PhysicalAssessmentHistoryPage({
    required this.studentId,
    required this.studentName,
    required this.onNewAssessmentTap,
    required this.onAssessmentTap,
    super.key,
  });

  final String studentId;
  final String studentName;

  final Future<bool?> Function() onNewAssessmentTap;
  final ValueChanged<String> onAssessmentTap;


  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) =>
      PhysicalAssessmentHistoryViewModel(
        PhysicalAssessmentsService(
          context.read<ApiClient>(),
        ),
        studentId,
      )..loadInitial(),
      child: _PhysicalAssessmentHistoryView(
        studentName: studentName,
        onNewAssessmentTap: onNewAssessmentTap,
        onAssessmentTap: onAssessmentTap,
      ),
    );
  }
}

class _PhysicalAssessmentHistoryView extends StatelessWidget {
  const _PhysicalAssessmentHistoryView({
    required this.studentName,
    required this.onNewAssessmentTap,
    required this.onAssessmentTap,
  });

  final String studentName;
  final ValueChanged<String> onAssessmentTap;
  final Future<bool?> Function() onNewAssessmentTap;

  Future<void> _openNewAssessment(
      PhysicalAssessmentHistoryViewModel viewModel,
      ) async {
    final created = await onNewAssessmentTap();

    if (created == true) {
      await viewModel.loadInitial();
    }
  }

  @override
  Widget build(BuildContext context) {
    final viewModel =
    context.watch<PhysicalAssessmentHistoryViewModel>();

    return Scaffold(
      bottomNavigationBar:
      const ProfessorAdminBottomNavigation(
        currentItem: ProfessorAdminNavItem.students,
      ),
      body: SafeArea(
        child: RefreshIndicator(
          onRefresh: viewModel.refresh,
          child: CustomScrollView(
            physics:
            const AlwaysScrollableScrollPhysics(),
            slivers: [
              SliverToBoxAdapter(
                child: Padding(
                  padding: const EdgeInsets.fromLTRB(
                    20,
                    12,
                    20,
                    0,
                  ),
                  child: Column(
                    crossAxisAlignment:
                    CrossAxisAlignment.stretch,
                    children: [
                      const ProfessorAdminPageHeader(),

                      const SizedBox(height: 16),

                      _PageHeader(
                        studentName: studentName,
                        onNewAssessmentTap: () {
                          _openNewAssessment(viewModel);
                        },
                      ),

                      const SizedBox(height: 20),
                    ],
                  ),
                ),
              ),

              if (viewModel.isLoading)
                const SliverFillRemaining(
                  hasScrollBody: false,
                  child: Center(
                    child: CircularProgressIndicator(),
                  ),
                )
              else if (viewModel.errorMessage != null)
                SliverFillRemaining(
                  hasScrollBody: false,
                  child: _ErrorState(
                    message: viewModel.errorMessage!,
                    onRetry: viewModel.loadInitial,
                  ),
                )
              else if (viewModel.items.isEmpty)
                  SliverFillRemaining(
                    hasScrollBody: false,
                    child: _EmptyState(
                      onNewAssessmentTap: () {
                        _openNewAssessment(viewModel);
                      },
                    ),
                  )
                else ...[
                    SliverToBoxAdapter(
                      child: Padding(
                        padding: const EdgeInsets.symmetric(
                          horizontal: 20,
                        ),
                        child: _HistoryInfoCard(
                          totalCount: viewModel.totalCount,
                        ),
                      ),
                    ),

                    const SliverToBoxAdapter(
                      child: SizedBox(height: 16),
                    ),

                    SliverPadding(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 20,
                      ),
                      sliver: SliverList.separated(
                        itemCount: viewModel.items.length,
                        separatorBuilder: (_, _) =>
                        const SizedBox(height: 12),
                        itemBuilder: (context, index) {
                          final assessment =
                          viewModel.items[index];

                          return _AssessmentCard(
                            assessment: assessment,
                            onTap: () => onAssessmentTap(
                              assessment.id,
                            ),
                          );
                        },
                      ),
                    ),

                    const SliverToBoxAdapter(
                      child: SizedBox(height: 20),
                    ),

                    SliverToBoxAdapter(
                      child: Padding(
                        padding: const EdgeInsets.symmetric(
                          horizontal: 20,
                        ),
                        child: _CurrentStatusCard(
                          assessment: viewModel.latest,
                        ),
                      ),
                    ),

                    const SliverToBoxAdapter(
                      child: SizedBox(height: 20),
                    ),

                    SliverToBoxAdapter(
                      child: Padding(
                        padding: const EdgeInsets.symmetric(
                          horizontal: 20,
                        ),
                        child: _Pagination(
                          page: viewModel.page,
                          totalPages: viewModel.totalPages,
                          isChangingPage:
                          viewModel.isChangingPage,
                          hasPreviousPage:
                          viewModel.hasPreviousPage,
                          hasNextPage:
                          viewModel.hasNextPage,
                          onPrevious:
                          viewModel.previousPage,
                          onNext: viewModel.nextPage,
                        ),
                      ),
                    ),

                    const SliverToBoxAdapter(
                      child: SizedBox(height: 32),
                    ),
                  ],
            ],
          ),
        ),
      ),
    );
  }
}

class _PageHeader extends StatelessWidget {
  const _PageHeader({
    required this.studentName,
    required this.onNewAssessmentTap,
  });

  final String studentName;
  final VoidCallback onNewAssessmentTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          'Avaliação Física',
          style: theme.textTheme.headlineSmall?.copyWith(
            fontWeight: FontWeight.w800,
          ),
        ),
        const SizedBox(height: 4),
        Text(
          studentName,
          style: theme.textTheme.bodyLarge?.copyWith(
            color: colorScheme.onSurfaceVariant,
          ),
        ),
        const SizedBox(height: 16),
        SizedBox(
          width: double.infinity,
          child: FilledButton.icon(
            onPressed: onNewAssessmentTap,
            icon: const Icon(Icons.add_rounded),
            label: const Text('Nova avaliação'),
          ),
        ),
      ],
    );
  }
}

class _HistoryInfoCard extends StatelessWidget {
  const _HistoryInfoCard({
    required this.totalCount,
  });

  final int totalCount;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: colorScheme.primary.withAlpha(16),
        borderRadius: BorderRadius.circular(14),
      ),
      child: Row(
        children: [
          Icon(
            Icons.history_rounded,
            color: colorScheme.primary,
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment:
              CrossAxisAlignment.start,
              children: [
                Text(
                  'Histórico de avaliações',
                  style:
                  theme.textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.w800,
                  ),
                ),
                const SizedBox(height: 3),
                Text(
                  totalCount == 1
                      ? '1 avaliação registrada'
                      : '$totalCount avaliações registradas',
                  style:
                  theme.textTheme.bodySmall?.copyWith(
                    color:
                    colorScheme.onSurfaceVariant,
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

class _AssessmentCard extends StatelessWidget {
  const _AssessmentCard({
    required this.assessment,
    required this.onTap,
  });

  final PhysicalAssessmentHistoryItem assessment;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Material(
      color: colorScheme.surface,
      borderRadius: BorderRadius.circular(16),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(16),
        child: Container(
          padding: const EdgeInsets.all(18),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(16),
            border: Border.all(
              color:
              colorScheme.outlineVariant.withAlpha(120),
            ),
          ),
          child: Column(
            crossAxisAlignment:
            CrossAxisAlignment.stretch,
            children: [
              Row(
                children: [
                  Icon(
                    Icons.calendar_month_outlined,
                    color: colorScheme.primary,
                    size: 22,
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Text(
                      _formatDate(
                        assessment.assessmentDate,
                      ),
                      style:
                      theme.textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                  ),
                  Icon(
                    Icons.chevron_right_rounded,
                    color:
                    colorScheme.onSurfaceVariant,
                  ),
                ],
              ),

              const SizedBox(height: 16),
              const Divider(height: 1),
              const SizedBox(height: 16),

              Row(
                children: [
                  Expanded(
                    child: _Metric(
                      label: 'Peso',
                      value:
                      '${_formatNumber(assessment.weightKg)} kg',
                    ),
                  ),
                  Expanded(
                    child: _Metric(
                      label: 'Altura',
                      value:
                      '${_formatNumber(assessment.heightCm)} cm',
                    ),
                  ),
                  Expanded(
                    child: _Metric(
                      label: '% Gordura',
                      value:
                      assessment.bodyFatPercentage ==
                          null
                          ? '—'
                          : '${_formatNumber(assessment.bodyFatPercentage!)}%',
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _Metric extends StatelessWidget {
  const _Metric({
    required this.label,
    required this.value,
  });

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Column(
      crossAxisAlignment:
      CrossAxisAlignment.start,
      children: [
        Text(
          label,
          style: theme.textTheme.bodySmall?.copyWith(
            color: colorScheme.onSurfaceVariant,
          ),
        ),
        const SizedBox(height: 4),
        Text(
          value,
          style: theme.textTheme.titleSmall?.copyWith(
            fontWeight: FontWeight.w800,
          ),
        ),
      ],
    );
  }
}

class _CurrentStatusCard extends StatelessWidget {
  const _CurrentStatusCard({
    required this.assessment,
  });

  final PhysicalAssessment? assessment;

  @override
  Widget build(BuildContext context) {
    if (assessment == null) {
      return const SizedBox.shrink();
    }

    final theme = Theme.of(context);

    final due = assessment!.isReassessmentDue;

    final color = due
        ? const Color(0xFFF97316)
        : const Color(0xFF16A34A);

    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: color.withAlpha(18),
        borderRadius: BorderRadius.circular(14),
      ),
      child: Row(
        crossAxisAlignment:
        CrossAxisAlignment.start,
        children: [
          Icon(
            due
                ? Icons.warning_amber_rounded
                : Icons.check_circle_outline_rounded,
            color: color,
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment:
              CrossAxisAlignment.start,
              children: [
                Text(
                  due
                      ? 'Reavaliação recomendada'
                      : 'Avaliação em dia',
                  style:
                  theme.textTheme.titleSmall?.copyWith(
                    color: color,
                    fontWeight: FontWeight.w800,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  due
                      ? 'A próxima avaliação era recomendada '
                      'para ${_formatDate(assessment!.nextAssessmentDate)}.'
                      : 'Próxima avaliação recomendada para '
                      '${_formatDate(assessment!.nextAssessmentDate)}.',
                  style: theme.textTheme.bodySmall?.copyWith(
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

class _Pagination extends StatelessWidget {
  const _Pagination({
    required this.page,
    required this.totalPages,
    required this.isChangingPage,
    required this.hasPreviousPage,
    required this.hasNextPage,
    required this.onPrevious,
    required this.onNext,
  });

  final int page;
  final int totalPages;
  final bool isChangingPage;
  final bool hasPreviousPage;
  final bool hasNextPage;

  final VoidCallback onPrevious;
  final VoidCallback onNext;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Expanded(
          child: OutlinedButton(
            onPressed:
            hasPreviousPage && !isChangingPage
                ? onPrevious
                : null,
            child: const Text('Anterior'),
          ),
        ),
        const SizedBox(width: 14),
        Text(
          '$page de $totalPages',
          style: const TextStyle(
            fontWeight: FontWeight.w700,
          ),
        ),
        const SizedBox(width: 14),
        Expanded(
          child: OutlinedButton(
            onPressed: hasNextPage && !isChangingPage
                ? onNext
                : null,
            child: isChangingPage
                ? const SizedBox(
              width: 18,
              height: 18,
              child: CircularProgressIndicator(
                strokeWidth: 2,
              ),
            )
                : const Text('Próxima'),
          ),
        ),
      ],
    );
  }
}

class _EmptyState extends StatelessWidget {
  const _EmptyState({
    required this.onNewAssessmentTap,
  });

  final VoidCallback onNewAssessmentTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Padding(
      padding: const EdgeInsets.all(32),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Icon(
            Icons.assignment_outlined,
            color: colorScheme.primary,
            size: 48,
          ),
          const SizedBox(height: 16),
          Text(
            'Nenhuma avaliação registrada',
            textAlign: TextAlign.center,
            style: theme.textTheme.titleLarge?.copyWith(
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 8),
          Text(
            'Este aluno ainda não possui avaliações físicas.',
            textAlign: TextAlign.center,
            style: theme.textTheme.bodyMedium?.copyWith(
              color: colorScheme.onSurfaceVariant,
            ),
          ),
          const SizedBox(height: 20),
          FilledButton.icon(
            onPressed: onNewAssessmentTap,
            icon: const Icon(Icons.add_rounded),
            label: const Text('Nova avaliação'),
          ),
        ],
      ),
    );
  }
}

class _ErrorState extends StatelessWidget {
  const _ErrorState({
    required this.message,
    required this.onRetry,
  });

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Padding(
      padding: const EdgeInsets.all(32),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Icon(
            Icons.error_outline_rounded,
            color: colorScheme.error,
            size: 42,
          ),
          const SizedBox(height: 14),
          Text(
            message,
            textAlign: TextAlign.center,
          ),
          const SizedBox(height: 18),
          OutlinedButton(
            onPressed: onRetry,
            child: const Text('Tentar novamente'),
          ),
        ],
      ),
    );
  }
}

String _formatDate(DateTime date) {
  final day = date.day.toString().padLeft(2, '0');
  final month = date.month.toString().padLeft(2, '0');

  return '$day/$month/${date.year}';
}

String _formatNumber(double value) {
  final formatted = value % 1 == 0
      ? value.toStringAsFixed(0)
      : value.toStringAsFixed(1);

  return formatted.replaceAll('.', ',');
}
