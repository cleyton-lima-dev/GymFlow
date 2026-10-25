import 'package:avelri_gestao/app/theme/branding_controller.dart';
import 'package:avelri_gestao/app/theme/semantic_colors.dart';
import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/core/widgets/app_filter_button.dart';
import 'package:avelri_gestao/features/charges/data/charges_service.dart';
import 'package:avelri_gestao/features/charges/presentation/charges_view_model.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:avelri_gestao/features/charges/models/charge_summary.dart';
import 'package:intl/intl.dart';
import 'package:avelri_gestao/features/charges/models/payment_details.dart';

class ChargesPage extends StatelessWidget {
  const ChargesPage({this.overdueOnly = false, this.paidMonth, super.key});

  final bool overdueOnly;
  final DateTime? paidMonth;

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) => ChargesViewModel(
        ChargesService(context.read<ApiClient>()),
        initialStatus: overdueOnly
            ? ChargesStatusFilter.overdue
            : paidMonth != null
            ? ChargesStatusFilter.paid
            : ChargesStatusFilter.all,
        paidMonth: paidMonth,
      )..loadInitial(),
      child: const _ChargesView(),
    );
  }
}

class _ChargesView extends StatelessWidget {
  const _ChargesView();

  @override
  Widget build(BuildContext context) {
    final viewModel = context.watch<ChargesViewModel>();

    final branding = context.watch<BrandingController>().branding;

    final primaryColor = branding.primaryColor;
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return SingleChildScrollView(
      padding: const EdgeInsets.all(30),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 1250),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
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
                  onPressed: viewModel.isLoading ? null : viewModel.refresh,
                  icon: Icon(Icons.refresh_rounded, color: primaryColor),
                ),
              ],
            ),
            const SizedBox(height: 26),
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(22),
              decoration: BoxDecoration(
                color: colorScheme.surface,
                borderRadius: BorderRadius.circular(18),
                border: Border.all(color: theme.dividerColor),
              ),
              child: Column(
                children: [
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: [
                      AppFilterButton(
                        label: 'Todas',
                        selected:
                            viewModel.statusFilter == ChargesStatusFilter.all,
                        color: primaryColor,
                        onPressed: () {
                          viewModel.updateStatusFilter(ChargesStatusFilter.all);
                        },
                      ),
                      AppFilterButton(
                        label: 'Pendentes',
                        selected:
                            viewModel.statusFilter ==
                            ChargesStatusFilter.pending,
                        color: primaryColor,
                        onPressed: () {
                          viewModel.updateStatusFilter(
                            ChargesStatusFilter.pending,
                          );
                        },
                      ),
                      AppFilterButton(
                        label: 'Pagas',
                        selected:
                            viewModel.statusFilter == ChargesStatusFilter.paid,
                        color: primaryColor,
                        onPressed: () {
                          viewModel.updateStatusFilter(
                            ChargesStatusFilter.paid,
                          );
                        },
                      ),
                      AppFilterButton(
                        label: 'Em atraso',
                        selected:
                            viewModel.statusFilter ==
                            ChargesStatusFilter.overdue,
                        color: primaryColor,
                        onPressed: () {
                          viewModel.updateStatusFilter(
                            ChargesStatusFilter.overdue,
                          );
                        },
                      ),
                      AppFilterButton(
                        label: 'Canceladas',
                        selected:
                            viewModel.statusFilter ==
                            ChargesStatusFilter.cancelled,
                        color: primaryColor,
                        onPressed: () {
                          viewModel.updateStatusFilter(
                            ChargesStatusFilter.cancelled,
                          );
                        },
                      ),
                    ],
                  ),
                  if (viewModel.paidMonth case final month?) ...[
                    const SizedBox(height: 12),
                    Align(
                      alignment: Alignment.centerLeft,
                      child: InputChip(
                        key: const ValueKey('charges-paid-month-filter'),
                        label: Text(
                          'Pagas em ${DateFormat('MM/yyyy').format(month)}',
                        ),
                        onDeleted: viewModel.isLoading
                            ? null
                            : viewModel.clearPaidMonth,
                      ),
                    ),
                  ],
                  const SizedBox(height: 20),
                  if (viewModel.isLoading && !viewModel.hasCharges)
                    const Padding(
                      padding: EdgeInsets.symmetric(vertical: 70),
                      child: Center(child: CircularProgressIndicator()),
                    )
                  else if (viewModel.errorMessage != null)
                    Center(child: Text(viewModel.errorMessage!))
                  else if (viewModel.filteredCharges.isEmpty)
                    const Padding(
                      padding: EdgeInsets.symmetric(vertical: 70),
                      child: Center(
                        child: Text(
                          'Nenhuma cobrança encontrada para este filtro.',
                        ),
                      ),
                    )
                  else
                    _ChargesTable(charges: viewModel.filteredCharges),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ChargesTable extends StatefulWidget {
  const _ChargesTable({required this.charges});

  final List<ChargeSummary> charges;

  @override
  State<_ChargesTable> createState() => _ChargesTableState();
}

class _ChargesTableState extends State<_ChargesTable> {
  static const _compactBreakpoint = 760.0;

  Future<void> _confirmPayment(
    BuildContext context,
    ChargeSummary charge,
  ) async {
    final formKey = GlobalKey<FormState>();

    String paidAmountText = charge.amount.toStringAsFixed(2);

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
              title: const Text('Confirmar pagamento'),
              content: SizedBox(
                width: 430,
                child: Form(
                  key: formKey,
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      TextFormField(
                        initialValue: paidAmountText,
                        keyboardType: const TextInputType.numberWithOptions(
                          decimal: true,
                        ),
                        decoration: const InputDecoration(
                          labelText: 'Valor pago',
                          prefixText: 'R\$ ',
                        ),
                        validator: (value) {
                          final amount = double.tryParse(
                            (value ?? '').replaceAll(',', '.'),
                          );

                          if (amount == null || amount <= 0) {
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
                        keyboardType: const TextInputType.numberWithOptions(
                          decimal: true,
                        ),
                        decoration: const InputDecoration(
                          labelText: 'Desconto',
                          prefixText: 'R\$ ',
                        ),
                        validator: (value) {
                          final discount = double.tryParse(
                            (value ?? '').replaceAll(',', '.'),
                          );

                          if (discount == null || discount < 0) {
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
                              (method) => DropdownMenuItem(
                                value: method,
                                child: Text(method.label),
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
                          final selectedDate = await showDatePicker(
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
                          decoration: const InputDecoration(
                            labelText: 'Data do pagamento',
                            prefixIcon: Icon(Icons.calendar_today_outlined),
                          ),
                          child: Text(dateFormat.format(paidAt)),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
              actions: [
                TextButton(
                  onPressed: () => Navigator.of(dialogContext).pop(),
                  child: const Text('Voltar'),
                ),
                FilledButton(
                  onPressed: () {
                    if (!(formKey.currentState?.validate() ?? false)) {
                      return;
                    }

                    final paidAmount = double.parse(
                      paidAmountText.replaceAll(',', '.'),
                    );

                    final discount = double.parse(
                      discountText.replaceAll(',', '.'),
                    );

                    if ((paidAmount + discount - charge.amount).abs() > 0.009) {
                      ScaffoldMessenger.of(dialogContext).showSnackBar(
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
                        paymentMethod: selectedMethod!,
                        paidAt: paidAt,
                      ),
                    );
                  },
                  child: const Text('Confirmar pagamento'),
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

    await context.read<ChargesViewModel>().confirmPayment(charge, payment);
  }

  Future<void> _showDetails(BuildContext context, ChargeSummary charge) async {
    final currency = NumberFormat.currency(locale: 'pt_BR', symbol: 'R\$');

    final dateFormat = DateFormat('dd/MM/yyyy');
    final dateTimeFormat = DateFormat('dd/MM/yyyy HH:mm');

    await showDialog<void>(
      context: context,
      builder: (dialogContext) {
        return AlertDialog(
          title: const Text('Detalhes da cobrança'),
          content: SizedBox(
            width: 430,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('Aluno: ${charge.studentName}'),
                const SizedBox(height: 10),
                Text('Plano: ${charge.planName}'),
                const SizedBox(height: 10),
                Text('Valor: ${currency.format(charge.amount)}'),
                const SizedBox(height: 10),
                Text(
                  'Valor pago: ${charge.paidAmount == null ? 'Não registrado' : currency.format(charge.paidAmount)}',
                ),
                const SizedBox(height: 10),
                Text('Desconto: ${currency.format(charge.discountAmount)}'),
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
                Text('Status: ${_statusLabel(charge.status)}'),
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
              onPressed: () => Navigator.of(dialogContext).pop(),
              child: const Text('Fechar'),
            ),
          ],
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    final currency = NumberFormat.currency(locale: 'pt_BR', symbol: 'R\$');
    final dateFormat = DateFormat('dd/MM/yyyy');

    return LayoutBuilder(
      builder: (context, constraints) {
        if (constraints.maxWidth < _compactBreakpoint) {
          return Column(
            key: const ValueKey('charges-compact-list'),
            children: [
              for (final charge in widget.charges)
                _buildCompactCharge(context, charge, currency, dateFormat),
            ],
          );
        }

        return Column(
          key: const ValueKey('charges-responsive-table'),
          children: [
            _buildTableRow(
              context,
              isHeader: true,
              student: const Text('Aluno'),
              plan: const Text('Plano'),
              amount: const Text('Valor'),
              dueDate: const Text('Vencimento'),
              status: const Text('Status'),
              actions: const Text('Ações'),
            ),
            const Divider(height: 1),
            for (final charge in widget.charges)
              _buildTableRow(
                context,
                student: _ellipsized(charge.studentName),
                plan: _ellipsized(charge.planName),
                amount: Text(currency.format(charge.amount)),
                dueDate: Text(dateFormat.format(charge.dueDate)),
                status: _ChargeStatusBadge(
                  label: _statusLabel(charge.status),
                  status: charge.status,
                ),
                actions: _buildActions(context, charge),
              ),
          ],
        );
      },
    );
  }

  Widget _buildTableRow(
    BuildContext context, {
    required Widget student,
    required Widget plan,
    required Widget amount,
    required Widget dueDate,
    required Widget status,
    required Widget actions,
    bool isHeader = false,
  }) {
    final colors = Theme.of(context).colorScheme;
    final style = isHeader
        ? TextStyle(
            color: colors.onSurfaceVariant,
            fontSize: 11,
            fontWeight: FontWeight.w800,
          )
        : TextStyle(color: colors.onSurface, fontSize: 12);

    return Container(
      key: isHeader ? const ValueKey('charges-table-header') : null,
      constraints: BoxConstraints(minHeight: isHeader ? 42 : 62),
      padding: const EdgeInsets.symmetric(vertical: 8),
      decoration: BoxDecoration(
        color: isHeader ? colors.surfaceContainerHighest : null,
        border: isHeader
            ? null
            : Border(bottom: BorderSide(color: Theme.of(context).dividerColor)),
      ),
      child: Row(
        children: [
          _chargeCell(flex: 19, child: student, style: style),
          _chargeCell(flex: 16, child: plan, style: style),
          _chargeCell(flex: 8, child: amount, style: style),
          _chargeCell(flex: 10, child: dueDate, style: style),
          _chargeCell(flex: 10, child: status, style: style),
          Expanded(flex: 5, child: Center(child: actions)),
        ],
      ),
    );
  }

  Widget _chargeCell({
    required int flex,
    required Widget child,
    required TextStyle style,
  }) => Expanded(
    flex: flex,
    child: Padding(
      padding: const EdgeInsets.symmetric(horizontal: 4),
      child: DefaultTextStyle.merge(
        style: style,
        maxLines: 2,
        overflow: TextOverflow.ellipsis,
        child: child,
      ),
    ),
  );

  Widget _ellipsized(String value) => Tooltip(
    message: value,
    child: Text(value, maxLines: 2, overflow: TextOverflow.ellipsis),
  );

  Widget _buildActions(BuildContext context, ChargeSummary charge) =>
      PopupMenuButton<String>(
        key: ValueKey('charge-actions-${charge.id}'),
        tooltip: 'Ações',
        padding: EdgeInsets.zero,
        constraints: const BoxConstraints(minWidth: 40, minHeight: 40),
        icon: const Icon(Icons.more_vert_rounded, size: 20),
        onSelected: (action) async {
          if (action == 'details') {
            await _showDetails(context, charge);
            return;
          }
          if (action == 'confirm') {
            await _confirmPayment(context, charge);
          }
        },
        itemBuilder: (context) => [
          const PopupMenuItem(
            value: 'details',
            child: ListTile(
              leading: Icon(Icons.visibility_outlined),
              title: Text('Ver detalhes'),
              contentPadding: EdgeInsets.zero,
            ),
          ),
          if (charge.status == ChargeStatus.pending ||
              charge.status == ChargeStatus.overdue)
            const PopupMenuItem(
              value: 'confirm',
              child: ListTile(
                leading: Icon(Icons.check_circle_outline),
                title: Text('Confirmar pagamento'),
                contentPadding: EdgeInsets.zero,
              ),
            ),
        ],
      );

  Widget _buildCompactCharge(
    BuildContext context,
    ChargeSummary charge,
    NumberFormat currency,
    DateFormat dateFormat,
  ) {
    final colors = Theme.of(context).colorScheme;
    return Container(
      key: ValueKey('charge-card-${charge.id}'),
      width: double.infinity,
      margin: const EdgeInsets.only(bottom: 10),
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: colors.surface,
        border: Border.all(color: Theme.of(context).dividerColor),
        borderRadius: BorderRadius.circular(12),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    _ellipsized(charge.studentName),
                    const SizedBox(height: 3),
                    _ellipsized(charge.planName),
                  ],
                ),
              ),
              _buildActions(context, charge),
            ],
          ),
          const SizedBox(height: 8),
          Wrap(
            spacing: 14,
            runSpacing: 6,
            children: [
              Text('Valor: ${currency.format(charge.amount)}'),
              Text('Vencimento: ${dateFormat.format(charge.dueDate)}'),
              _ChargeStatusBadge(
                label: _statusLabel(charge.status),
                status: charge.status,
              ),
            ],
          ),
        ],
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

class _ChargeStatusBadge extends StatelessWidget {
  const _ChargeStatusBadge({required this.label, required this.status});

  final String label;
  final ChargeStatus status;

  @override
  Widget build(BuildContext context) {
    final color = switch (status) {
      ChargeStatus.paid => AvelriSemanticColors.success,
      ChargeStatus.pending => AvelriSemanticColors.warning,
      ChargeStatus.overdue => AvelriSemanticColors.error,
      ChargeStatus.cancelled => Theme.of(context).colorScheme.onSurfaceVariant,
    };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 4),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.10),
        borderRadius: BorderRadius.circular(20),
      ),
      child: Text(
        label,
        maxLines: 2,
        overflow: TextOverflow.ellipsis,
        textAlign: TextAlign.center,
        style: TextStyle(
          color: Theme.of(context).colorScheme.onSurface,
          fontSize: 11,
          fontWeight: FontWeight.w700,
        ),
      ),
    );
  }
}
