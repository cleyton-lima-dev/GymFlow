import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/dashboard/data/dashboard_service.dart';
import 'package:avelri_gestao/features/dashboard/presentation/dashboard_card_grid.dart';
import 'package:avelri_gestao/features/dashboard/presentation/dashboard_view_model.dart';
import 'package:avelri_gestao/app/theme/semantic_colors.dart';

class DashboardMetrics extends StatelessWidget {
  const DashboardMetrics({this.erpEnabled = false, super.key});
  final bool erpEnabled;

  @override
  Widget build(BuildContext context) => ChangeNotifierProvider(
    key: ValueKey(erpEnabled),
    create: (context) => DashboardViewModel(
      DashboardService(context.read<ApiClient>()),
      includeFinancial: erpEnabled,
    )..load(),
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
    final metrics = <Widget>[];

    final financialData = data;
    if (model.includeFinancial &&
        financialData != null &&
        financialData.hasFinancial) {
      metrics.addAll([
        _Metric(
          title: 'Receita prevista no mês',
          value: money.format(financialData.expectedRevenue!),
          description: 'Cobranças por vencimento, líquidas de descontos; exclui canceladas.',
        ),
        _Metric(
          title: 'Receita recebida no mês',
          value: money.format(financialData.receivedRevenue!),
          description: 'Valores pagos por data de pagamento, inclusive vencimentos de outros meses.',
          onTap: () => context.go(
            Uri(
              path: '/finance',
              queryParameters: {
                'status': 'paid',
                'paidMonth': _dateOnly(financialData.periodStart!),
              },
            ).toString(),
          ),
          actionLabel: 'Ver cobranças pagas no mês →',
        ),
        _Metric(
          title: 'Cobranças vencidas',
          value:
              '${financialData.overdueCharges!} • ${money.format(financialData.overdueAmount!)}',
          description: 'Saldo de cobranças em atraso, de todos os períodos.',
          alert: financialData.overdueCharges! > 0,
          onTap: () => context.go('/finance?status=overdue'),
        ),
        _Metric(
          title: 'Matrículas vigentes',
          value: '${financialData.activeEnrollments!}',
          description: 'Ativas, já iniciadas e ainda não encerradas.',
        ),
        _Metric(
          title: 'Matrículas a vencer',
          value: '${financialData.expiringEnrollments!}',
          description:
              'Vigentes com término nos próximos ${financialData.expiringWithinDays!} dias.',
          alert: financialData.expiringEnrollments! > 0,
          onTap: () => context.go(
            Uri(
              path: '/enrollments',
              queryParameters: {
                'expiringWithinDays': '${financialData.expiringWithinDays}',
              },
            ).toString(),
          ),
          actionLabel: 'Ver matrículas e alunos →',
        ),
      ]);
    }

    if (data != null) {
      metrics.addAll([
        _Metric(
          title: 'Alunos ativos',
          value: '${data.activeStudents}',
          description: 'Usuários ativos e alunos não arquivados.',
        ),
        _Metric(
          title: 'Sem avaliação física',
          value: '${data.studentsWithoutAssessment}',
          description: 'Alunos ativos sem avaliação registrada.',
          onTap: () => context.go('/students?assessmentFilter=missing'),
          actionLabel: 'Ver alunos sem avaliação →',
        ),
        _Metric(
          title: 'Reavaliações pendentes',
          value: '${data.reassessmentsDue}',
          description: 'Alunos ativos com última avaliação há 2 meses ou mais.',
          alert: data.reassessmentsDue > 0,
          onTap: () => context.go('/students?assessmentFilter=reassessmentDue'),
          actionLabel: 'Ver alunos para reavaliar →',
        ),
      ]);
    }

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
            '${data.hasFinancial ? 'Mês ${DateFormat('MM/yyyy').format(data.periodStart!)} • ' : ''}posição em ${DateFormat('dd/MM/yyyy').format(data.today)} (academia)',
          ),
          const SizedBox(height: 12),
          DashboardCardGrid(
            key: const ValueKey('dashboard-metric-grid'),
            itemCount: metrics.length,
            minItemWidth: 280,
            itemHeight: 230,
            spacing: 12,
            itemBuilder: (context, index) => metrics[index],
          ),
          if (data.activeStudents == 0 &&
              (!data.hasFinancial ||
                  (data.activeEnrollments == 0 &&
                      data.expectedRevenue == 0 &&
                      data.receivedRevenue == 0 &&
                      data.overdueCharges == 0)))
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
    this.actionLabel,
  });
  final String title, value, description;
  final bool alert;
  final VoidCallback? onTap;
  final String? actionLabel;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final color = alert
        ? AvelriSemanticColors.riskFor(theme.brightness)
        : theme.colorScheme.primary;
    final content = Padding(
      padding: const EdgeInsets.all(18),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            height: 40,
            child: Align(
              alignment: Alignment.topLeft,
              child: Text(
                title,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: theme.textTheme.titleSmall,
              ),
            ),
          ),
          const SizedBox(height: 8),
          Text(
            value,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: theme.textTheme.headlineSmall?.copyWith(
              color: color,
              fontWeight: FontWeight.bold,
            ),
          ),
          const SizedBox(height: 8),
          Expanded(
            child: Text(
              description,
              maxLines: onTap == null ? 4 : 3,
              overflow: TextOverflow.ellipsis,
            ),
          ),
          if (onTap != null) ...[
            const SizedBox(height: 6),
            Text(
              actionLabel ?? 'Ver cobranças em atraso →',
              style: theme.textTheme.labelMedium?.copyWith(
                color: theme.colorScheme.primary,
                fontWeight: FontWeight.w700,
              ),
            ),
          ],
        ],
      ),
    );

    return Card(
      key: ValueKey('dashboard-metric-$title'),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(16),
        side: BorderSide(color: alert ? color : theme.dividerColor),
      ),
      clipBehavior: Clip.antiAlias,
      child: onTap == null
          ? content
          : InkWell(
              onTap: onTap,
              mouseCursor: SystemMouseCursors.click,
              borderRadius: BorderRadius.circular(16),
              child: content,
            ),
    );
  }
}

String _dateOnly(DateTime date) =>
    '${date.year.toString().padLeft(4, '0')}-${date.month.toString().padLeft(2, '0')}-${date.day.toString().padLeft(2, '0')}';
