import 'package:flutter/material.dart';
import 'package:gymflow/features/physical_assessments/models/physical_assessment.dart';
import 'package:gymflow/features/physical_assessments/presentation/physical_assessment_summary_view_model.dart';
import 'package:provider/provider.dart';

class PhysicalAssessmentSummaryCard extends StatelessWidget {
  const PhysicalAssessmentSummaryCard({
    required this.onHistoryTap,
    required this.onNewAssessmentTap,
    super.key,
  });

  final VoidCallback onHistoryTap;
  final VoidCallback onNewAssessmentTap;

  @override
  Widget build(BuildContext context) {
    final viewModel =
    context.watch<PhysicalAssessmentSummaryViewModel>();

    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(
          color: colorScheme.outlineVariant.withAlpha(120),
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const _Header(),

          const Divider(height: 1),

          if (viewModel.isLoading)
            const Padding(
              padding: EdgeInsets.all(36),
              child: Center(
                child: CircularProgressIndicator(),
              ),
            )
          else if (viewModel.errorMessage != null)
            _ErrorState(
              message: viewModel.errorMessage!,
              onRetry: viewModel.load,
            )
          else if (!viewModel.hasAssessment)
              _EmptyState(
                onNewAssessmentTap: onNewAssessmentTap,
              )
            else
              _AssessmentState(
                assessment: viewModel.latest!,
                onHistoryTap: onHistoryTap,
                onNewAssessmentTap: onNewAssessmentTap,
              ),
        ],
      ),
    );
  }
}

class _Header extends StatelessWidget {
  const _Header();

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Padding(
      padding: const EdgeInsets.all(20),
      child: Row(
        children: [
          Container(
            width: 44,
            height: 44,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: colorScheme.primary.withAlpha(22),
            ),
            child: Icon(
              Icons.monitor_weight_outlined,
              color: colorScheme.primary,
            ),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Text(
              'Avaliação Física',
              style: theme.textTheme.titleLarge?.copyWith(
                fontWeight: FontWeight.w800,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _AssessmentState extends StatelessWidget {
  const _AssessmentState({
    required this.assessment,
    required this.onHistoryTap,
    required this.onNewAssessmentTap,
  });

  final PhysicalAssessment assessment;
  final VoidCallback onHistoryTap;
  final VoidCallback onNewAssessmentTap;

  @override
  Widget build(BuildContext context) {
    final due = assessment.isReassessmentDue;

    return Padding(
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _StatusPanel(
            isReassessmentDue: due,
          ),

          const SizedBox(height: 22),

          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: _DateInfo(
                  label: 'Última avaliação',
                  date: assessment.assessmentDate,
                ),
              ),

              const SizedBox(width: 16),

              Expanded(
                child: _DateInfo(
                  label: 'Próxima recomendada',
                  date: assessment.nextAssessmentDate,
                ),
              ),
            ],
          ),

          const SizedBox(height: 20),
          const Divider(),
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
                  value: assessment.bodyFatPercentage == null
                      ? '—'
                      : '${_formatNumber(assessment.bodyFatPercentage!)}%',
                ),
              ),
            ],
          ),

          const SizedBox(height: 22),

          _ActionTile(
            icon: Icons.history_rounded,
            label: 'Ver histórico de avaliações',
            onTap: onHistoryTap,
          ),

          const SizedBox(height: 10),

          _ActionTile(
            icon: Icons.add_rounded,
            label: 'Nova avaliação',
            onTap: onNewAssessmentTap,
          ),
        ],
      ),
    );
  }

  static String _formatNumber(double value) {
    final formatted = value % 1 == 0
        ? value.toStringAsFixed(0)
        : value.toStringAsFixed(1);

    return formatted.replaceAll('.', ',');
  }
}

class _StatusPanel extends StatelessWidget {
  const _StatusPanel({
    required this.isReassessmentDue,
  });

  final bool isReassessmentDue;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    final color = isReassessmentDue
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
            isReassessmentDue
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
                  isReassessmentDue
                      ? 'Reavaliação recomendada'
                      : 'Em dia',
                  style: theme.textTheme.titleMedium?.copyWith(
                    color: color,
                    fontWeight: FontWeight.w800,
                  ),
                ),
                const SizedBox(height: 5),
                Text(
                  isReassessmentDue
                      ? 'Sua avaliação está atrasada. '
                      'Recomendamos realizar uma nova avaliação.'
                      : 'Sua avaliação está atualizada.',
                  style: theme.textTheme.bodyMedium?.copyWith(
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
      padding: const EdgeInsets.all(20),
      child: Column(
        children: [
          Container(
            width: 54,
            height: 54,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: colorScheme.primary.withAlpha(18),
            ),
            child: Icon(
              Icons.info_outline_rounded,
              color: colorScheme.primary,
              size: 30,
            ),
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
            'Ainda não há avaliações físicas registradas '
                'para este aluno.',
            textAlign: TextAlign.center,
            style: theme.textTheme.bodyMedium?.copyWith(
              color: colorScheme.onSurfaceVariant,
              height: 1.4,
            ),
          ),

          const SizedBox(height: 20),

          SizedBox(
            width: double.infinity,
            child: FilledButton.icon(
              onPressed: onNewAssessmentTap,
              icon: const Icon(Icons.add_rounded),
              label: const Text('Nova avaliação'),
            ),
          ),
        ],
      ),
    );
  }
}

class _DateInfo extends StatelessWidget {
  const _DateInfo({
    required this.label,
    required this.date,
  });

  final String label;
  final DateTime date;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          label,
          style: theme.textTheme.bodySmall?.copyWith(
            color: colorScheme.onSurfaceVariant,
          ),
        ),
        const SizedBox(height: 7),
        Row(
          children: [
            Icon(
              Icons.calendar_month_outlined,
              color: colorScheme.primary,
              size: 20,
            ),
            const SizedBox(width: 7),
            Flexible(
              child: Text(
                _formatDate(date),
                style: theme.textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.w800,
                ),
              ),
            ),
          ],
        ),
      ],
    );
  }

  String _formatDate(DateTime date) {
    final day = date.day.toString().padLeft(2, '0');
    final month = date.month.toString().padLeft(2, '0');

    return '$day/$month/${date.year}';
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
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          label,
          style: theme.textTheme.bodySmall?.copyWith(
            color: colorScheme.onSurfaceVariant,
          ),
        ),
        const SizedBox(height: 5),
        Text(
          value,
          style: theme.textTheme.titleMedium?.copyWith(
            fontWeight: FontWeight.w800,
          ),
        ),
      ],
    );
  }
}

class _ActionTile extends StatelessWidget {
  const _ActionTile({
    required this.icon,
    required this.label,
    required this.onTap,
  });

  final IconData icon;
  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Material(
      color: Colors.transparent,
      borderRadius: BorderRadius.circular(12),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(12),
        child: Container(
          padding: const EdgeInsets.symmetric(
            horizontal: 14,
            vertical: 14,
          ),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(12),
            border: Border.all(
              color: colorScheme.outlineVariant,
            ),
          ),
          child: Row(
            children: [
              Icon(
                icon,
                color: colorScheme.primary,
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Text(
                  label,
                  style: const TextStyle(
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ),
              Icon(
                Icons.chevron_right_rounded,
                color: colorScheme.onSurfaceVariant,
              ),
            ],
          ),
        ),
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
      padding: const EdgeInsets.all(20),
      child: Column(
        children: [
          Icon(
            Icons.error_outline_rounded,
            color: colorScheme.error,
            size: 34,
          ),
          const SizedBox(height: 10),
          Text(
            message,
            textAlign: TextAlign.center,
          ),
          const SizedBox(height: 14),
          OutlinedButton(
            onPressed: onRetry,
            child: const Text('Tentar novamente'),
          ),
        ],
      ),
    );
  }
}
