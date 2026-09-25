import 'package:avelri_gestao/app/theme/branding_controller.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/charges/data/charges_service.dart';
import 'package:avelri_gestao/features/charges/presentation/charges_view_model.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:avelri_gestao/features/charges/models/charge_summary.dart';
import 'package:intl/intl.dart';

class ChargesPage extends StatelessWidget {
  const ChargesPage({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) => ChargesViewModel(
        ChargesService(
          context.read<ApiClient>(),
        ),
      )..loadInitial(),
      child: const _ChargesView(),
    );
  }
}

class _ChargesView extends StatelessWidget {
  const _ChargesView();

  @override
  Widget build(BuildContext context) {
    final viewModel =
    context.watch<ChargesViewModel>();

    final branding =
        context.watch<BrandingController>().branding;

    final primaryColor = branding.primaryColor;
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return SingleChildScrollView(
      padding: const EdgeInsets.all(30),
      child: ConstrainedBox(
        constraints: const BoxConstraints(
          maxWidth: 1250,
        ),
        child: Column(
          crossAxisAlignment:
          CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment:
                    CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Cobranças',
                        style: TextStyle(
                          color: colorScheme.onSurface,
                          fontSize: 28,
                          fontWeight: FontWeight.w800,
                        ),
                      ),
                      SizedBox(height: 7),
                      Text(
                        'Acompanhe os pagamentos das matrículas.',
                        style: TextStyle(
                          color: colorScheme.onSurfaceVariant,
                          fontSize: 14,
                        ),
                      ),
                    ],
                  ),
                ),
                IconButton(
                  tooltip: 'Atualizar',
                  onPressed: viewModel.isLoading
                      ? null
                      : viewModel.refresh,
                  icon: Icon(
                    Icons.refresh_rounded,
                    color: primaryColor,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 26),
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(22),
              decoration: BoxDecoration(
                color: colorScheme.surface,
                borderRadius:
                BorderRadius.circular(18),
                border: Border.all(
                  color: theme.dividerColor,
                ),
              ),
              child: viewModel.isLoading &&
                  !viewModel.hasCharges
                  ? const Padding(
                padding: EdgeInsets.symmetric(
                  vertical: 70,
                ),
                child: Center(
                  child: CircularProgressIndicator(),
                ),
              )
                  : viewModel.errorMessage != null
                  ? Center(
                child: Text(
                  viewModel.errorMessage!,
                ),
              )
                  : !viewModel.hasCharges
                  ? const Padding(
                padding: EdgeInsets.symmetric(
                  vertical: 70,
                ),
                child: Center(
                  child: Text(
                    'Nenhuma cobrança encontrada.',
                  ),
                ),
              )
                  : _ChargesTable(
                charges: viewModel.charges,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
class _ChargesTable extends StatelessWidget {
  const _ChargesTable({
    required this.charges,
  });

  final List<ChargeSummary> charges;

  Future<void> _confirmPayment(
      BuildContext context,
      ChargeSummary charge,
      ) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) {
        return AlertDialog(
          title: const Text(
            'Confirmar pagamento',
          ),
          content: Text(
            'Confirmar o pagamento de '
                '${charge.studentName}?',
          ),
          actions: [
            TextButton(
              onPressed: () =>
                  Navigator.of(dialogContext).pop(false),
              child: const Text('Voltar'),
            ),
            FilledButton(
              onPressed: () =>
                  Navigator.of(dialogContext).pop(true),
              child: const Text(
                'Confirmar pagamento',
              ),
            ),
          ],
        );
      },
    );

    if (confirmed != true || !context.mounted) {
      return;
    }

    await context
        .read<ChargesViewModel>()
        .confirmPayment(charge);
  }

  Future<void> _showDetails(
      BuildContext context,
      ChargeSummary charge,
      ) async {
    final currency = NumberFormat.currency(
      locale: 'pt_BR',
      symbol: 'R\$',
    );

    final dateFormat = DateFormat('dd/MM/yyyy');
    final dateTimeFormat =
    DateFormat('dd/MM/yyyy HH:mm');

    await showDialog<void>(
      context: context,
      builder: (dialogContext) {
        return AlertDialog(
          title: const Text(
            'Detalhes da cobrança',
          ),
          content: SizedBox(
            width: 430,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment:
              CrossAxisAlignment.start,
              children: [
                Text('Aluno: ${charge.studentName}'),
                const SizedBox(height: 10),
                Text('Plano: ${charge.planName}'),
                const SizedBox(height: 10),
                Text(
                  'Valor: ${currency.format(charge.amount)}',
                ),
                const SizedBox(height: 10),
                Text(
                  'Vencimento: '
                      '${dateFormat.format(charge.dueDate)}',
                ),
                const SizedBox(height: 10),
                Text(
                  'Status: ${_statusLabel(charge.status)}',
                ),
                const SizedBox(height: 10),
                Text(
                  charge.paidAt == null
                      ? 'Pagamento: Não confirmado'
                      : 'Pago em: '
                      '${dateTimeFormat.format(charge.paidAt!.toLocal())}',
                ),
                const SizedBox(height: 10),
                Text(
                  'Cobrança criada em: '
                      '${dateTimeFormat.format(charge.createdAt.toLocal())}',
                ),
              ],
            ),
          ),
          actions: [
            TextButton(
              onPressed: () =>
                  Navigator.of(dialogContext).pop(),
              child: const Text('Fechar'),
            ),
          ],
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    final currency = NumberFormat.currency(
      locale: 'pt_BR',
      symbol: 'R\$',
    );

    final dateFormat = DateFormat('dd/MM/yyyy');

    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      child: DataTable(
        columns: const [
          DataColumn(label: Text('Aluno')),
          DataColumn(label: Text('Plano')),
          DataColumn(label: Text('Valor')),
          DataColumn(label: Text('Vencimento')),
          DataColumn(label: Text('Status')),
          DataColumn(label: Text('Ações')),
        ],
        rows: charges.map((charge) {
          return DataRow(
            cells: [
              DataCell(
                Text(charge.studentName),
              ),
              DataCell(
                Text(charge.planName),
              ),
              DataCell(
                Text(
                  currency.format(charge.amount),
                ),
              ),
              DataCell(
                Text(
                  dateFormat.format(charge.dueDate),
                ),
              ),
              DataCell(
                Text(
                  _statusLabel(charge.status),
                ),
              ),
              DataCell(
                PopupMenuButton<String>(
                  tooltip: 'Ações',
                  icon: const Icon(
                    Icons.more_vert_rounded,
                  ),
                  onSelected: (action) async {
                    if (action == 'details') {
                      await _showDetails(
                        context,
                        charge,
                      );
                      return;
                    }

                    if (action == 'confirm') {
                      await _confirmPayment(
                        context,
                        charge,
                      );
                    }
                  },
                  itemBuilder: (context) => [
                    const PopupMenuItem(
                      value: 'details',
                      child: ListTile(
                        leading: Icon(
                          Icons.visibility_outlined,
                        ),
                        title: Text(
                          'Ver detalhes',
                        ),
                        contentPadding: EdgeInsets.zero,
                      ),
                    ),
                    if (charge.status ==
                        ChargeStatus.pending ||
                        charge.status ==
                            ChargeStatus.overdue)
                      const PopupMenuItem(
                        value: 'confirm',
                        child: ListTile(
                          leading: Icon(
                            Icons.check_circle_outline,
                          ),
                          title: Text(
                            'Confirmar pagamento',
                          ),
                          contentPadding: EdgeInsets.zero,
                        ),
                      ),
                  ],
                ),
              ),
            ],
          );
        }).toList(),
      ),
    );
  }

  String _statusLabel(ChargeStatus status) {
    return switch (status) {
      ChargeStatus.pending => 'Pendente',
      ChargeStatus.paid => 'Pago',
      ChargeStatus.overdue => 'Em atraso',
      ChargeStatus.cancelled => 'Cancelado',
    };
  }
}
