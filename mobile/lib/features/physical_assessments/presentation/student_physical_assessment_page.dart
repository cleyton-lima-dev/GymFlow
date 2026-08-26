import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:gymflow/core/network/api_client.dart';
import 'package:gymflow/features/home/presentation/widgets/professor_admin_brand_header.dart';
import 'package:gymflow/features/home/presentation/widgets/student_bottom_navigation.dart';
import 'package:gymflow/features/physical_assessments/data/physical_assessments_service.dart';
import 'package:gymflow/features/physical_assessments/models/physical_assessment.dart';
import 'package:gymflow/features/physical_assessments/presentation/physical_assessment_summary_view_model.dart';
import 'package:provider/provider.dart';

class StudentPhysicalAssessmentPage extends StatelessWidget {
  const StudentPhysicalAssessmentPage({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) => PhysicalAssessmentSummaryViewModel.forCurrentUser(
        PhysicalAssessmentsService(context.read<ApiClient>()),
      )..load(),
      child: const _StudentPhysicalAssessmentView(),
    );
  }
}

class _StudentPhysicalAssessmentView extends StatelessWidget {
  const _StudentPhysicalAssessmentView();

  @override
  Widget build(BuildContext context) {
    final viewModel = context.watch<PhysicalAssessmentSummaryViewModel>();

    return Scaffold(
      body: SafeArea(
        bottom: false,
        child: RefreshIndicator(
          onRefresh: viewModel.load,
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
                        'Minha Avaliação Física',
                        style: Theme.of(context).textTheme.headlineMedium
                            ?.copyWith(fontWeight: FontWeight.w800),
                      ),

                      const SizedBox(height: 6),

                      Text(
                        'Acompanhe seus dados e a recomendação da próxima avaliação.',
                        style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                          color: Theme.of(context).colorScheme.onSurfaceVariant,
                          height: 1.4,
                        ),
                      ),

                      const SizedBox(height: 26),

                      if (viewModel.isLoading)
                        const _LoadingState()
                      else if (viewModel.errorMessage != null)
                        _ErrorState(
                          message: viewModel.errorMessage!,
                          onRetry: viewModel.load,
                        )
                      else if (viewModel.latest == null)
                        const _EmptyAssessmentState()
                      else
                        StudentPhysicalAssessmentContent(
                          assessment: viewModel.latest!,
                        ),
                      if (!viewModel.isLoading &&
                          viewModel.errorMessage == null) ...[
                        const SizedBox(height: 18),

                        OutlinedButton.icon(
                          onPressed: () {
                            context.push(
                              '/student/physical-assessment/history',
                            );
                          },
                          icon: const Icon(Icons.history_rounded),
                          label: const Text('Meu Histórico de Avaliações'),
                        ),
                      ],
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

class StudentPhysicalAssessmentContent extends StatelessWidget {
  const StudentPhysicalAssessmentContent({
    required this.assessment,
    this.historical = false,
    super.key,
  });

  final PhysicalAssessment assessment;
  final bool historical;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (historical)
          _HistoricalAssessmentCard(assessmentDate: assessment.assessmentDate)
        else
          _StatusCard(assessment: assessment),

        const SizedBox(height: 16),

        _LatestAssessmentCard(assessment: assessment, historical: historical),

        const SizedBox(height: 16),

        _MeasurementsCard(assessment: assessment),

        const SizedBox(height: 16),

        _NotesCard(notes: assessment.notes),

        const SizedBox(height: 16),

        const _InfoCard(),
      ],
    );
  }
}

class _HistoricalAssessmentCard extends StatelessWidget {
  const _HistoricalAssessmentCard({required this.assessmentDate});

  final DateTime assessmentDate;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: colorScheme.outlineVariant.withAlpha(120)),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 46,
            height: 46,
            decoration: BoxDecoration(
              color: colorScheme.primary.withAlpha(18),
              shape: BoxShape.circle,
            ),
            child: Icon(Icons.history_rounded, color: colorScheme.primary),
          ),

          const SizedBox(width: 14),

          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Avaliação histórica',
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    color: colorScheme.primary,
                    fontWeight: FontWeight.w800,
                  ),
                ),

                const SizedBox(height: 5),

                Text(
                  'Avaliação realizada em '
                  '${_formatDate(assessmentDate)}. '
                  'O status atual considera sempre sua avaliação mais recente.',
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

class _StatusCard extends StatelessWidget {
  const _StatusCard({required this.assessment});

  final PhysicalAssessment assessment;

  @override
  Widget build(BuildContext context) {
    final due = assessment.isReassessmentDue;
    final statusColor = due ? Colors.orange : Colors.green;

    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: colorScheme.outlineVariant.withAlpha(120)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Container(
                width: 46,
                height: 46,
                decoration: BoxDecoration(
                  color: statusColor.withAlpha(18),
                  shape: BoxShape.circle,
                ),
                child: Icon(
                  due
                      ? Icons.warning_amber_rounded
                      : Icons.check_circle_rounded,
                  color: statusColor,
                ),
              ),

              const SizedBox(width: 14),

              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Status',
                      style: Theme.of(context).textTheme.bodySmall
                          ?.copyWith(color: colorScheme.onSurfaceVariant),
                    ),

                    const SizedBox(height: 3),

                    Text(
                      due ? 'Reavaliação recomendada' : 'Em dia',
                      style: Theme.of(context).textTheme.titleLarge?.copyWith(
                        color: statusColor,
                        fontWeight: FontWeight.w800,
                      ),
                    ),

                    const SizedBox(height: 5),

                    Text(
                      due
                          ? 'Uma nova avaliação física já é recomendada.'
                          : 'Sua avaliação física está atualizada.',
                      style: Theme.of(context).textTheme.bodyMedium
                          ?.copyWith(color: colorScheme.onSurfaceVariant),
                    ),
                  ],
                ),
              ),
            ],
          ),

          const SizedBox(height: 18),

          const Divider(height: 1),

          const SizedBox(height: 16),

          Row(
            children: [
              Icon(Icons.calendar_month_outlined, color: colorScheme.primary),

              const SizedBox(width: 10),

              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      due
                          ? 'Reavaliação recomendada desde'
                          : 'Próxima avaliação recomendada',
                      style: Theme.of(context).textTheme.bodySmall
                          ?.copyWith(color: colorScheme.onSurfaceVariant),
                    ),

                    const SizedBox(height: 3),

                    Text(
                      _formatDate(assessment.nextAssessmentDate),
                      style: Theme.of(context).textTheme.titleMedium
                          ?.copyWith(fontWeight: FontWeight.w800),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _LatestAssessmentCard extends StatelessWidget {
  const _LatestAssessmentCard({
    required this.assessment,
    required this.historical,
  });

  final PhysicalAssessment assessment;
  final bool historical;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: colorScheme.outlineVariant.withAlpha(120)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Icon(Icons.calendar_today_outlined, color: colorScheme.primary),

              const SizedBox(width: 10),

              Expanded(
                child: Text(
                  historical ? 'Avaliação selecionada' : 'Última avaliação',
                  style: Theme.of(context).textTheme.titleMedium
                      ?.copyWith(fontWeight: FontWeight.w800),
                ),
              ),

              Text(
                _formatDate(assessment.assessmentDate),
                style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  color: colorScheme.primary,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ],
          ),

          const SizedBox(height: 18),

          const Divider(height: 1),

          const SizedBox(height: 18),

          Row(
            children: [
              Expanded(
                child: _MainMetric(
                  label: 'Peso',
                  value: '${_formatNumber(assessment.weightKg)} kg',
                ),
              ),
              Expanded(
                child: _MainMetric(
                  label: 'Altura',
                  value: '${_formatNumber(assessment.heightCm)} cm',
                ),
              ),
              Expanded(
                child: _MainMetric(
                  label: '% Gordura',
                  value: _optionalPercentage(assessment.bodyFatPercentage),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _MainMetric extends StatelessWidget {
  const _MainMetric({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          label,
          style: Theme.of(context).textTheme.bodySmall
              ?.copyWith(color: colorScheme.onSurfaceVariant),
        ),

        const SizedBox(height: 5),

        Text(
          value,
          style: Theme.of(context).textTheme.titleMedium
              ?.copyWith(fontWeight: FontWeight.w800),
        ),
      ],
    );
  }
}

class _MeasurementsCard extends StatelessWidget {
  const _MeasurementsCard({required this.assessment});

  final PhysicalAssessment assessment;

  @override
  Widget build(BuildContext context) {
    final measurements = [
      _Measurement('Peito', _optionalCm(assessment.chestCm)),
      _Measurement('Cintura', _optionalCm(assessment.waistCm)),
      _Measurement('Abdômen', _optionalCm(assessment.abdomenCm)),
      _Measurement('Quadril', _optionalCm(assessment.hipCm)),
      _Measurement('Braço direito', _optionalCm(assessment.rightArmCm)),
      _Measurement('Braço esquerdo', _optionalCm(assessment.leftArmCm)),
      _Measurement('Coxa direita', _optionalCm(assessment.rightThighCm)),
      _Measurement('Coxa esquerda', _optionalCm(assessment.leftThighCm)),
      _Measurement('Panturrilha direita', _optionalCm(assessment.rightCalfCm)),
      _Measurement('Panturrilha esquerda', _optionalCm(assessment.leftCalfCm)),
    ];

    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: colorScheme.outlineVariant.withAlpha(120)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Icon(Icons.straighten_rounded, color: colorScheme.primary),

              const SizedBox(width: 10),

              Text(
                'Medidas corporais',
                style: Theme.of(context).textTheme.titleLarge
                    ?.copyWith(fontWeight: FontWeight.w800),
              ),
            ],
          ),

          const SizedBox(height: 18),

          LayoutBuilder(
            builder: (context, constraints) {
              const spacing = 10.0;

              final itemWidth = (constraints.maxWidth - spacing) / 2;

              return Wrap(
                spacing: spacing,
                runSpacing: 10,
                children: [
                  for (final measurement in measurements)
                    SizedBox(
                      width: itemWidth,
                      child: _MeasurementTile(measurement: measurement),
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
  const _Measurement(this.label, this.value);

  final String label;
  final String value;
}

class _MeasurementTile extends StatelessWidget {
  const _MeasurementTile({required this.measurement});

  final _Measurement measurement;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: colorScheme.outlineVariant.withAlpha(90)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            measurement.label,
            style: Theme.of(context).textTheme.bodySmall
                ?.copyWith(color: colorScheme.onSurfaceVariant),
          ),

          const SizedBox(height: 5),

          Text(
            measurement.value,
            style: Theme.of(context).textTheme.titleSmall
                ?.copyWith(fontWeight: FontWeight.w800),
          ),
        ],
      ),
    );
  }
}

class _NotesCard extends StatelessWidget {
  const _NotesCard({required this.notes});

  final String? notes;

  @override
  Widget build(BuildContext context) {
    final normalized = notes?.trim();

    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: colorScheme.outlineVariant.withAlpha(120)),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(Icons.notes_rounded, color: colorScheme.primary),

          const SizedBox(width: 12),

          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Observações',
                  style: Theme.of(context).textTheme.titleMedium
                      ?.copyWith(fontWeight: FontWeight.w800),
                ),

                const SizedBox(height: 6),

                Text(
                  normalized == null || normalized.isEmpty
                      ? 'Nenhuma observação registrada.'
                      : normalized,
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

class _InfoCard extends StatelessWidget {
  const _InfoCard();

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      padding: const EdgeInsets.all(16),
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
            child: Text(
              'As medidas são usadas para acompanhar sua evolução ao longo do tempo.',
              style: Theme.of(context).textTheme.bodyMedium
                  ?.copyWith(color: colorScheme.onSurfaceVariant, height: 1.4),
            ),
          ),
        ],
      ),
    );
  }
}

class _EmptyAssessmentState extends StatelessWidget {
  const _EmptyAssessmentState();

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 54),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: colorScheme.outlineVariant.withAlpha(120)),
      ),
      child: Column(
        children: [
          Container(
            width: 72,
            height: 72,
            decoration: BoxDecoration(
              color: colorScheme.primary.withAlpha(18),
              shape: BoxShape.circle,
            ),
            child: Icon(
              Icons.monitor_weight_outlined,
              size: 36,
              color: colorScheme.primary,
            ),
          ),

          const SizedBox(height: 22),

          Text(
            'Nenhuma avaliação física registrada',
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.titleLarge
                ?.copyWith(fontWeight: FontWeight.w800),
          ),

          const SizedBox(height: 10),

          Text(
            'Quando sua academia registrar uma avaliação física, '
            'os dados aparecerão aqui.',
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.bodyMedium
                ?.copyWith(color: colorScheme.onSurfaceVariant, height: 1.4),
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

String _formatDate(DateTime value) {
  return '${value.day.toString().padLeft(2, '0')}/'
      '${value.month.toString().padLeft(2, '0')}/'
      '${value.year}';
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
