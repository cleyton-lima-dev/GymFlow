import 'package:flutter/material.dart';
import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_bottom_navigation.dart';
import 'package:gymflow/features/physical_assessments/data/physical_assessments_service.dart';
import 'package:gymflow/features/physical_assessments/models/physical_assessment.dart';
import 'package:gymflow/features/physical_assessments/presentation/physical_assessment_details_view_model.dart';
import 'package:provider/provider.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_page_header.dart';

class PhysicalAssessmentDetailsPage extends StatelessWidget {
  const PhysicalAssessmentDetailsPage({
    required this.studentId,
    required this.assessmentId,
    required this.studentName,
    super.key,
  });

  final String studentId;
  final String assessmentId;
  final String studentName;

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) => PhysicalAssessmentDetailsViewModel(
        PhysicalAssessmentsService(
          context.read<ApiClient>(),
        ),
        studentId,
        assessmentId,
      )..load(),
      child: _PhysicalAssessmentDetailsView(
        studentName: studentName,
      ),
    );
  }
}

class _PhysicalAssessmentDetailsView extends StatelessWidget {
  const _PhysicalAssessmentDetailsView({
    required this.studentName,
  });

  final String studentName;

  @override
  Widget build(BuildContext context) {
    final viewModel =
    context.watch<PhysicalAssessmentDetailsViewModel>();

    return Scaffold(
      bottomNavigationBar:
      const ProfessorAdminBottomNavigation(
        currentItem: ProfessorAdminNavItem.students,
      ),
      body: SafeArea(
        child: CustomScrollView(
          slivers: [
            const ProfessorAdminPageHeader(),

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
                  onRetry: viewModel.load,
                ),
              )
            else if (viewModel.assessment != null)
                SliverToBoxAdapter(
                  child: _Content(
                    assessment: viewModel.assessment!,
                    studentName: studentName,
                    isLatest: viewModel.isLatest,
                  ),
                ),
          ],
        ),
      ),
    );
  }
}

class _Content extends StatelessWidget {
  const _Content({
    required this.assessment,
    required this.studentName,
    required this.isLatest,
  });

  final PhysicalAssessment assessment;
  final String studentName;
  final bool isLatest;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Padding(
      padding: const EdgeInsets.fromLTRB(
        20,
        12,
        20,
        32,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            'Detalhe da avaliação física',
            style: theme.textTheme.headlineSmall?.copyWith(
              fontWeight: FontWeight.w800,
            ),
          ),

          const SizedBox(height: 6),

          Text(
            studentName,
            style: theme.textTheme.bodyLarge?.copyWith(
              color: colorScheme.onSurfaceVariant,
            ),
          ),

          const SizedBox(height: 20),

          _AssessmentHeader(
            assessment: assessment,
          ),

          const SizedBox(height: 16),

          if (isLatest) ...[
            _CurrentStatusCard(
              assessment: assessment,
            ),
            const SizedBox(height: 16),
          ] else ...[
            _HistoricalInfoCard(
              assessment: assessment,
            ),
            const SizedBox(height: 16),
          ],

          _MeasurementsCard(
            assessment: assessment,
          ),

          const SizedBox(height: 16),

          _NotesCard(
            notes: assessment.notes,
          ),

          const SizedBox(height: 16),

          Container(
            padding: const EdgeInsets.all(16),
            decoration: BoxDecoration(
              color: colorScheme.primary.withAlpha(16),
              borderRadius: BorderRadius.circular(14),
            ),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Icon(
                  Icons.info_outline_rounded,
                  color: colorScheme.primary,
                ),
                const SizedBox(width: 12),
                const Expanded(
                  child: Text(
                    'As medidas são usadas para acompanhar '
                        'a evolução do aluno ao longo do tempo.',
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

class _AssessmentHeader extends StatelessWidget {
  const _AssessmentHeader({
    required this.assessment,
  });

  final PhysicalAssessment assessment;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(
          color: colorScheme.outlineVariant.withAlpha(120),
        ),
      ),
      child: Row(
        children: [
          Icon(
            Icons.calendar_month_outlined,
            color: colorScheme.primary,
            size: 28,
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Data da avaliação',
                  style: theme.textTheme.bodySmall?.copyWith(
                    color: colorScheme.onSurfaceVariant,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  _formatDate(
                    assessment.assessmentDate,
                  ),
                  style: theme.textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.w800,
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

class _CurrentStatusCard extends StatelessWidget {
  const _CurrentStatusCard({
    required this.assessment,
  });

  final PhysicalAssessment assessment;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    final due = assessment.isReassessmentDue;

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
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(
            due
                ? Icons.warning_amber_rounded
                : Icons.check_circle_rounded,
            color: color,
            size: 28,
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  due
                      ? 'Reavaliação recomendada'
                      : 'Avaliação em dia',
                  style: theme.textTheme.titleMedium?.copyWith(
                    color: color,
                    fontWeight: FontWeight.w800,
                  ),
                ),
                const SizedBox(height: 5),
                Text(
                  due
                      ? 'Uma nova avaliação já é recomendada.'
                      : 'Próxima avaliação recomendada para '
                      '${_formatDate(assessment.nextAssessmentDate)}.',
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _HistoricalInfoCard extends StatelessWidget {
  const _HistoricalInfoCard({
    required this.assessment,
  });

  final PhysicalAssessment assessment;

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
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(
            Icons.history_rounded,
            color: colorScheme.primary,
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Avaliação histórica',
                  style: theme.textTheme.titleSmall?.copyWith(
                    fontWeight: FontWeight.w800,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  'Na época, a próxima avaliação era recomendada '
                      'para ${_formatDate(assessment.nextAssessmentDate)}.',
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _MeasurementsCard extends StatelessWidget {
  const _MeasurementsCard({
    required this.assessment,
  });

  final PhysicalAssessment assessment;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    final measurements = [
      _Measurement(
        'Peso',
        '${_formatNumber(assessment.weightKg)} kg',
      ),
      _Measurement(
        'Altura',
        '${_formatNumber(assessment.heightCm)} cm',
      ),
      _Measurement(
        '% Gordura',
        _optionalPercentage(
          assessment.bodyFatPercentage,
        ),
      ),
      _Measurement(
        'Peito',
        _optionalCm(assessment.chestCm),
      ),
      _Measurement(
        'Cintura',
        _optionalCm(assessment.waistCm),
      ),
      _Measurement(
        'Abdômen',
        _optionalCm(assessment.abdomenCm),
      ),
      _Measurement(
        'Quadril',
        _optionalCm(assessment.hipCm),
      ),
      _Measurement(
        'Braço direito',
        _optionalCm(assessment.rightArmCm),
      ),
      _Measurement(
        'Braço esquerdo',
        _optionalCm(assessment.leftArmCm),
      ),
      _Measurement(
        'Coxa direita',
        _optionalCm(assessment.rightThighCm),
      ),
      _Measurement(
        'Coxa esquerda',
        _optionalCm(assessment.leftThighCm),
      ),
      _Measurement(
        'Panturrilha direita',
        _optionalCm(assessment.rightCalfCm),
      ),
      _Measurement(
        'Panturrilha esquerda',
        _optionalCm(assessment.leftCalfCm),
      ),
    ];

    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(
          color: colorScheme.outlineVariant.withAlpha(120),
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Icon(
                Icons.monitor_weight_outlined,
                color: colorScheme.primary,
              ),
              const SizedBox(width: 10),
              Text(
                'Medidas corporais',
                style: theme.textTheme.titleLarge?.copyWith(
                  fontWeight: FontWeight.w800,
                ),
              ),
            ],
          ),

          const SizedBox(height: 20),

          LayoutBuilder(
            builder: (context, constraints) {
              const spacing = 12.0;
              final itemWidth =
                  (constraints.maxWidth - spacing) / 2;

              return Wrap(
                spacing: spacing,
                runSpacing: 12,
                children: [
                  for (final measurement in measurements)
                    SizedBox(
                      width: itemWidth,
                      child: _MeasurementTile(
                        measurement: measurement,
                      ),
                    ),
                ],
              );
            },
          ),
        ],
      ),
    );
  }
}

class _Measurement {
  const _Measurement(
      this.label,
      this.value,
      );

  final String label;
  final String value;
}

class _MeasurementTile extends StatelessWidget {
  const _MeasurementTile({
    required this.measurement,
  });

  final _Measurement measurement;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(12),
        border: Border.all(
          color: colorScheme.outlineVariant.withAlpha(100),
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            measurement.label,
            style: theme.textTheme.bodySmall?.copyWith(
              color: colorScheme.onSurfaceVariant,
            ),
          ),
          const SizedBox(height: 5),
          Text(
            measurement.value,
            style: theme.textTheme.titleSmall?.copyWith(
              fontWeight: FontWeight.w800,
            ),
          ),
        ],
      ),
    );
  }
}

class _NotesCard extends StatelessWidget {
  const _NotesCard({
    required this.notes,
  });

  final String? notes;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    final normalizedNotes = notes?.trim();

    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(
          color: colorScheme.outlineVariant.withAlpha(120),
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(
                Icons.notes_rounded,
                color: colorScheme.primary,
              ),
              const SizedBox(width: 10),
              Text(
                'Observações',
                style: theme.textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.w800,
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          Text(
            normalizedNotes == null ||
                normalizedNotes.isEmpty
                ? 'Nenhuma observação registrada.'
                : normalizedNotes,
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

String _optionalCm(double? value) {
  if (value == null) {
    return '—';
  }

  return '${_formatNumber(value)} cm';
}

String _optionalPercentage(double? value) {
  if (value == null) {
    return '—';
  }

  return '${_formatNumber(value)}%';
}
