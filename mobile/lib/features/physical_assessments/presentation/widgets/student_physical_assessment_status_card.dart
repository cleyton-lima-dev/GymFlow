import 'package:flutter/material.dart';
import 'package:gymflow/features/physical_assessments/models/physical_assessment.dart';
import 'package:gymflow/features/physical_assessments/presentation/physical_assessment_summary_view_model.dart';
import 'package:provider/provider.dart';

class StudentPhysicalAssessmentStatusCard extends StatelessWidget {
  const StudentPhysicalAssessmentStatusCard({super.key});

  @override
  Widget build(BuildContext context) {
    final viewModel = context.watch<PhysicalAssessmentSummaryViewModel>();

    final colorScheme = Theme.of(context).colorScheme;

    if (viewModel.isLoading) {
      return _Card(
        child: Row(
          children: [
            SizedBox(
              width: 22,
              height: 22,
              child: CircularProgressIndicator(
                strokeWidth: 2,
                color: colorScheme.primary,
              ),
            ),
            const SizedBox(width: 14),
            const Text('Carregando avaliação física...'),
          ],
        ),
      );
    }

    if (viewModel.errorMessage != null) {
      return _Card(
        child: Row(
          children: [
            Icon(Icons.error_outline_rounded, color: colorScheme.error),
            const SizedBox(width: 12),
            Expanded(
              child: Text(
                'Não foi possível carregar o status da avaliação física.',
                style: Theme.of(context).textTheme.bodyMedium,
              ),
            ),
            IconButton(
              onPressed: viewModel.load,
              icon: const Icon(Icons.refresh_rounded),
            ),
          ],
        ),
      );
    }

    final assessment = viewModel.latest;

    if (assessment == null) {
      return const _EmptyAssessmentCard();
    }

    return _AssessmentStatusCard(assessment: assessment);
  }
}

class _AssessmentStatusCard extends StatelessWidget {
  const _AssessmentStatusCard({required this.assessment});

  final PhysicalAssessment assessment;

  @override
  Widget build(BuildContext context) {
    final due = assessment.isReassessmentDue;

    final statusColor = due ? Colors.orange : Colors.green;

    return _Card(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Icon(
                Icons.monitor_weight_outlined,
                color: Theme.of(context).colorScheme.primary,
              ),
              const SizedBox(width: 10),
              Text(
                'Avaliação física',
                style: Theme.of(context).textTheme.titleMedium
                    ?.copyWith(fontWeight: FontWeight.w800),
              ),
            ],
          ),

          const SizedBox(height: 16),

          Row(
            children: [
              Icon(
                due ? Icons.warning_amber_rounded : Icons.check_circle_rounded,
                color: statusColor,
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Text(
                  due ? 'Reavaliação recomendada' : 'Em dia',
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    color: statusColor,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),
            ],
          ),

          const SizedBox(height: 10),

          Text(
            due
                ? 'Próxima avaliação recomendada desde '
                      '${_formatDate(assessment.nextAssessmentDate)}.'
                : 'Próxima avaliação recomendada em '
                      '${_formatDate(assessment.nextAssessmentDate)}.',
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
              color: Theme.of(context).colorScheme.onSurfaceVariant,
            ),
          ),
        ],
      ),
    );
  }
}

class _EmptyAssessmentCard extends StatelessWidget {
  const _EmptyAssessmentCard();

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return _Card(
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
                  'Avaliação física',
                  style: Theme.of(context).textTheme.titleMedium
                      ?.copyWith(fontWeight: FontWeight.w800),
                ),
                const SizedBox(height: 5),
                Text(
                  'Nenhuma avaliação física registrada.',
                  style: Theme.of(context).textTheme.bodyMedium
                      ?.copyWith(color: colorScheme.onSurfaceVariant),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _Card extends StatelessWidget {
  const _Card({required this.child});

  final Widget child;

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
      child: child,
    );
  }
}

String _formatDate(DateTime value) {
  return '${value.day.toString().padLeft(2, '0')}/'
      '${value.month.toString().padLeft(2, '0')}/'
      '${value.year}';
}
