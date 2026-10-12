import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/dashboard/data/dashboard_service.dart';
import 'package:avelri_gestao/features/dashboard/presentation/dashboard_view_model.dart';

class DashboardMetrics extends StatelessWidget {
  const DashboardMetrics({super.key});

  @override
  Widget build(BuildContext context) => ChangeNotifierProvider(
    create: (context) =>
        DashboardViewModel(DashboardService(context.read<ApiClient>()))..load(),
    child: const _Metrics(),
  );
}

class _Metrics extends StatelessWidget {
  const _Metrics();

  @override
  Widget build(BuildContext context) {
    final model = context.watch<DashboardViewModel>();
    final data = model.summary;
    final money = NumberFormat.currency(locale: 'pt_BR', symbol: 'R\$');
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            const Expanded(
              child: Text(
                'Dashboard gerencial',
                style: TextStyle(fontSize: 22, fontWeight: FontWeight.w800),
              ),
            ),
            IconButton(
              tooltip: 'Atualizar indicadores',
              onPressed: model.isLoading ? null : model.load,
              icon: const Icon(Icons.refresh),
            ),
          ],
        ),
        if (model.isLoading) const LinearProgressIndicator(),
        if (model.error != null) ...[
          Text(
            model.error!,
            style: TextStyle(color: Theme.of(context).colorScheme.error),
          ),
          TextButton(
            onPressed: model.isLoading ? null : model.load,
            child: const Text('Tentar novamente'),
          ),
          if (data != null)
            const Text(
              'Os valores abaixo são da última consulta bem-sucedida.',
            ),
        ],
        if (data != null) ...[
          Text(
            'Mês ${DateFormat('MM/yyyy').format(data.periodStart)} • posição em ${DateFormat('dd/MM/yyyy').format(data.today)} (academia)',
          ),
          const SizedBox(height: 12),
          Wrap(
            spacing: 12,
            runSpacing: 12,
            children: [
              _Metric(
                title: 'Receita prevista no mês',
                value: money.format(data.expectedRevenue),
                description: 'Cobranças por vencimento, líquidas de descontos; exclui canceladas.',
              ),
              _Metric(
                title: 'Receita recebida no mês',
                value: money.format(data.receivedRevenue),
                description: 'Valores pagos por data de pagamento, inclusive vencimentos de outros meses.',
              ),
              _Metric(
                title: 'Cobranças vencidas',
                value:
                    '${data.overdueCharges} • ${money.format(data.overdueAmount)}',
                description:
                    'Saldo de cobranças em atraso, de todos os períodos.',
                alert: data.overdueCharges > 0,
                onTap: () => context.go('/finance?status=overdue'),
              ),
              _Metric(
                title: 'Matrículas vigentes',
                value: '${data.activeEnrollments}',
                description: 'Ativas, já iniciadas e ainda não encerradas.',
              ),
              _Metric(
                title: 'Matrículas a vencer',
                value: '${data.expiringEnrollments}',
                description:
                    'Vigentes com término nos próximos ${data.expiringWithinDays} dias.',
                alert: data.expiringEnrollments > 0,
              ),
              _Metric(
                title: 'Alunos ativos',
                value: '${data.activeStudents}',
                description: 'Usuários ativos e alunos não arquivados.',
              ),
              _Metric(
                title: 'Sem avaliação física',
                value: '${data.studentsWithoutAssessment}',
                description: 'Alunos ativos sem avaliação registrada.',
              ),
              _Metric(
                title: 'Reavaliações pendentes',
                value: '${data.reassessmentsDue}',
                description:
                    'Alunos ativos com última avaliação há 2 meses ou mais.',
                alert: data.reassessmentsDue > 0,
              ),
            ],
          ),
          if (data.activeStudents == 0 &&
              data.activeEnrollments == 0 &&
              data.expectedRevenue == 0 &&
              data.receivedRevenue == 0 &&
              data.overdueCharges == 0)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 12),
              child: Text(
                'Sem movimentação ou cadastros ativos para os indicadores atuais.',
              ),
            ),
        ],
        const Padding(
          padding: EdgeInsets.symmetric(vertical: 16),
          child: Text(
            'Frequência e alunos ausentes: indicadores indisponíveis até integração e homologação do módulo de check-in.',
          ),
        ),
      ],
    );
  }
}

class _Metric extends StatelessWidget {
  const _Metric({
    required this.title,
    required this.value,
    required this.description,
    this.alert = false,
    this.onTap,
  });
  final String title, value, description;
  final bool alert;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final color = alert ? theme.colorScheme.error : theme.colorScheme.primary;
    return SizedBox(
      width: 280,
      child: Card(
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(16),
          side: BorderSide(color: alert ? color : theme.dividerColor),
        ),
        child: InkWell(
          onTap: onTap,
          borderRadius: BorderRadius.circular(16),
          child: Padding(
            padding: const EdgeInsets.all(18),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(title, style: theme.textTheme.titleSmall),
                const SizedBox(height: 10),
                Text(
                  value,
                  style: theme.textTheme.headlineSmall?.copyWith(
                    color: color,
                    fontWeight: FontWeight.bold,
                  ),
                ),
                const SizedBox(height: 8),
                Text(description),
                if (onTap != null)
                  const Padding(
                    padding: EdgeInsets.only(top: 8),
                    child: Text('Ver cobranças em atraso →'),
                  ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
