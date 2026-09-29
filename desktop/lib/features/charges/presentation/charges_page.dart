import 'package:avelri_gestao/app/theme/branding_controller.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/charges/data/charges_service.dart';
import 'package:avelri_gestao/features/charges/presentation/charges_view_model.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:avelri_gestao/features/charges/models/charge_summary.dart';
import 'package:intl/intl.dart';
import 'package:avelri_gestao/features/charges/models/payment_details.dart';

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
              child: Column(
                children: [
                  Row(
                    children: [
                      _ChargeFilterButton(
                        label: 'Todas',
                        selected:
                        viewModel.statusFilter ==
                            ChargesStatusFilter.all,
                        color: primaryColor,
                        onTap: () {
                          viewModel.updateStatusFilter(
                            ChargesStatusFilter.all,
                          );
                        },
                      ),
                      const SizedBox(width: 8),
                      _ChargeFilterButton(
                        label: 'Pendentes',
                        selected:
                        viewModel.statusFilter ==
                            ChargesStatusFilter.pending,
                        color: primaryColor,
                        onTap: () {
                          viewModel.updateStatusFilter(
                            ChargesStatusFilter.pending,
                          );
                        },
                      ),
                      const SizedBox(width: 8),
                      _ChargeFilterButton(
                        label: 'Pagas',
                        selected:
                        viewModel.statusFilter ==
                            ChargesStatusFilter.paid,
                        color: primaryColor,
                        onTap: () {
                          viewModel.updateStatusFilter(
                            ChargesStatusFilter.paid,
                          );
                        },
                      ),
                      const SizedBox(width: 8),
                      _ChargeFilterButton(
                        label: 'Em atraso',
                        selected:
                        viewModel.statusFilter ==
                            ChargesStatusFilter.overdue,
                        color: primaryColor,
                        onTap: () {
                          viewModel.updateStatusFilter(
                            ChargesStatusFilter.overdue,
                          );
                        },
                      ),
                      const SizedBox(width: 8),
                      _ChargeFilterButton(
                        label: 'Canceladas',
                        selected:
                        viewModel.statusFilter ==
                            ChargesStatusFilter.cancelled,
                        color: primaryColor,
                        onTap: () {
                          viewModel.updateStatusFilter(
                            ChargesStatusFilter.cancelled,
                          );
                        },
                      ),
                    ],
                  ),
                  const SizedBox(height: 20),
                  if (viewModel.isLoading &&
                      !viewModel.hasCharges)
                    const Padding(
                      padding: EdgeInsets.symmetric(
                        vertical: 70,
                      ),
                      child: Center(
                        child: CircularProgressIndicator(),
                      ),
                    )
                  else if (viewModel.errorMessage != null)
                    Center(
                      child: Text(
                        viewModel.errorMessage!,
                      ),
                    )
                  else if (viewModel.filteredCharges.isEmpty)
                      const Padding(
                        padding: EdgeInsets.symmetric(
                          vertical: 70,
                        ),
                        child: Center(
                          child: Text(
                            'Nenhuma cobrança encontrada para este filtro.',
                          ),
                        ),
                      )
                    else
                      _ChargesTable(
                        charges: viewModel.filteredCharges,
                      ),
                ],
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
    final formKey = GlobalKey<FormState>();

    String paidAmountText =
    charge.amount.toStringAsFixed(2);

    String discountText = '0.00';

    PaymentMethod? selectedMethod;
    DateTime paidAt = DateTime.now();

    final dateFormat = DateFormat('dd/MM/yyyy');

    final payment = await showDialog<PaymentDetails>(
      context: context,
      builder: (dialogContext) {
        return StatefulBuilder(
          builder: (context, setDialogState) {
            return AlertDialog(
              title: const Text(
                'Confirmar pagamento',
              ),
              content: SizedBox(
                width: 430,
                child: Form(
                  key: formKey,
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      TextFormField(
                        initialValue: paidAmountText,
                        keyboardType:
                        const TextInputType.numberWithOptions(
                          decimal: true,
                        ),
                        decoration: const InputDecoration(
                          labelText: 'Valor pago',
                          prefixText: 'R\$ ',
                        ),
                        validator: (value) {
                          final amount = double.tryParse(
                            (value ?? '')
                                .replaceAll(',', '.'),
                          );

                          if (amount == null ||
                              amount <= 0) {
                            return 'Informe um valor válido.';
                          }

                          return null;
                        },
                        onChanged: (value) {
                          paidAmountText = value;
                        },
                      ),
                      const SizedBox(height: 18),
                      TextFormField(
                        initialValue: discountText,
                        keyboardType:
                        const TextInputType.numberWithOptions(
                          decimal: true,
                        ),
                        decoration: const InputDecoration(
                          labelText: 'Desconto',
                          prefixText: 'R\$ ',
                        ),
                        validator: (value) {
                          final discount = double.tryParse(
                            (value ?? '')
                                .replaceAll(',', '.'),
                          );

                          if (discount == null ||
                              discount < 0) {
                            return 'Informe um desconto válido.';
                          }

                          return null;
                        },
                        onChanged: (value) {
                          discountText = value;
                        },
                      ),
                      const SizedBox(height: 18),
                      DropdownButtonFormField<PaymentMethod>(
                        initialValue: selectedMethod,
                        decoration: const InputDecoration(
                          labelText: 'Forma de pagamento',
                        ),
                        items: PaymentMethod.values
                            .map(
                              (method) =>
                              DropdownMenuItem(
                                value: method,
                                child: Text(
                                  method.label,
                                ),
                              ),
                        )
                            .toList(),
                        validator: (value) {
                          if (value == null) {
                            return 'Selecione a forma de pagamento.';
                          }

                          return null;
                        },
                        onChanged: (value) {
                          setDialogState(() {
                            selectedMethod = value;
                          });
                        },
                      ),
                      const SizedBox(height: 18),
                      InkWell(
                        onTap: () async {
                          final selectedDate =
                          await showDatePicker(
                            context: dialogContext,
                            initialDate: paidAt,
                            firstDate: DateTime(2020),
                            lastDate: DateTime.now(),
                          );

                          if (selectedDate == null) {
                            return;
                          }

                          setDialogState(() {
                            paidAt = DateTime(
                              selectedDate.year,
                              selectedDate.month,
                              selectedDate.day,
                              paidAt.hour,
                              paidAt.minute,
                              paidAt.second,
                            );
                          });
                        },
                        child: InputDecorator(
                          decoration:
                          const InputDecoration(
                            labelText:
                            'Data do pagamento',
                            prefixIcon: Icon(
                              Icons
                                  .calendar_today_outlined,
                            ),
                          ),
                          child: Text(
                            dateFormat.format(paidAt),
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
              actions: [
                TextButton(
                  onPressed: () =>
                      Navigator.of(dialogContext)
                          .pop(),
                  child: const Text('Voltar'),
                ),
                FilledButton(
                  onPressed: () {
                    if (!(formKey.currentState
                        ?.validate() ??
                        false)) {
                      return;
                    }

                    final paidAmount =
                    double.parse(
                      paidAmountText
                          .replaceAll(',', '.'),
                    );

                    final discount =
                    double.parse(
                      discountText
                          .replaceAll(',', '.'),
                    );

                    if ((paidAmount +
                        discount -
                        charge.amount)
                        .abs() >
                        0.009) {
                      ScaffoldMessenger.of(
                        dialogContext,
                      ).showSnackBar(
                        const SnackBar(
                          content: Text(
                            'O valor pago mais o desconto deve ser igual ao valor da cobrança.',
                          ),
                        ),
                      );

                      return;
                    }

                    Navigator.of(dialogContext).pop(
                      PaymentDetails(
                        paidAmount: paidAmount,
                        discountAmount: discount,
                        paymentMethod:
                        selectedMethod!,
                        paidAt: paidAt,
                      ),
                    );
                  },
                  child: const Text(
                    'Confirmar pagamento',
                  ),
                ),
              ],
            );
          },
        );
      },
    );

    if (payment == null || !context.mounted) {
      return;
    }

    await context
        .read<ChargesViewModel>()
        .confirmPayment(
      charge,
      payment,
    );
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
                  'Valor pago: ${charge.paidAmount == null ? 'Não registrado' : currency.format(charge.paidAmount)}',
                ),
                const SizedBox(height: 10),
                Text(
                  'Desconto: ${currency.format(charge.discountAmount)}',
                ),
                const SizedBox(height: 10),
                Text(
                  'Forma de pagamento: ${charge.paymentMethod?.label ?? 'Não registrada'}',
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
class _ChargeFilterButton extends StatelessWidget {
  const _ChargeFilterButton({
    required this.label,
    required this.selected,
    required this.color,
    required this.onTap,
  });

  final String label;
  final bool selected;
  final Color color;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return OutlinedButton(
      onPressed: onTap,
      style: OutlinedButton.styleFrom(
        foregroundColor: selected
            ? colorScheme.onPrimary
            : colorScheme.onSurfaceVariant,
        backgroundColor:
        selected ? color : colorScheme.surface,
        side: BorderSide(
          color:
          selected ? color : theme.dividerColor,
        ),
      ),
      child: Text(label),
    );
  }
}
